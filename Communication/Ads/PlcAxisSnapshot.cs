namespace ZNQInterface.Communication.Ads
{
    /// <summary>
    /// 一次轮询得到的 Axis1 快照。
    /// 后台线程先完整读取快照，再一次性投递到 UI 线程，
    /// 避免 WPF 在一次刷新中看到新旧数据混合。
    /// </summary>
    internal sealed class PlcAxisSnapshot
    {
        public double PositionVelocity { get; init; }
        public double JogVelocity { get; init; }

        public double SetPosition { get; init; }
        public double ActualPosition { get; init; }
        public double TargetPosition { get; init; }
        public double SetVelocity { get; init; }
        public double ActualVelocity { get; init; }
        public double SetAcceleration { get; init; }
        public double ActualAcceleration { get; init; }
        public double FollowingError { get; init; }
        public double SetTorque { get; init; }
        public double ActualTorque { get; init; }
        public double ActualRpm { get; init; }

        public bool Ready { get; init; }
        public bool PowerStatus { get; init; }
        public bool Homed { get; init; }
        public bool Busy { get; init; }
        public bool Active { get; init; }
        public bool Done { get; init; }
        public bool CommandAborted { get; init; }
        public bool CommandRejected { get; init; }
        public short MotionState { get; init; }
        /// <summary>
        /// PLC的E_AxisMotionDirection默认底层类型为INT，
        /// TwinCAT INT对应C# short。
        /// </summary>
        public short MotionDirection { get; init; }
        public bool SoftLimitEnabled { get; init; }
        public bool SoftLimitReady { get; init; }
        public bool SoftLimitPositive { get; init; }
        public bool SoftLimitNegative { get; init; }

        public bool Error { get; init; }
        public uint ErrorId { get; init; }
        public short ErrorSource { get; init; }
        public bool Warning { get; init; }
        public uint WarningId { get; init; }
        public short RejectReason { get; init; }

        public double SoftwareLimitNegative { get; init; }
        public double SoftwareLimitPositive { get; init; }
    }
}
