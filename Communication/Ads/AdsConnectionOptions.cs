using System;

namespace ZNQInterface.Communication.Ads
{
    /// <summary>
    /// ADS 连接参数。
    /// 当前 WPF 与 PLC 位于同一台倍福工控机，
    /// 因此连接本机 AMS Net ID 和 PLC Runtime 1（端口 851）。
    /// </summary>
    public sealed class AdsConnectionOptions
    {
        public int AmsPort { get; init; } = 851;

        /// <summary>
        /// 实时数据显示刷新间隔。20 Hz 足以满足手动调试界面。
        /// </summary>
        public TimeSpan PollInterval { get; init; } =
            TimeSpan.FromMilliseconds(50);

        /// <summary>
        /// 断线后的自动重连间隔。
        /// </summary>
        public TimeSpan ReconnectInterval { get; init; } =
            TimeSpan.FromSeconds(1);
        /// <summary>
        /// 脉冲型命令保持时间。
        /// 100 ms 可跨越多个 PLC 扫描周期，避免上升沿被漏检。
        /// </summary>
        public TimeSpan CommandPulseWidth { get; init; } =
            TimeSpan.FromMilliseconds(100);
        /// <summary>
        /// WPF向PLC发送HMI心跳的周期。
        ///
        /// PLC超时时间建议为2秒，
        /// WPF心跳周期建议为250～500ms。
        /// </summary>
        public TimeSpan HmiHeartbeatInterval { get; init; } =
            TimeSpan.FromMilliseconds(250);
    }
}
