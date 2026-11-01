#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZNQInterface.Models.Product;
using ZNQInterface.Services.Communication.Ads;
using ZNQInterface.Services.Processes;

namespace ZNQInterface.Services.Product
{
    public sealed record ProductCompletion
    {
        public ulong SessionId { get; init; }
        public uint RequestId { get; init; }
        public uint CompleteSeq { get; init; }
        public string QRCode { get; init; } = "";
        public ScannerCodeSource CodeSource { get; init; }
        public uint OriginalResultId { get; init; }
        public uint CorrectionResultId { get; init; }
        public double? InitialX { get; init; }
        public double? InitialY { get; init; }
        public double? InitialCoaxiality { get; init; }
        public double? FinalX { get; init; }
        public double? FinalY { get; init; }
        public double? FinalCoaxiality { get; init; }
        public uint AdjustCount { get; init; }
        public double AdjustmentSeconds { get; init; }
        public bool Qualified { get; init; }
        public bool VisionFault { get; init; }
        public bool VisionTimeout { get; init; }
        public short VisionError { get; init; }
        public bool OutOfRange { get; init; }
        public string Key => $"{SessionId}:{RequestId}";

        // Only NG text mapping; no tolerance/quality calculation.
        public string NgReason => Qualified ? "" : VisionTimeout || VisionError == 20 ? "视觉超时"
            : VisionError == 10 ? "视觉服务离线" : VisionError == 30 ? "视觉结果无效"
            : VisionError == 40 ? "视觉服务异常" : VisionError == 60 ? "视觉未检测到目标"
            : VisionFault || VisionError != 0 ? "原因未提供"
            : FinalCoaxiality.HasValue ? (OutOfRange ? "调整范围受限且最终同轴度超差" : "同轴度超差")
            : "原因未提供";
        public ProductDataItem ToItem(DateTime scanUtc, DateTime receivedLocal) => new ProductDataItem
        {
            SessionId = SessionId, RequestId = RequestId, CompleteSeq = CompleteSeq,
            DetectionTime = scanUtc.ToLocalTime(), CompletionTime = receivedLocal, ProductNumber = QRCode,
            InitialX = InitialX, InitialY = InitialY, InitialCoaxiality = InitialCoaxiality,
            FinalX = FinalX, FinalY = FinalY, Coaxiality = FinalCoaxiality,
            TotalAdjustCount = AdjustCount, AdjustmentSeconds = AdjustmentSeconds,
            IsQualified = Qualified, NgReason = NgReason
        };
    }
    // Existing scanner/tracking ADS reads and all PLC write paths remain untouched.
    public sealed class ProductCompletionReader
    {
        private const string Root = "GVL_ProductData.";
        private readonly IAdsConnectionService _ads;
        private readonly WorkpieceAdsReader _connection;
        private readonly WorkpieceTrackingOptions _options;
        public uint Diagnostic { get; private set; }
        public ProductCompletionReader(IAdsConnectionService ads, WorkpieceAdsReader connection, WorkpieceTrackingOptions options)
        { _ads = ads; _connection = connection; _options = options; }
        private static AdsReadRequest R<T>(string name) => AdsReadRequest.Create<T>(Root + "stLastCompleted." + name);
        private void Check(long epoch)
        { if (!_ads.IsConnected || _connection.Epoch != epoch) throw new IOException("产品读取期间ADS连接变化，等待重读。"); }
        public async Task<ProductCompletion?> ReadAsync(CancellationToken ct)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                long epoch = _connection.Epoch; Check(epoch);
                var before = await _ads.ReadManyAsync(new[] {
                    AdsReadRequest.Create<uint>(Root + "udiSnapshotVersion"),
                    AdsReadRequest.Create<ulong>(WorkpieceAdsReader.Scanner + "uliSessionId") }, ct).ConfigureAwait(false);
                uint version = Convert.ToUInt32(before[0]);
                if ((version & 1) != 0) continue;
                var v = await _ads.ReadManyAsync(new[] {
                    R<ulong>("uliSessionId"), R<uint>("udiRequestId"), R<PlcString128>("sQRCode"),
                    R<bool>("bQRCodeValid"), R<bool>("bTrusted"), R<short>("eCodeSource"),
                    R<uint>("udiOriginalResultId"), R<uint>("udiCorrectionResultId"),
                    R<bool>("bInitialMeasurementValid"), R<double>("fInitialDeltaX"), R<double>("fInitialDeltaY"), R<double>("fInitialCoaxiality"),
                    R<bool>("bFinalMeasurementValid"), R<double>("fFinalDeltaX"), R<double>("fFinalDeltaY"), R<double>("fFinalCoaxiality"),
                    R<uint>("nTotalAdjustCount"), R<double>("fAdjustmentSeconds"), R<bool>("bAdjustmentQualified"),
                    R<bool>("bVisionFault"), R<bool>("bVisionTimeout"), R<short>("eVisionError"),
                    R<bool>("bAdjustmentOutOfRange"), R<uint>("udiCompleteSeq") }, ct).ConfigureAwait(false);
                var after = await _ads.ReadManyAsync(new[] {
                    AdsReadRequest.Create<uint>(Root + "udiSnapshotVersion"),
                    AdsReadRequest.Create<ulong>(WorkpieceAdsReader.Scanner + "uliSessionId"),
                    AdsReadRequest.Create<uint>(Root + "udiDiagnostic") }, ct).ConfigureAwait(false);
                Check(epoch);
                if (version != Convert.ToUInt32(after[0]) || Convert.ToUInt64(before[1]) != Convert.ToUInt64(after[1])) continue;
                // A later rejected workpiece must not hide the previous valid held snapshot.
                Diagnostic = Convert.ToUInt32(after[2]);
                if (Convert.ToUInt32(v[23]) == 0) return null;
                if (Convert.ToUInt64(v[0]) != Convert.ToUInt64(after[1])) return null;
                string code = ((PlcString128)v[2]).Decode(_options.GetEncoding());
                var source = (ScannerCodeSource)Convert.ToInt16(v[5]);
                if (Convert.ToUInt64(v[0]) == 0 || Convert.ToUInt32(v[1]) == 0 || !Convert.ToBoolean(v[3])
                    || !Convert.ToBoolean(v[4]) || string.IsNullOrWhiteSpace(code) || Convert.ToUInt32(v[6]) == 0
                    || (source != ScannerCodeSource.Tcp && source != ScannerCodeSource.Manual)
                    || (source == ScannerCodeSource.Manual && Convert.ToUInt32(v[7]) == 0))
                    throw new InvalidDataException("已完成产品身份无效，未保存。");
                bool initial = Convert.ToBoolean(v[8]), final = Convert.ToBoolean(v[12]);
                double seconds = Convert.ToDouble(v[17]);
                if (!double.IsFinite(seconds) || seconds < 0
                    || (initial && v.Skip(9).Take(3).Any(x => !double.IsFinite(Convert.ToDouble(x))))
                    || (final && v.Skip(13).Take(3).Any(x => !double.IsFinite(Convert.ToDouble(x)))))
                    throw new InvalidDataException("产品测量数值非法，未保存。");
                return new ProductCompletion {
                    SessionId = Convert.ToUInt64(v[0]), RequestId = Convert.ToUInt32(v[1]), QRCode = code,
                    CodeSource = source, OriginalResultId = Convert.ToUInt32(v[6]), CorrectionResultId = Convert.ToUInt32(v[7]),
                    InitialX = initial ? Convert.ToDouble(v[9]) : null, InitialY = initial ? Convert.ToDouble(v[10]) : null,
                    InitialCoaxiality = initial ? Convert.ToDouble(v[11]) : null,
                    FinalX = final ? Convert.ToDouble(v[13]) : null, FinalY = final ? Convert.ToDouble(v[14]) : null,
                    FinalCoaxiality = final ? Convert.ToDouble(v[15]) : null,
                    AdjustCount = Convert.ToUInt32(v[16]), AdjustmentSeconds = seconds, Qualified = Convert.ToBoolean(v[18]),
                    VisionFault = Convert.ToBoolean(v[19]), VisionTimeout = Convert.ToBoolean(v[20]),
                    VisionError = Convert.ToInt16(v[21]), OutOfRange = Convert.ToBoolean(v[22]), CompleteSeq = Convert.ToUInt32(v[23]) };
            }
            throw new IOException("完成快照持续变化，等待稳定重读。");
        }
    }
}
