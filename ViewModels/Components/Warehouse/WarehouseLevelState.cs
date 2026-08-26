namespace ZNQInterface.ViewModels.Components.Warehouse
{
    /// <summary>
    /// 单层料仓的当前状态。
    /// 数值后续可以直接与PLC状态值对应。
    /// </summary>
    public enum WarehouseLevelState
    {
        /// <summary>
        /// 当前料仓层为空。
        /// </summary>
        Empty = 0,

        /// <summary>
        /// 当前层存在产品，但尚未调整。
        /// </summary>
        Unadjusted = 1,

        /// <summary>
        /// 当前层产品正在调整。
        /// </summary>
        Adjusting = 2,

        /// <summary>
        /// 当前层产品已经完成调整。
        /// </summary>
        Adjusted = 3,

        /// <summary>
        /// 当前料仓层出现异常。
        /// 如果PLC暂时没有该状态，也可以先保留。
        /// </summary>
        Fault = 4
    }
}