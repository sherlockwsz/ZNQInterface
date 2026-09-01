namespace ZNQInterface.Communication.Ads
{
    /// <summary>
    /// 集中保存WPF与PLC看门狗之间使用的ADS符号。
    ///
    /// PLC变量名称变化时只需要修改这里，
    /// 避免符号字符串散落在服务和ViewModel中。
    /// </summary>
    internal static class AdsHmiSymbols
    {
        /// <summary>
        /// WPF周期递增，PLC读取。
        /// </summary>
        public const string Heartbeat =
            "GVL_HMI.Comm.udiHeartbeat";

        /// <summary>
        /// PLC是否已经检测到有效HMI心跳。
        /// </summary>
        public const string Online =
            "GVL_HMI.Comm.bOnline";

        /// <summary>
        /// PLC是否判定心跳超时。
        /// </summary>
        public const string Timeout =
            "GVL_HMI.Comm.bTimeout";

        /// <summary>
        /// PLC是否允许HMI继续控制轴。
        /// </summary>
        public const string ControlAllowed =
            "GVL_HMI.Comm.bControlAllowed";

        /// <summary>
        /// 通信恢复后，PLC是否仍要求旧命令清零。
        /// </summary>
        public const string RequireEnableReset =
            "GVL_HMI.Comm.bRequireEnableReset";
    }
}