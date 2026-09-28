namespace ZNQInterface.Models.Device
{
    public sealed class DeviceControlState
    {
        public bool IsControlAuto { get; init; }

        public bool IsAutoStopping { get; init; }

        public bool IsAutoStopDone { get; init; }

        public bool IsAutoStopError { get; init; }

        public bool IsAutoStopTimeout { get; init; }

        public ushort AutoStopErrorAxis { get; init; }

        public uint AutoStopErrorId { get; init; }
    }
}