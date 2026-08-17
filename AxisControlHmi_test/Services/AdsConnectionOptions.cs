namespace AxisControlHmi_test.Services
{
    public sealed class AdsConnectionOptions
    {
        // 留空表示连接本机 TwinCAT Runtime；远程 PLC 时填写目标 AMS Net ID。
        public string AmsNetId { get; init; } = string.Empty;

        // TwinCAT 3 PLC Runtime 1 的默认 ADS 端口。
        public int Port { get; init; } = 851;

        public int TimeoutMilliseconds { get; init; } = 1000;

        // 与 PLC 的 GVL_AxisCfg.tHmiHeartbeatTimeout = T#3S 保持一致。
        public int HeartbeatTimeoutMilliseconds { get; init; } = 3000;

    }
}
