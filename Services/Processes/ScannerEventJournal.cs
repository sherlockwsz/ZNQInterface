#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace ZNQInterface.Services.Processes
{
    public sealed record ScannerJournalEntry
    {
        public ScannerEvent Event { get; init; } = new ScannerEvent();
        public DateTime ReceivedUtc { get; init; }
        public bool Processed { get; init; }
        public string Handling { get; init; } = "Received";
        public uint ManualSeq { get; init; }
        public string SubmittedCode { get; init; } = "";
        public ScannerManualAck? ManualOutcome { get; init; }
        public uint AckSeq { get; init; }
        public ScannerQueueAck? AckOutcome { get; init; }
        public DateTime ProtocolIntentUtc { get; init; }
    }
    public sealed class ScannerEventJournal : IDisposable
    {
        private sealed record Envelope(string Body, string Sha256);
        private readonly ConcurrentDictionary<string, ScannerJournalEntry> _entries = new();
        private readonly string _path;
        private readonly FileStream _consumerLock;
        public ScannerEventJournal(string directory)
        {
            Directory.CreateDirectory(directory);
            // One application may own the scanner consumer. Other monitors still run.
            _consumerLock = new FileStream(Path.Combine(directory, "consumer.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            _path = Path.Combine(directory, "events.jsonl");
            try
            {
                if (File.Exists(_path))
                    foreach (string line in File.ReadLines(_path, Encoding.UTF8))
                    {
                        var envelope = JsonSerializer.Deserialize<Envelope>(line) ?? throw new InvalidDataException("扫码日志空记录。");
                        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(envelope.Body)));
                        if (hash != envelope.Sha256) throw new InvalidDataException("扫码日志校验失败；禁止自动ACK。");
                        var entry = JsonSerializer.Deserialize<ScannerJournalEntry>(envelope.Body) ?? throw new InvalidDataException("扫码日志载荷无效。");
                        if (entry.Event.SessionId == 0 || entry.Event.ResultId == 0 || entry.Event.RequestId == 0) throw new InvalidDataException("扫码日志身份无效。");
                        _entries[entry.Event.Key] = entry;
                    }
            }
            catch { _consumerLock.Dispose(); throw; }
        }
        public ScannerJournalEntry? Find(ScannerEvent value) => _entries.TryGetValue(value.Key, out var entry) ? entry : null;
        // Read-only observer; exact PLC-confirmed event IDs, never nearest-time matching.
        public DateTime? FindProductScanTime(ulong session, uint request, string code,
            ScannerCodeSource source, uint originalId, uint correctionId)
        {
            uint resultId = source == ScannerCodeSource.Manual ? correctionId : originalId;
            if (session == 0 || request == 0 || resultId == 0
                || !_entries.TryGetValue($"{session}:{resultId}", out var entry)) return null;
            var value = entry.Event;
            if (value.RequestId != request || !value.QRCodeValid || value.QRCode != code
                || value.CodeSource != source
                || entry.ReceivedUtc.Kind != DateTimeKind.Utc) return null;
            if (source == ScannerCodeSource.Manual)
            {
                // PLC deliberately retains ScanSkipped on accepted manual corrections.
                if (value.Result != ScannerResult.ScanSkipped || value.CorrectsResultId != originalId
                    || !_entries.TryGetValue($"{session}:{originalId}", out var original)
                    || original.Event.RequestId != request
                    || original.ManualOutcome != ScannerManualAck.Accepted
                    || original.SubmittedCode != code) return null;
            }
            else if (source != ScannerCodeSource.Tcp || value.Result != ScannerResult.ScanSuccess || value.CorrectsResultId != 0) return null;
            return entry.ReceivedUtc;
        }
        public IEnumerable<ScannerJournalEntry> Pending(ulong session) => _entries.Values.Where(e => e.Event.SessionId == session
            && ((e.ManualSeq != 0 && e.ManualOutcome == null) || (e.AckSeq != 0 && e.AckOutcome == null))).ToArray();
        public ScannerJournalEntry Save(ScannerJournalEntry entry)
        {
            string body = JsonSerializer.Serialize(entry);
            var envelope = new Envelope(body, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))));
            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope) + "\n");
            using (var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            // Publish in-memory state only after the durable append succeeds.
            _entries[entry.Event.Key] = entry; return entry;
        }
        public void Dispose() => _consumerLock.Dispose();
    }
}
