namespace ZNQInterface.ViewModels.Components.MaterialSlots
{
    /// <summary>
    /// 阻尼器所在物理位置的内容状态。
    ///
    /// 除Unknown外，其余数值必须与PLC的
    /// E_DamperSlotState枚举完全一致。
    /// </summary>
    public enum MaterialSlotState : short
    {
        /// <summary>
        /// WPF专用状态：ADS未连接、读取失败或PLC返回未知值。
        /// PLC不会写入该状态。
        /// </summary>
        Unknown = -1,

        /// <summary>该物理位置没有阻尼器。</summary>
        Empty = 0,

        /// <summary>该位置放有尚未调整的阻尼器。</summary>
        Unadjusted = 10,

        /// <summary>阻尼器位于转台备料位，等待调整。</summary>
        PendingAdjustment = 20,

        /// <summary>阻尼器位于调整位，正在调整。</summary>
        Adjusting = 30,

        /// <summary>调整结果合格。</summary>
        Qualified = 40,

        /// <summary>调整结果不合格。</summary>
        Unqualified = 50
    }
}