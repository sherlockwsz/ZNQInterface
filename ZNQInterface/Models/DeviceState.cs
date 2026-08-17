namespace ZNQInterface.Models
{
    /// <summary>
    /// 设备整机主状态。
    /// </summary>
    public enum DeviceState : ushort
    {
        Starting = 0,       // 启动中
        Initializing = 1,   // 初始化中
        NotReady = 2,       // 未就绪
        Standby = 3,        // 待机
        Running = 4,        // 运行中
        Paused = 5,         // 暂停中
        Stopping = 6,       // 停止中
        SafetyStopped = 7,  // 安全停止
        FaultStopped = 8    // 故障停止
    }
}
