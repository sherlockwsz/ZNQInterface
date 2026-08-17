namespace AxisControlHmi_test.Models
{
    public sealed class AxisStatus
    {
        public double Position { get; init; }
        public double Velocity { get; init; }
        public double Rpm { get; init; }
        public bool IsEnabled { get; init; }
        public bool HasFault { get; init; }
        public bool IsMoving { get; init; }
        public uint ErrorId { get; init; }
        public short MotionState { get; init; }
        public bool CommandRejected { get; init; }
        public uint RejectReason { get; init; }
        public bool IsPlcOnline { get; init; }
        public bool IsHeartbeatTimeout { get; init; }
        public bool WasHeartbeatInterrupted { get; init; }
    }
}
