#nullable enable
using System;
using System.IO;
using System.Text;
using System.Text.Json;
namespace ZNQInterface.Services.Processes
{
    public sealed class WorkpieceTrackingOptions
    {
        public int ManualEntrySeconds { get; set; } = 10;
        public int ProtocolGraceMilliseconds { get; set; } = 2000;
        public string BarcodeEncoding { get; set; } = "utf-8";
        public Encoding GetEncoding() => Encoding.GetEncoding(BarcodeEncoding,
            EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        public static WorkpieceTrackingOptions Load(string path)
        {
            var value = File.Exists(path) ? JsonSerializer.Deserialize<WorkpieceTrackingOptions>(File.ReadAllText(path)) : new WorkpieceTrackingOptions();
            if (value == null || value.ManualEntrySeconds < 1 || value.ManualEntrySeconds > 60
                || value.ProtocolGraceMilliseconds < 500 || value.ProtocolGraceMilliseconds > 5000)
                throw new InvalidDataException("工件补录配置无效：窗口1–60秒，协议宽限500–5000毫秒。");
            value.GetEncoding(); return value;
        }
    }
}
