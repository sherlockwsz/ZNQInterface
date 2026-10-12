#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZNQInterface.Services.Communication.Ads;
namespace ZNQInterface.Services.Processes
{
    public sealed class WorkpieceAdsReader
    {
        public const string Tracking = "GVL_WorkpieceTracking.";
        public const string Scanner = "GVL_Scanner.";
        private readonly IAdsConnectionService _ads;
        private readonly WorkpieceTrackingOptions _options;
        private long _epoch;
        public long Epoch => Interlocked.Read(ref _epoch);
        public bool IsConnected => _ads.IsConnected;
        public event Action? ConnectionChanged;
        public WorkpieceAdsReader(IAdsConnectionService ads, WorkpieceTrackingOptions options)
        {
            _ads = ads; _options = options;
            _ads.StateChanged += (_, __) => { Interlocked.Increment(ref _epoch); ConnectionChanged?.Invoke(); };
        }
        private static AdsReadRequest R<T>(string symbol) => AdsReadRequest.Create<T>(symbol);
        private void CheckEpoch(long epoch)
        { if (!IsConnected || epoch != Epoch) throw new IOException("ADS连接代际变化，本次快照作废。"); }
        public async Task EnsureSessionAsync(ulong session, long epoch, CancellationToken ct)
        {
            CheckEpoch(epoch);
            ulong actual = await _ads.ReadAsync<ulong>(Scanner + "uliSessionId", ct).ConfigureAwait(false);
            CheckEpoch(epoch);
            if (actual != session || session == 0) throw new IOException("PLC会话已变更，废弃旧会话命令。");
        }
        public async Task WriteAsync<T>(string symbol, T value, ulong session, long epoch, CancellationToken ct)
        {
            await EnsureSessionAsync(session, epoch, ct).ConfigureAwait(false);
            await _ads.WriteAsync(symbol, value, ct).ConfigureAwait(false);
            CheckEpoch(epoch);
        }
        private static IEnumerable<AdsReadRequest> IdentityFields(string p)
        {
            yield return R<ulong>(p + "uliSessionId"); yield return R<uint>(p + "udiRequestId");
            yield return R<short>(p + "nTray"); yield return R<short>(p + "nRow"); yield return R<int>(p + "nColumn");
            yield return R<PlcString128>(p + "sQRCode"); yield return R<bool>(p + "bQRCodeValid");
            yield return R<short>(p + "eCodeSource"); yield return R<uint>(p + "udiOriginalResultId");
            yield return R<uint>(p + "udiCorrectionResultId"); yield return R<short>(p + "eScanResult");
        }
        private static IEnumerable<AdsReadRequest> StationFields(string p)
        {
            yield return R<short>(p + "ePresence"); yield return R<bool>(p + "bTrusted"); yield return R<uint>(p + "udiDiagnostic");
            foreach (var field in IdentityFields(p + "stIdentity.")) yield return field;
        }
        private WorkpieceStation Station(IReadOnlyList<object> v, ref int i)
        {
            var presence = (WorkpiecePresence)Convert.ToInt16(v[i++]); bool trusted = Convert.ToBoolean(v[i++]); uint diagnostic = Convert.ToUInt32(v[i++]);
            var identity = new WorkpieceIdentity {
                SessionId = Convert.ToUInt64(v[i++]), RequestId = Convert.ToUInt32(v[i++]),
                Tray = Convert.ToInt16(v[i++]), Row = Convert.ToInt16(v[i++]), Column = Convert.ToInt32(v[i++]),
                QRCode = ((PlcString128)v[i++]).Decode(_options.GetEncoding()), QRCodeValid = Convert.ToBoolean(v[i++]),
                CodeSource = (ScannerCodeSource)Convert.ToInt16(v[i++]), OriginalResultId = Convert.ToUInt32(v[i++]),
                CorrectionResultId = Convert.ToUInt32(v[i++]), ScanResult = (ScannerResult)Convert.ToInt16(v[i++]) };
            return new WorkpieceStation { Presence = presence, Trusted = trusted, Diagnostic = diagnostic, Identity = identity };
        }
        public async Task<WorkpieceSnapshot> ReadStationsAsync(CancellationToken ct)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                long epoch = Epoch; CheckEpoch(epoch);
                var before = await _ads.ReadManyAsync(new[] { R<ulong>(Scanner + "uliSessionId"), R<uint>(Tracking + "udiSnapshotVersion") }, ct).ConfigureAwait(false);
                uint version = Convert.ToUInt32(before[1]); if ((version & 1) != 0) continue;
                var fields = new List<AdsReadRequest> { R<ulong>(Tracking + "uliSessionId"), R<bool>(Tracking + "bTrackingReady"),
                    R<uint>(Tracking + "udiDiagnostic"), R<uint>(Tracking + "udiIncomingRequestId"), R<uint>(Tracking + "udiOutgoingRequestId") };
                fields.AddRange(StationFields(Tracking + "stTransfer.")); fields.AddRange(StationFields(Tracking + "stAdjustment."));
                var v = await _ads.ReadManyAsync(fields, ct).ConfigureAwait(false);
                var after = await _ads.ReadManyAsync(new[] { R<uint>(Tracking + "udiSnapshotVersion"), R<ulong>(Scanner + "uliSessionId") }, ct).ConfigureAwait(false);
                CheckEpoch(epoch);
                if (version != Convert.ToUInt32(after[0]) || Convert.ToUInt64(before[0]) != Convert.ToUInt64(after[1])) continue;
                int i = 5;
                var snapshot = new WorkpieceSnapshot { Version = version, ConnectionEpoch = epoch,
                    SessionId = Convert.ToUInt64(v[0]), Diagnostic = Convert.ToUInt32(v[2]), IncomingRequestId = Convert.ToUInt32(v[3]), OutgoingRequestId = Convert.ToUInt32(v[4]),
                    Transfer = Station(v, ref i), Adjustment = Station(v, ref i) };
                if (!Convert.ToBoolean(v[1]) || snapshot.SessionId != Convert.ToUInt64(before[0]) || !snapshot.IsCoherent())
                    throw new InvalidDataException("PLC双工位快照字段不自洽或尚未初始化。");
                return snapshot;
            }
            throw new IOException("双工位快照连续变更，等待稳定重读。");
        }
        private static IEnumerable<AdsReadRequest> EventFields(string p)
        {
            yield return R<ulong>(p + "uliSessionId"); yield return R<uint>(p + "udiRequestId"); yield return R<uint>(p + "udiResultId");
            yield return R<uint>(p + "udiCorrectsResultId"); yield return R<PlcString128>(p + "sQRCode"); yield return R<short>(p + "eResult");
            yield return R<short>(p + "eCodeSource"); yield return R<bool>(p + "bQRCodeValid"); yield return R<ushort>(p + "nAttemptCount");
            yield return R<short>(p + "eReason"); yield return R<uint>(p + "udiTransportErrorId"); yield return R<uint>(p + "udiDeviceErrorId");
            yield return R<short>(p + "stContext.nTray"); yield return R<short>(p + "stContext.nRow"); yield return R<int>(p + "stContext.nColumn");
        }
        private ScannerEvent Event(IReadOnlyList<object> v, ref int i) => new ScannerEvent {
            SessionId = Convert.ToUInt64(v[i++]), RequestId = Convert.ToUInt32(v[i++]), ResultId = Convert.ToUInt32(v[i++]),
            CorrectsResultId = Convert.ToUInt32(v[i++]), QRCode = ((PlcString128)v[i++]).Decode(_options.GetEncoding()), Result = (ScannerResult)Convert.ToInt16(v[i++]),
            CodeSource = (ScannerCodeSource)Convert.ToInt16(v[i++]), QRCodeValid = Convert.ToBoolean(v[i++]), AttemptCount = Convert.ToUInt16(v[i++]), Reason = Convert.ToInt16(v[i++]),
            TransportErrorId = Convert.ToUInt32(v[i++]), DeviceErrorId = Convert.ToUInt32(v[i++]), Tray = Convert.ToInt16(v[i++]), Row = Convert.ToInt16(v[i++]), Column = Convert.ToInt32(v[i++]) };
        public async Task<ScannerQueueSnapshot> ReadQueueAsync(CancellationToken ct)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                long epoch = Epoch; CheckEpoch(epoch);
                var h = await _ads.ReadManyAsync(new[] { R<ulong>(Scanner + "uliSessionId"), R<uint>(Scanner + "udiQueueVersion"), R<ushort>(Scanner + "uiHeadIndex"),
                    R<ushort>(Scanner + "uiCount"), R<ushort>(Scanner + "uiReserved"), R<uint>(Scanner + "udiLastPublishedResultId") }, ct).ConfigureAwait(false);
                ulong session = Convert.ToUInt64(h[0]); uint version = Convert.ToUInt32(h[1]); int headIndex = Convert.ToUInt16(h[2]), count = Convert.ToUInt16(h[3]);
                ushort reserved = Convert.ToUInt16(h[4]); uint last = Convert.ToUInt32(h[5]);
                if ((version & 1) != 0) continue;
                if (session == 0 || headIndex < 1 || headIndex > 64 || count > 64 || count + reserved > 64) throw new InvalidDataException("PLC扫码FIFO头信息无效。");
                ScannerEvent? head = null; var records = new List<ScannerEvent>(count);
                if (count > 0)
                {
                    var hv = await _ads.ReadManyAsync(EventFields(Scanner + "stHead.").ToArray(), ct).ConfigureAwait(false); int hi = 0; head = Event(hv, ref hi);
                    // 30 * 15 = 450 leaves, below the existing 500-leaf limit.
                    for (int start = 0; start < count; start += 30)
                    {
                        int length = Math.Min(30, count - start); var fields = new List<AdsReadRequest>();
                        for (int offset = start; offset < start + length; offset++) fields.AddRange(EventFields(Scanner + $"aResults[{((headIndex - 1 + offset) % 64) + 1}]."));
                        var v = await _ads.ReadManyAsync(fields, ct).ConfigureAwait(false); int i = 0;
                        for (int n = 0; n < length; n++) records.Add(Event(v, ref i));
                    }
                }
                var after = await _ads.ReadManyAsync(new[] { R<uint>(Scanner + "udiQueueVersion"), R<ulong>(Scanner + "uliSessionId") }, ct).ConfigureAwait(false);
                CheckEpoch(epoch);
                if (version != Convert.ToUInt32(after[0]) || session != Convert.ToUInt64(after[1])) continue;
                if (records.Any(e => e.SessionId != session || e.RequestId == 0 || e.ResultId == 0 || e.ResultId > last)
                    || records.Select(e => e.ResultId).Distinct().Count() != records.Count || (head != null && head != records[0])
                    || !records.Select(e => e.ResultId).SequenceEqual(records.Select(e => e.ResultId).OrderBy(id => id)))
                    throw new InvalidDataException("PLC扫码事件不自洽，保留队列不ACK。");
                return new ScannerQueueSnapshot { SessionId = session, ConnectionEpoch = epoch, Version = version,
                    LastPublished = last, Reserved = reserved, Head = head, Events = records };
            }
            throw new IOException("扫码队列快照变更，等待稳定重读。");
        }
        public async Task<ScannerProtocolState> ReadProtocolAsync(ulong session, long epoch, CancellationToken ct)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                await EnsureSessionAsync(session, epoch, ct).ConfigureAwait(false);
                var before = await _ads.ReadManyAsync(new[] { R<uint>(Scanner + "udiManualAckSeq"), R<uint>(Scanner + "udiAckProcessedSeq") }, ct).ConfigureAwait(false);
                var v = await _ads.ReadManyAsync(new[] { R<uint>(Scanner + "udiManualSubmitSeq"), R<uint>(Scanner + "udiManualAckSeq"), R<short>(Scanner + "eManualAck"),
                    R<uint>(Scanner + "udiAckSubmitSeq"), R<uint>(Scanner + "udiAckProcessedSeq"), R<short>(Scanner + "eAck") }, ct).ConfigureAwait(false);
                var after = await _ads.ReadManyAsync(new[] { R<uint>(Scanner + "udiManualAckSeq"), R<uint>(Scanner + "udiAckProcessedSeq") }, ct).ConfigureAwait(false);
                await EnsureSessionAsync(session, epoch, ct).ConfigureAwait(false);
                if (Convert.ToUInt32(before[0]) != Convert.ToUInt32(v[1]) || Convert.ToUInt32(v[1]) != Convert.ToUInt32(after[0])
                    || Convert.ToUInt32(before[1]) != Convert.ToUInt32(v[4]) || Convert.ToUInt32(v[4]) != Convert.ToUInt32(after[1])) continue;
                if (!Enum.IsDefined(typeof(ScannerManualAck), (short)Convert.ToInt16(v[2]))
                    || !Enum.IsDefined(typeof(ScannerQueueAck), (short)Convert.ToInt16(v[5])))
                    throw new InvalidDataException("PLC返回未知ACK枚举；禁止确认或重复提交。");
                return new ScannerProtocolState { SessionId = session, ManualSubmit = Convert.ToUInt32(v[0]), ManualAckSeq = Convert.ToUInt32(v[1]),
                    ManualAck = (ScannerManualAck)Convert.ToInt16(v[2]), AckSubmit = Convert.ToUInt32(v[3]), AckProcessed = Convert.ToUInt32(v[4]), Ack = (ScannerQueueAck)Convert.ToInt16(v[5]) };
            }
            throw new IOException("Scanner ACK提交序号读取中变化，等待稳定重读。");
        }
    }
}
