namespace AxisControlHmi_test.Services
{
    internal static class AdsSymbolNames
    {
        internal const string Enable = "GVL_HMI.AxisCmd.bEnable";
        internal const string Reset = "GVL_HMI.AxisCmd.bReset";
        internal const string Stop = "GVL_HMI.AxisCmd.bStop";
        internal const string MoveRelative = "GVL_HMI.AxisCmd.bMoveRel";
        internal const string MoveAbsolute = "GVL_HMI.AxisCmd.bMoveAbs";
        internal const string JogPositive = "GVL_HMI.AxisCmd.bJogPos";
        internal const string JogNegative = "GVL_HMI.AxisCmd.bJogNeg";

        internal const string RelativeDistance = "GVL_HMI.AxisSet.fRelDistance";
        internal const string AbsolutePosition = "GVL_HMI.AxisSet.fAbsPosition";

        internal const string PowerStatus = "GVL_HMI.AxisSts.bPowerStatus";
        internal const string Busy = "GVL_HMI.AxisSts.bBusy";
        internal const string Error = "GVL_HMI.AxisSts.bError";
        internal const string ErrorId = "GVL_HMI.AxisSts.nErrorID";
        internal const string MotionState = "GVL_HMI.AxisSts.nMotionState";
        internal const string ActualPosition = "GVL_HMI.AxisSts.fActPos";
        internal const string ActualVelocity = "GVL_HMI.AxisSts.fActVelo";
        internal const string ActualRpm = "GVL_HMI.AxisSts.fActRpm";
        internal const string CommandRejected = "GVL_HMI.AxisSts.bCmdRejected";
        internal const string RejectReason = "GVL_HMI.AxisSts.nRejectReason";

        internal const string Heartbeat = "GVL_HMI.Comm.udiHeartbeat";
    }
}
