namespace ZNQInterface.Models.Axes
{
    /// <summary>
    /// 轴当前运动状态。
    /// 枚举数字最好与PLC中的E_AxisMotionState保持一致。
    /// </summary>
    public enum AxisMotionState
    {
        Unknown = 0,

        Disabled = 1,

        Standstill = 2,

        Homing = 3,

        MovingPositive = 4,

        MovingNegative = 5,

        InPosition = 6,

        Stopping = 7,

        Fault = 8
    }
}