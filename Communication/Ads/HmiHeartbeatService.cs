using Prism.Mvvm;
using System;
using System.Windows;
using System.Windows.Threading;

namespace ZNQInterface.Communication.Ads
{
    /// <summary>
    /// 使用WPF DispatcherTimer向PLC发送心跳。
    ///
    /// 选择DispatcherTimer的原因：
    /// 1. 进程退出时，心跳停止；
    /// 2. ADS断开时，心跳写入停止；
    /// 3. UI线程卡死时，DispatcherTimer不再触发，
    ///    PLC会在2秒后检测到心跳超时；
    /// 4. 心跳状态可以直接通知WPF界面刷新。
    /// </summary>
    public sealed class HmiHeartbeatService :
        BindableBase,
        IHmiHeartbeatService
    {
        private readonly IAdsConnectionService _ads;
        private readonly DispatcherTimer _heartbeatTimer;

        private bool _started;
        private bool _sendingEnabled;
        private bool _tickInProgress;

        private uint _heartbeat;

        private bool _isAdsConnected;
        private bool _isPlcOnline;
        private bool _isTimedOut;
        private bool _isControlAllowed;
        private bool _requiresEnableReset;
        private bool _isPulseOn;

        private string _statusText =
            "PLC心跳：尚未启动";

        public HmiHeartbeatService(
            IAdsConnectionService ads)
        {
            _ads = ads
                ?? throw new ArgumentNullException(nameof(ads));

            /*
             * 必须使用Application的UI Dispatcher。
             *
             * 如果UI线程卡死，Timer Tick无法继续触发，
             * PLC端最终会检测到心跳超时。
             */
            Dispatcher dispatcher =
                Application.Current?.Dispatcher
                ?? Dispatcher.CurrentDispatcher;

            _heartbeatTimer = new DispatcherTimer(
                DispatcherPriority.Background,
                dispatcher)
            {
                Interval =
                    _ads.Options.HmiHeartbeatInterval
            };

            _heartbeatTimer.Tick +=
                OnHeartbeatTimerTick;
        }

        public bool IsAdsConnected
        {
            get => _isAdsConnected;
            private set => SetProperty(
                ref _isAdsConnected,
                value);
        }

        public bool IsPlcOnline
        {
            get => _isPlcOnline;
            private set => SetProperty(
                ref _isPlcOnline,
                value);
        }

        public bool IsTimedOut
        {
            get => _isTimedOut;
            private set => SetProperty(
                ref _isTimedOut,
                value);
        }

        public bool IsControlAllowed
        {
            get => _isControlAllowed;
            private set => SetProperty(
                ref _isControlAllowed,
                value);
        }

        public bool RequiresEnableReset
        {
            get => _requiresEnableReset;
            private set => SetProperty(
                ref _requiresEnableReset,
                value);
        }

        /// <summary>
        /// ADS连接有效、PLC已经确认心跳且没有超时。
        /// </summary>
        public bool IsHeartbeatConfirmed =>
            IsAdsConnected
            && IsPlcOnline
            && !IsTimedOut;

        /// <summary>
        /// 心跳正常、PLC开放控制且不需要清除旧命令。
        /// </summary>
        public bool IsHealthy =>
            IsHeartbeatConfirmed
            && IsControlAllowed
            && !RequiresEnableReset;

        public bool IsPulseOn
        {
            get => _isPulseOn;
            private set => SetProperty(
                ref _isPulseOn,
                value);
        }

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(
                ref _statusText,
                value);
        }

        /// <summary>
        /// 启动应用级心跳。
        /// 多次调用不会重复注册或重复启动Timer。
        /// </summary>
        public void Start()
        {
            if (_started)
            {
                Resume();
                return;
            }

            _started = true;
            _sendingEnabled = true;

            StatusText = "PLC心跳：等待ADS连接";

            _heartbeatTimer.Start();
        }

        /// <summary>
        /// 主动断开前暂停心跳。
        ///
        /// Timer仍然存在，但不会继续发起ADS写入。
        /// 这样重新连接时可以直接Resume。
        /// </summary>
        public void Pause()
        {
            _sendingEnabled = false;
            IsPulseOn = false;

            UpdateState(
                adsConnected: _ads.IsConnected,
                plcOnline: false,
                timedOut: false,
                controlAllowed: false,
                requiresEnableReset: false,
                statusText: "PLC心跳：已暂停");
        }

        /// <summary>
        /// 恢复周期心跳。
        /// 如果服务尚未启动，则直接执行Start。
        /// </summary>
        public void Resume()
        {
            if (!_started)
            {
                Start();
                return;
            }

            _sendingEnabled = true;

            StatusText = _ads.IsConnected
                ? "PLC心跳：等待PLC确认"
                : "PLC心跳：等待ADS连接";
        }

        /// <summary>
        /// 应用退出时停止Timer。
        /// </summary>
        public void Stop()
        {
            _sendingEnabled = false;
            _started = false;

            _heartbeatTimer.Stop();
            IsPulseOn = false;

            UpdateState(
                adsConnected: false,
                plcOnline: false,
                timedOut: false,
                controlAllowed: false,
                requiresEnableReset: false,
                statusText: "PLC心跳：已停止");
        }

        /// <summary>
        /// DispatcherTimer事件。
        ///
        /// async void只用于WPF事件处理器；
        /// 方法内部捕获全部异常，防止异常进入Dispatcher。
        /// </summary>
        private async void OnHeartbeatTimerTick(
            object sender,
            EventArgs eventArgs)
        {
            /*
             * 防止上一次ADS读写尚未结束时，
             * 下一个500ms周期再次进入。
             */
            if (!_started
                || !_sendingEnabled
                || _tickInProgress)
            {
                return;
            }

            /*
             * ADS尚未连接时不写PLC。
             * 自动连接成功后，下一个Timer周期会自动恢复。
             */
            if (!_ads.IsConnected)
            {
                UpdateState(
                    adsConnected: false,
                    plcOnline: false,
                    timedOut: false,
                    controlAllowed: false,
                    requiresEnableReset: false,
                    statusText: "PLC心跳：ADS未连接");

                return;
            }

            _tickInProgress = true;

            try
            {
                //--------------------------------------------------
                // 1. 递增心跳
                //--------------------------------------------------

                unchecked
                {
                    _heartbeat++;

                    /*
                     * 计数溢出后跳过0不是PLC判断所必需，
                     * 但可以让在线观察更直观。
                     */
                    if (_heartbeat == 0)
                    {
                        _heartbeat = 1;
                    }
                }


                //--------------------------------------------------
                // 2. 将新心跳写入PLC
                //--------------------------------------------------

                await _ads.WriteAsync(
                    AdsHmiSymbols.Heartbeat,
                    _heartbeat);


                //--------------------------------------------------
                // 3. 读取PLC看门狗反馈
                //
                // 指示灯不能只根据WPF本地Timer闪烁，
                // 必须确认PLC已经实际响应。
                //--------------------------------------------------

                bool plcOnline =
                    await _ads.ReadAsync<bool>(
                        AdsHmiSymbols.Online);

                bool timedOut =
                    await _ads.ReadAsync<bool>(
                        AdsHmiSymbols.Timeout);

                bool controlAllowed =
                    await _ads.ReadAsync<bool>(
                        AdsHmiSymbols.ControlAllowed);

                bool requiresEnableReset =
                    await _ads.ReadAsync<bool>(
                        AdsHmiSymbols.RequireEnableReset);


                /*
                 * 主动断开期间可能在await过程中执行Pause。
                 * 此时不再把刚读到的状态显示为正常。
                 */
                if (!_started || !_sendingEnabled)
                {
                    IsPulseOn = false;
                    return;
                }


                //--------------------------------------------------
                // 4. 生成界面状态文字
                //--------------------------------------------------

                string statusText;

                if (timedOut)
                {
                    statusText =
                        "PLC心跳：PLC已判定超时";
                }
                else if (!plcOnline)
                {
                    statusText =
                        "PLC心跳：等待PLC确认";
                }
                else if (requiresEnableReset)
                {
                    statusText =
                        "PLC心跳：通信恢复，等待命令复位";
                }
                else if (!controlAllowed)
                {
                    statusText =
                        "PLC心跳：PLC未开放HMI控制";
                }
                else
                {
                    statusText =
                        $"PLC心跳：正常，计数 {_heartbeat}";
                }


                //--------------------------------------------------
                // 5. 更新WPF绑定状态
                //--------------------------------------------------

                UpdateState(
                    adsConnected: true,
                    plcOnline: plcOnline,
                    timedOut: timedOut,
                    controlAllowed: controlAllowed,
                    requiresEnableReset: requiresEnableReset,
                    statusText: statusText);


                //--------------------------------------------------
                // 6. 切换指示灯明暗
                //
                // 只有PLC确认心跳有效时才闪烁。
                // 控制许可未恢复时使用黄色闪烁；
                // 完全正常时使用绿色闪烁。
                //--------------------------------------------------

                IsPulseOn = IsHeartbeatConfirmed
                    ? !IsPulseOn
                    : false;
            }
            catch (Exception exception)
            {
                /*
                 * AdsConnectionService会把通信异常转换为Faulted，
                 * 并根据DesiredConnected执行自动重连。
                 */
                UpdateState(
                    adsConnected: false,
                    plcOnline: false,
                    timedOut: false,
                    controlAllowed: false,
                    requiresEnableReset: false,
                    statusText:
                        $"PLC心跳：通信异常 - {exception.Message}");
            }
            finally
            {
                _tickInProgress = false;
            }
        }

        /// <summary>
        /// 集中更新状态并通知派生属性刷新。
        /// </summary>
        private void UpdateState(
            bool adsConnected,
            bool plcOnline,
            bool timedOut,
            bool controlAllowed,
            bool requiresEnableReset,
            string statusText)
        {
            IsAdsConnected = adsConnected;
            IsPlcOnline = plcOnline;
            IsTimedOut = timedOut;
            IsControlAllowed = controlAllowed;
            RequiresEnableReset = requiresEnableReset;
            StatusText = statusText;

            /*
             * IsHeartbeatConfirmed和IsHealthy是计算属性，
             * 其依赖属性改变后需要主动通知WPF重新读取。
             */
            RaisePropertyChanged(
                nameof(IsHeartbeatConfirmed));

            RaisePropertyChanged(
                nameof(IsHealthy));

            if (!IsHeartbeatConfirmed)
            {
                IsPulseOn = false;
            }
        }

        public void Dispose()
        {
            Stop();

            _heartbeatTimer.Tick -=
                OnHeartbeatTimerTick;
        }
    }
}