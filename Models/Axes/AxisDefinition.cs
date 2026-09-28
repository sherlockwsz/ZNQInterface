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
        /// 当前项目强制等于AxisId数值和PLC数组下标。
        /// </summary>
        public int PlcAxisNumber { get; init; }

        /// <summary>
        /// 按设备设计，该下标是否应该绑定有效NC轴。
        /// 运行时还会与PLC AxisLimits[i].bConfigured交叉校验。
        /// </summary>
        public bool IsExpectedConfigured { get; init; }
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
        /// PLC坐标正负方向与界面机械方向的对应关系。
        /// 默认不反向。
        /// </summary>
        public AxisDirectionPolarity DirectionPolarity
        {
            get;
            init;
        } = AxisDirectionPolarity.Normal;

        /// <summary>
        /// 当前轴是否需要进行机械方向反映射。
        /// </summary>
        public bool IsMotionDirectionReversed =>
            DirectionPolarity ==
            AxisDirectionPolarity.Reversed;


        /// <summary>
        /// 界面定义的正机械方向文字。
        /// 该方向不一定等于PLC坐标正方向。
        /// </summary>

        public string PositiveDirectionText { get; init; }

        /// <summary>
        /// 界面定义的负机械方向文字。
        /// 该方向不一定等于PLC坐标负方向。
        /// </summary>
        public string NegativeDirectionText { get; init; }

        /// <summary>
        /// 轴使用的单位。
        /// </summary>
        public AxisUnitSet Units { get; init; }

        /// <summary>
        /// PLC 中该轴运行接口的 ADS 符号前缀。
        /// 例如：GVL_AxisRuntime.Axes[1]。
        /// </summary>
        public string AdsSymbolPrefix { get; init; }

        /// <summary>
        /// PLC 中该轴限位配置的 ADS 符号前缀。
        /// 只读，用于在界面显示实际软件限位范围。
        /// </summary>
        public string AdsLimitSymbolPrefix { get; init; }

        /// <summary>
        /// 当前轴是否允许接入PLC轴控制。
        ///
        /// DamperGripper虽然保留数组地址和编号7，
        /// 但当前没有绑定NC轴，因此返回false。
        /// </summary>
        public bool IsAdsMapped =>
            IsExpectedConfigured &&
            !string.IsNullOrWhiteSpace(AdsSymbolPrefix);
    }
}
