#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
namespace ZNQInterface.Services.Processes
{
    public enum WorkpiecePresence : short { Unknown = 0, Empty = 1, Occupied = 2 }
    public enum ScannerResult : short { ScanSuccess = 1, ScanSkipped = 2, ScanAborted = 3, MotionFault = 4 }
    public enum ScannerCodeSource : short { None = 0, Tcp = 1, Manual = 2 }
    public enum ScannerManualAck : short
    { None = 0, Accepted = 1, EmptyCode = 2, SessionMismatch = 3, RecordNotFound = 4, NotModifiable = 5, QueueFull = 6, DuplicateOrOldSequence = 7, SequenceExhausted = 8 }
    public enum ScannerQueueAck : short
    { None = 0, Accepted = 1, SessionMismatch = 2, NotHead = 3, DuplicateOrOld = 4, NoRecord = 5 }
    // STRING(128) is exactly 129 bytes. No PLC ST alignment is assumed.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public sealed class PlcString128
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 129, ArraySubType = UnmanagedType.U1)]
        public byte[] Bytes = new byte[129];
        public string Decode(Encoding encoding)
        {
            if (Bytes == null || Bytes.Length != 129) throw new InvalidDataException("STRING(128)字节数错误。");
            int end = Array.IndexOf(Bytes, (byte)0);
            if (end < 0) throw new InvalidDataException("二维码缺少NUL结束符。");
            return encoding.GetString(Bytes, 0, end);
        }
        public static PlcString128 Encode(string code, Encoding encoding)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Length > 128 || code.IndexOf('\0') >= 0)
                throw new ArgumentException("请输入非空、最多128字符且不含NUL的二维码。");
            byte[] bytes = encoding.GetBytes(code);
            if (bytes.Length > 128) throw new ArgumentException("该二维码编码后超过PLC STRING(128)的128字节容量。");
            var value = new PlcString128(); Array.Copy(bytes, value.Bytes, bytes.Length); return value;
        }
    }
    public sealed record WorkpieceIdentity
    {
        public ulong SessionId { get; init; }
        public uint RequestId { get; init; }
        public short Tray { get; init; }
        public short Row { get; init; }
        public int Column { get; init; }
        public string QRCode { get; init; } = "";
        public bool QRCodeValid { get; init; }
        public ScannerCodeSource CodeSource { get; init; }
        public uint OriginalResultId { get; init; }
        public uint CorrectionResultId { get; init; }
        public ScannerResult ScanResult { get; init; }
    }
    public sealed record WorkpieceStation
    {
        public WorkpiecePresence Presence { get; init; }
        public bool Trusted { get; init; }
        public uint Diagnostic { get; init; }
        public WorkpieceIdentity Identity { get; init; } = new WorkpieceIdentity();
        public string DisplayCode => !Trusted || Presence == WorkpiecePresence.Unknown ? "待确认"
            : Presence == WorkpiecePresence.Empty ? "--"
            : Presence == WorkpiecePresence.Occupied ? (Identity.QRCodeValid ? Identity.QRCode : "未识别") : "待确认";
        public bool IsManaged(ulong session, uint request) => Trusted && Presence == WorkpiecePresence.Occupied
            && Identity.SessionId == session && Identity.RequestId == request && request != 0;
        public bool IsCoherent(ulong session)
        {
            if (Presence == WorkpiecePresence.Unknown) return !Trusted;
            if (Presence == WorkpiecePresence.Empty)
                return Trusted && Identity.RequestId == 0 && !Identity.QRCodeValid && Identity.QRCode.Length == 0;
            if (Presence != WorkpiecePresence.Occupied || !Trusted || Identity.SessionId != session
                || Identity.RequestId == 0 || Identity.OriginalResultId == 0) return false;
            return Identity.QRCodeValid
                ? Identity.QRCode.Length > 0 && (Identity.CodeSource == ScannerCodeSource.Tcp || Identity.CodeSource == ScannerCodeSource.Manual)
                : Identity.QRCode.Length == 0 && Identity.CodeSource == ScannerCodeSource.None;
        }
    }
    public sealed record WorkpieceSnapshot
    {
        public ulong SessionId { get; init; }
        public uint Version { get; init; }
        public long ConnectionEpoch { get; init; }
        public uint Diagnostic { get; init; }
        public uint IncomingRequestId { get; init; }
        public uint OutgoingRequestId { get; init; }
        public WorkpieceStation Transfer { get; init; } = new WorkpieceStation();
        public WorkpieceStation Adjustment { get; init; } = new WorkpieceStation();
        public bool IsManaged(ulong session, uint request) => session == SessionId && request != 0
            && (Transfer.IsManaged(session, request) || Adjustment.IsManaged(session, request) || IncomingRequestId == request || OutgoingRequestId == request);
        public bool IsCoherent()
        {
            if (SessionId == 0 || (Version & 1) != 0 || !Transfer.IsCoherent(SessionId) || !Adjustment.IsCoherent(SessionId)) return false;
            var ids = new[] { Transfer.Trusted ? Transfer.Identity.RequestId : 0, Adjustment.Trusted ? Adjustment.Identity.RequestId : 0,
                IncomingRequestId, OutgoingRequestId }.Where(id => id != 0).ToArray();
            return ids.Length == ids.Distinct().Count();
        }
    }
    public sealed record ScannerEvent
    {
        public ulong SessionId { get; init; }
        public uint RequestId { get; init; }
        public uint ResultId { get; init; }
        public uint CorrectsResultId { get; init; }
        public string QRCode { get; init; } = "";
        public ScannerResult Result { get; init; }
        public ScannerCodeSource CodeSource { get; init; }
        public bool QRCodeValid { get; init; }
        public ushort AttemptCount { get; init; }
        public short Reason { get; init; }
        public uint TransportErrorId { get; init; }
        public uint DeviceErrorId { get; init; }
        public short Tray { get; init; }
        public short Row { get; init; }
        public int Column { get; init; }
        public string Key => $"{SessionId}:{ResultId}";
        public bool IsCorrection => CorrectsResultId != 0 || CodeSource == ScannerCodeSource.Manual;
        public bool IsOriginalSkipped => !IsCorrection && Result == ScannerResult.ScanSkipped && !QRCodeValid;
    }
    public sealed record ScannerQueueSnapshot
    {
        public ulong SessionId { get; init; }
        public long ConnectionEpoch { get; init; }
        public uint Version { get; init; }
        public uint LastPublished { get; init; }
        public ushort Reserved { get; init; }
        public ScannerEvent? Head { get; init; }
        public IReadOnlyList<ScannerEvent> Events { get; init; } = Array.Empty<ScannerEvent>();
        public bool NearFull => Events.Count + Reserved >= 62;
    }
    public sealed record ScannerProtocolState
    {
        public ulong SessionId { get; init; }
        public uint ManualSubmit { get; init; }
        public uint ManualAckSeq { get; init; }
        public ScannerManualAck ManualAck { get; init; }
        public uint AckSubmit { get; init; }
        public uint AckProcessed { get; init; }
        public ScannerQueueAck Ack { get; init; }
    }
}
