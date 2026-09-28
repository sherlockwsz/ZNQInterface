using System;
using System.Threading.Tasks;
using ZNQInterface.Models.Communication;

namespace ZNQInterface.Services.Watchdog
{
    /// <summary>
    /// WPF侧HMI心跳和PLC看门狗状态读取入口。
    /// </summary>
    public interface IHmiWatchdogService
    {
        /// <summary>
        /// 看门狗综合状态发生变化。
        /// </summary>
        event EventHandler<HmiWatchdogStateChangedEventArgs>
            StateChanged;

        /// <summary>
        /// 当前看门狗综合状态。
        /// </summary>
        HmiWatchdogState State { get; }

        /// <summary>
        /// PLC当前是否允许WPF控制设备。
        /// </summary>
        bool IsControlAllowed { get; }

        /// <summary>
        /// 启动WPF心跳发送和PLC状态读取。
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// 主动断开ADS前暂停心跳发送。
        /// </summary>
        void Pause();

        /// <summary>
        /// ADS重新连接或主动断开失败后恢复心跳。
        /// </summary>
        void Resume();

        /// <summary>
        /// 停止WPF心跳发送。
        /// </summary>
        void Stop();
    }
}
