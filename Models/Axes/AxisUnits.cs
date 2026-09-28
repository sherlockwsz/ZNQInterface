namespace ZNQInterface.Models.Axes
{
    /// <summary>
    /// 项目中常用的轴单位。
    /// 避免在14根轴中重复填写字符串。
    /// </summary>
    public static class AxisUnits
    {
        /// <summary>
        /// 直线运动轴单位。
        /// </summary>
        public static AxisUnitSet Linear { get; } =
            new AxisUnitSet(
                position: "mm",
                velocity: "mm/s",
                acceleration: "mm/s²",
                torque: "%");

        /// <summary>
        /// 旋转运动轴单位。
        /// </summary>
        public static AxisUnitSet Rotary { get; } =
            new AxisUnitSet(
                position: "°",
                velocity: "°/s",
                acceleration: "°/s²",
                torque: "%");
    }
}