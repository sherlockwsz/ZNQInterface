namespace ZNQInterface.ViewModels.Components.MaterialSlots
{
    /// <summary>
    /// 料位当前状态。
    /// 后续可以与PLC传递的状态数值对应。
    /// </summary>
    public enum MaterialSlotState
    {
        /// <summary>
        /// 料位为空。
        /// </summary>
        Empty = 0,

        /// <summary>
        /// 已放置产品，等待检测。
        /// </summary>
        Waiting = 1,

        /// <summary>
        /// 产品正在检测。
        /// </summary>
        Inspecting = 2,

        /// <summary>
        /// 产品检测合格。
        /// </summary>
        Qualified = 3,

        /// <summary>
        /// 产品检测不合格。
        /// </summary>
        Unqualified = 4,

        /// <summary>
        /// 产品正在人工调整。
        /// </summary>
        ManualAdjusting = 5,

        /// <summary>
        /// 料位发生异常。
        /// </summary>
        Fault = 6
    }
}