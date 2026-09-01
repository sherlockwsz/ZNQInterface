using System;
using System.ComponentModel;

namespace ZNQInterface.Communication.Ads
{
    /// <summary>
    /// WPF应用级HMI心跳服务。
    ///
    /// 服务由App启动，由MainWindow显示状态。
    /// DispatcherTimer必须运行在WPF UI线程上，
    /// 这样UI线程卡死后心跳会自然停止。
    /// </summary>
    public interface IHmiHeartbeatService :
        INotifyPropertyChanged,
        IDisposable
    {
        /// <summary>
        /// 当前ADS是否连接。
        /// </summary>
        bool IsAdsConnected { get; }

        /// <summary>
        /// PLC是否确认收到有效心跳。
        /// </summary>
        bool IsPlcOnline { get; }

        /// <summary>
        /// PLC是否判定心跳超时。
        /// </summary>
        bool IsTimedOut { get; }

        /// <summary>
        /// PLC是否允许HMI控制轴。
        /// </summary>
        bool IsControlAllowed { get; }

        /// <summary>
        /// PLC是否要求旧使能和运动命令复位。
        /// </summary>
        bool RequiresEnableReset { get; }

        /// <summary>
        /// ADS和PLC心跳链路是否已经确认有效。
        /// </summary>
        bool IsHeartbeatConfirmed { get; }

        /// <summary>
        /// 心跳与控制许可是否均正常。
        /// </summary>
        bool IsHealthy { get; }

        /// <summary>
        /// 指示灯当前闪烁相位。
        /// </summary>
        bool IsPulseOn { get; }

        /// <summary>
        /// 看门狗状态说明，用于指示灯ToolTip。
        /// </summary>
        string StatusText { get; }

        /// <summary>
        /// 启动UI线程心跳定时器。
        /// </summary>
        void Start();

        /// <summary>
        /// 主动断开ADS前暂停心跳。
        /// </summary>
        void Pause();

        /// <summary>
        /// ADS重新连接后恢复心跳。
        /// </summary>
        void Resume();

        /// <summary>
        /// 应用退出时停止心跳。
        /// </summary>
        void Stop();
    }
}