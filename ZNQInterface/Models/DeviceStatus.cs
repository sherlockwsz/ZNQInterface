namespace ZNQInterface.Models
{
    /// <summary>
    /// 设备当前运行状态信息。
    /// </summary>
    public class DeviceStatus
    {
        /// <summary>
        /// 当前整机主状态
        /// </summary>
        public DeviceState State { get; set; }

        /// <summary>
        /// 状态原因，例如“安全门打开”“X轴未回零”
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// 安全回路是否正常
        /// </summary>
        public bool IsSafetyOk { get; set; }

        /// <summary>
        /// 是否满足自动运行条件
        /// </summary>
        public bool IsReady { get; set; }

        /// <summary>
        /// 已使能轴数量
        /// </summary>
        public int EnabledAxisCount { get; set; }

        /// <summary>
        /// 运动中的轴数量
        /// </summary>
        public int MovingAxisCount { get; set; }

        /// <summary>
        /// 故障轴数量
        /// </summary>
        public int FaultAxisCount { get; set; }

        /// <summary>
        /// 当前自动流程步骤
        /// </summary>
        public string CurrentStep { get; set; } = string.Empty;
    }
}
