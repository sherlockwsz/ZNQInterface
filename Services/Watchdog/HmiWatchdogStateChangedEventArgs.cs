using System;
using ZNQInterface.Models.Communication;

namespace ZNQInterface.Services.Watchdog
{
    /// <summary>
    /// HMI看门狗综合状态发生变化时的事件参数。
    /// </summary>
    public sealed class HmiWatchdogStateChangedEventArgs :
        EventArgs
    {
        public HmiWatchdogStateChangedEventArgs(
            HmiWatchdogState state,
            bool isControlAllowed)
        {
            State = state;
            IsControlAllowed = isControlAllowed;
        }

        /// <summary>
        /// 当前综合看门狗状态。
        /// </summary>
        public HmiWatchdogState State { get; }

        /// <summary>
        /// PLC当前是否允许WPF发送控制命令。
        /// </summary>
        public bool IsControlAllowed { get; }
    }
}