namespace ZNQInterface.Models.Axes
{
    /// <summary>
    /// 手动调试页面当前选择的运动方式。
    /// 绝对和相对运动共用 PLC 的 Position 动态参数，
    /// 点动单独使用 Jog 动态参数。
    /// </summary>
    public enum ManualMotionMode
    {
        Absolute = 0,
        Relative = 1,
        Jog = 2
    }
}
