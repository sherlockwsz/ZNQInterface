#nullable enable
using System;
using System.Collections.Generic;
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
        private readonly Dictionary<string, ScannerJournalEntry> _entries = new();
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
