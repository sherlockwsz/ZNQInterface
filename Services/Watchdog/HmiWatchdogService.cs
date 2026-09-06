using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ZNQInterface.Models.Communication;
using ZNQInterface.Services.Communication.Ads;

namespace ZNQInterface.Services.Watchdog
{
    /// <summary>
    /// WPF侧HMI看门狗服务。
    ///
    /// 主要职责：
    /// 1. 每250ms递增一次心跳；
    /// 2. 通过ADS写入PLC；
    /// 3. 读取PLC最终看门狗反馈；
    /// 4. 生成界面使用的综合状态；
    /// 5. 向ViewModel发布状态变化事件。
    ///
    /// 使用DispatcherTimer：
    /// 如果WPF界面线程严重阻塞，心跳也会停止，
    /// PLC将在超时后停止轴并撤销使能。
    /// </summary>
    public sealed class HmiWatchdogService :
        IHmiWatchdogService
    {
        private readonly IAdsConnectionService _ads;
        private readonly DispatcherTimer _heartbeatTimer;

        /*
         * 防止上一次ADS读写尚未结束时，
         * 下一个DispatcherTimer周期再次进入。
         *
         * 0：当前没有执行；
         * 1：当前正在执行。
         */
        private int _cycleRunning;

        private bool _started;
        private bool _sendingEnabled;

        /*
         * 使用系统运行时间初始化心跳，
         * 降低WPF重新启动后第一次心跳
         * 与PLC上一次保存值完全相同的概率。
         */
        private uint _heartbeat =
            unchecked((uint)Environment.TickCount64);

        public HmiWatchdogService(
            IAdsConnectionService ads)
        {
            _ads = ads
                ?? throw new ArgumentNullException(nameof(ads));

            State = HmiWatchdogState.AdsDisconnected;
            IsControlAllowed = false;

            /*
             * 使用Background优先级：
             * 正常界面操作不会受到影响；
             * UI线程长期阻塞时，心跳也会停止。
             */
            _heartbeatTimer =
                new DispatcherTimer(
                    DispatcherPriority.Background)
                {
                    Interval =
                        _ads.Options.HmiHeartbeatInterval
                };

            _heartbeatTimer.Tick +=
                OnHeartbeatTimerTick;
        }

        public event EventHandler<HmiWatchdogStateChangedEventArgs>
            StateChanged;

        public HmiWatchdogState State { get; private set; }

        public bool IsControlAllowed { get; private set; }

        /// <summary>
        /// 启动心跳。
        ///
        /// DispatcherTimer必须在WPF Dispatcher线程启动。
        /// </summary>
        public Task StartAsync()
        {
            if (_started)
            {
                Resume();
                return Task.CompletedTask;
            }

            _started = true;
            _sendingEnabled = true;

            if (Application.Current == null ||
                Application.Current.Dispatcher.CheckAccess())
            {
                _heartbeatTimer.Start();
                return Task.CompletedTask;
            }

            return Application.Current.Dispatcher
                .InvokeAsync(() =>
                    _heartbeatTimer.Start())
                .Task;
        }

        /// <summary>
        /// 主动断开ADS前暂停发送心跳。
        ///
        /// 定时器仍保留，避免重新连接时重复创建Timer；
        /// 已经进入的ADS调用无法强制撤销，但其结果不会再更新界面状态。
        /// </summary>
        public void Pause()
        {
            _sendingEnabled = false;

            SetState(
                HmiWatchdogState.Paused,
                isControlAllowed: false);
        }

        /// <summary>
        /// ADS重新连接或断开失败后恢复心跳。
        /// 下一次定时周期将重新读取PLC最终反馈。
        /// </summary>
        public void Resume()
        {
            if (!_started)
            {
                _ = StartAsync();
                return;
            }

            _sendingEnabled = true;

            SetState(
                _ads.IsConnected
                    ? HmiWatchdogState.WaitingHeartbeat
                    : HmiWatchdogState.AdsDisconnected,
                isControlAllowed: false);
        }

        /// <summary>
        /// 停止心跳。
        ///
        /// WPF退出时先停止心跳，
        /// 如果后续正常停机流程失败，PLC看门狗仍能最终接管。
        /// </summary>
        public void Stop()
        {
            _sendingEnabled = false;
            _started = false;

            if (Application.Current == null ||
                Application.Current.Dispatcher.CheckAccess())
            {
                StopOnDispatcher();
                return;
            }

            Application.Current.Dispatcher.Invoke(
                StopOnDispatcher);
        }

        private void StopOnDispatcher()
        {
            _heartbeatTimer.Stop();

            SetState(
                HmiWatchdogState.AdsDisconnected,
                isControlAllowed: false);
        }

        /// <summary>
        /// DispatcherTimer回调。
        /// async void仅允许用于事件处理函数。
        /// </summary>
        private async void OnHeartbeatTimerTick(
            object sender,
            EventArgs eventArgs)
        {
            if (!_started || !_sendingEnabled)
            {
                return;
            }

            /*
             * 如果上一个周期还没有完成，
             * 本周期直接跳过，避免ADS操作不断排队。
             */
            if (Interlocked.Exchange(
                    ref _cycleRunning,
                    1) != 0)
            {
                return;
            }

            try
            {
                await ExecuteHeartbeatCycleAsync();
            }
            catch (OperationCanceledException)
            {
                // 应用程序退出时取消，不需要显示为故障。
            }
            catch
            {
                /*
                 * AdsConnectionService会记录具体ADS异常，
                 * 并把连接状态切换为Faulted。
                 *
                 * 看门狗服务只负责将界面状态切换为未连接。
                 */
                if (_started && _sendingEnabled)
                {
                    SetState(
                        HmiWatchdogState.AdsDisconnected,
                        isControlAllowed: false);
                }
            }
            finally
            {
                Interlocked.Exchange(
                    ref _cycleRunning,
                    0);
            }
        }

        private async Task ExecuteHeartbeatCycleAsync()
        {
            if (!_started || !_sendingEnabled)
            {
                return;
            }

            /*
             * ADS尚未连接时不能写入心跳，
             * PLC会在超时时间到达后自行进入安全状态。
             */
            if (!_ads.IsConnected)
            {
                SetState(
                    HmiWatchdogState.AdsDisconnected,
                    isControlAllowed: false);

                return;
            }

            unchecked
            {
                _heartbeat++;
            }

            /*
             * 先写入新心跳。
             *
             * UDINT计数溢出后会自动从0继续，
             * PLC使用“不等于上一次值”判断，因此不受影响。
             */
            await _ads.WriteAsync(
                HmiWatchdogSymbols.Heartbeat,
                _heartbeat);

            // 主动断开可能发生在上面的await期间。
            if (!_started || !_sendingEnabled)
            {
                return;
            }

            /*
             * 即使PLC当前尚未启用看门狗，
             * WPF仍持续发送心跳。
             *
             * 这样PLC以后启用看门狗时，
             * 不需要重新启动WPF。
             */
            bool useWatchdog =
                await _ads.ReadAsync<bool>(
                    HmiWatchdogSymbols.UseWatchdog);

            if (!_started || !_sendingEnabled)
            {
                return;
            }

            if (!useWatchdog)
            {
                SetState(
                    HmiWatchdogState.Disabled,
                    isControlAllowed: true);

                return;
            }

            /*
             * 读取PLC最终反馈。
             *
             * WPF不能根据“心跳写入成功”直接判断允许控制，
             * 必须以PLC计算后的状态为准。
             */
            bool online =
                await _ads.ReadAsync<bool>(
                    HmiWatchdogSymbols.Online);

            bool timeout =
                await _ads.ReadAsync<bool>(
                    HmiWatchdogSymbols.Timeout);

            bool controlAllowed =
                await _ads.ReadAsync<bool>(
                    HmiWatchdogSymbols.ControlAllowed);

            bool requireEnableReset =
                await _ads.ReadAsync<bool>(
                    HmiWatchdogSymbols.RequireEnableReset);

            // 不允许暂停前启动的旧周期覆盖“已暂停”状态。
            if (!_started || !_sendingEnabled)
            {
                return;
            }

            if (timeout)
            {
                SetState(
                    HmiWatchdogState.Timeout,
                    isControlAllowed: false);

                return;
            }

            if (!online)
            {
                SetState(
                    HmiWatchdogState.WaitingHeartbeat,
                    isControlAllowed: false);

                return;
            }

            if (requireEnableReset ||
                !controlAllowed)
            {
                SetState(
                    HmiWatchdogState.WaitingCommandReset,
                    isControlAllowed: false);

                return;
            }

            SetState(
                HmiWatchdogState.Online,
                isControlAllowed: true);
        }

        /// <summary>
        /// 更新综合状态并发布变化事件。
        /// 只有状态或控制许可发生变化时才通知界面。
        /// </summary>
        private void SetState(
            HmiWatchdogState state,
            bool isControlAllowed)
        {
            if (State == state &&
                IsControlAllowed == isControlAllowed)
            {
                return;
            }

            State = state;
            IsControlAllowed = isControlAllowed;

            /*
             * 当前服务由DispatcherTimer驱动，
             * 因此该事件正常会在UI线程触发。
             */
            StateChanged?.Invoke(
                this,
                new HmiWatchdogStateChangedEventArgs(
                    state,
                    isControlAllowed));
        }
    }
}
