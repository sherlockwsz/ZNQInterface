namespace ZNQInterface.Models.Communication
{
    /// <summary>
    /// WPF界面使用的HMI看门狗综合状态。
    ///
    /// 该枚举不直接对应PLC单个变量，
    /// 而是由PLC多个看门狗反馈综合计算得到。
    /// </summary>
    public enum HmiWatchdogState
    {
        /// <summary>
        /// ADS当前没有连接，无法读取PLC看门狗状态。
        /// </summary>
        AdsDisconnected = 0,

        /// <summary>
        /// PLC看门狗功能当前未启用。
        /// </summary>
        Disabled = 10,

        /// <summary>
        /// ADS已连接，但PLC尚未检测到有效心跳。
        /// </summary>
        WaitingHeartbeat = 20,

        /// <summary>
        /// 心跳正常，并且PLC允许WPF控制设备。
        /// </summary>
        Online = 30,

        /// <summary>
        /// 心跳已经恢复，但PLC仍在等待旧命令清零。
        /// </summary>
        WaitingCommandReset = 40,

        /// <summary>
        /// PLC检测到HMI心跳超时。
        /// </summary>
        Timeout = 50
    }
}