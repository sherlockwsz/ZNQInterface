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
        public bool IsControlPrepare { get; init; }

        public bool IsPreparationBusy { get; init; }

        public bool IsPreparationDone { get; init; }

        public bool IsPreparationError { get; init; }

        public bool IsPreparationTimeout { get; init; }

        public ushort PreparationErrorAxis { get; init; }

        public uint PreparationErrorCode { get; init; }

        public MachineMode MachineMode { get; init; }

        public bool IsMachineFault { get; init; }

        public bool IsMachineFaultResetBlocked { get; init; }

        public MachineFaultSource MachineFaultSource { get; init; }

        public ushort MachineFaultAxis { get; init; }

        public uint MachineFaultCode { get; init; }

    }
}