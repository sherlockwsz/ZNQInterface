namespace ZNQInterface.ViewModels.Components.Warehouse
{
    /// <summary>
    /// 五层料盘状态。
    ///
    /// 除Unknown外，数值与PLC的E_Tray枚举一致。
    /// </summary>
    public enum WarehouseLevelState : short
    {
        /// <summary>WPF专用：ADS未连接或读取失败。</summary>
        Unknown = -1,

        /// <summary>当前位置没有料盘。</summary>
        Empty = 0,

        /// <summary>料盘等待检测。</summary>
        WaitingInspection = 1,

        /// <summary>当前料盘正在检测/调整。</summary>
        Inspecting = 2,

        /// <summary>当前料盘已经检测完成。</summary>
        Inspected = 3
    }
}