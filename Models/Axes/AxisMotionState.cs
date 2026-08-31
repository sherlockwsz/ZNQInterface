namespace ZNQInterface.Models.Axes
{
    /// <summary>
    /// 轴当前运动状态。
    /// 枚举名称和数值与 PLC 中 E_AxisMotionState 完全一致。
    /// ADS 读取 PLC 的 INT 后可以直接转换为本枚举。
    /// </summary>
    public enum AxisMotionState
    {
        Undefined = 0,
        Disabled = 10,
        NotReady = 20,
        Standstill = 30,
        Homing = 40,
        Moving = 50,
        Jogging = 60,
        Stopping = 70,
        Resetting = 80,
        Error = 90,
        TorqueRunning = 100
    }
}
