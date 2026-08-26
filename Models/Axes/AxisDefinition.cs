namespace ZNQInterface.Models.Axes
{
    /// <summary>
    /// 一根轴的固定配置信息。
    /// 这些信息在设备运行过程中通常不会改变。
    /// </summary>
    public class AxisDefinition
    {
        /// <summary>
        /// 上位机内部轴ID。
        /// </summary>
        public AxisId AxisId { get; init; }

        /// <summary>
        /// PLC中的实际轴号。
        /// 不建议默认认为它一定等于AxisId的数字。
        /// </summary>
        public int PlcAxisNumber { get; init; }
        /// <summary>
        /// 该轴所属功能组。
        /// </summary>
        public AxisGroupId GroupId { get; init; }

        /// <summary>
        /// 界面显示名称。
        /// </summary>
        public string DisplayName { get; init; }

        /// <summary>
        /// 轴类型。
        /// </summary>
        public AxisType AxisType { get; init; }

        /// <summary>
        /// 正向运动时显示的文字。
        /// </summary>
        public string PositiveDirectionText { get; init; }

        /// <summary>
        /// 负向运动时显示的文字。
        /// </summary>
        public string NegativeDirectionText { get; init; }

        /// <summary>
        /// 轴使用的单位。
        /// </summary>
        public AxisUnitSet Units { get; init; }
    }
}