namespace ZNQInterface.Models.Axes
{
    /// <summary>
    /// PLC坐标方向与界面机械方向之间的对应关系。
    /// </summary>
    public enum AxisDirectionPolarity
    {
        /// <summary>
        /// PLC正方向对应界面定义的正机械方向。
        /// </summary>
        Normal = 1,

        /// <summary>
        /// PLC正方向对应界面定义的负机械方向。
        /// </summary>
        Reversed = -1
    }
}