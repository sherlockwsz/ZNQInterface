namespace ZNQInterface.Services.Watchdog
{
    /// <summary>
    /// PLC侧HMI看门狗相关ADS符号。
    ///
    /// 符号统一集中管理，
    /// PLC变量名称变化时只需要修改本文件。
    /// </summary>
    internal static class HmiWatchdogSymbols
    {
        /// <summary>
        /// PLC是否启用HMI看门狗。
        /// </summary>
        public const string UseWatchdog =
            "GVL_System.bUseHmiWatchdog";

        /// <summary>
        /// WPF周期递增并写入PLC的心跳计数。
        /// </summary>
        public const string Heartbeat =
            "GVL_HMI.Comm.udiHeartbeat";

        /// <summary>
        /// PLC是否已经检测到有效心跳。
        /// </summary>
        public const string Online =
            "GVL_HMI.Comm.bOnline";

        /// <summary>
        /// PLC是否检测到心跳超时。
        /// </summary>
        public const string Timeout =
            "GVL_HMI.Comm.bTimeout";

        /// <summary>
        /// PLC是否允许HMI控制设备。
        /// </summary>
        public const string ControlAllowed =
            "GVL_HMI.Comm.bControlAllowed";

        /// <summary>
        /// PLC通信恢复后是否仍在等待旧命令清零。
        /// </summary>
        public const string RequireEnableReset =
            "GVL_HMI.Comm.bRequireEnableReset";
    }
}