using Prism.Mvvm;
using Prism.Regions;
using Prism.Commands;
using System;
using System.Windows;
using System.Collections.Generic;
using System.Windows.Threading;
using System.Windows.Media;
using ZNQInterface.Infrastructure;
using ZNQInterface.Models.Navigation;
using ZNQInterface.Services.Communication.Ads;
using ZNQInterface.Services.Axes;
using ZNQInterface.Services.Watchdog;
using ZNQInterface.Models.Communication;
using System.Threading;
using System.Threading.Tasks;
using ZNQInterface.Models.Device;
using ZNQInterface.Services.Device;
namespace ZNQInterface.ViewModels
{
    /// <summary>
    /// 主窗口状态、导航和系统时间管理。
    /// </summary>
    public class MainWindowViewModel : BindableBase
    {

        // Prism 内容区域管理器。
        private readonly IRegionManager _regionManager;
        // 系统时间刷新计时器。
        private readonly DispatcherTimer _systemTimeTimer;
        private readonly IAdsConnectionService _adsConnection;
        private readonly IAxisCommandService _axisCommandService;
        // WPF心跳和PLC看门狗状态服务。
        private readonly IHmiWatchdogService _hmiWatchdogService;

        private readonly IDeviceControlService _deviceControlService;

        private readonly DispatcherTimer _deviceStateTimer;

        private int _deviceStateCycleRunning;
        private bool _deviceCommandExecuting;

        private DeviceControlState _machineState;
        private bool _hasMachineSnapshot;
        private bool _prepareRequestPending;
        private bool _machineRequestExecuting;
        private int _adsStateEpoch;

        public DelegateCommand PrepareMachineCommand { get; }
        public DelegateCommand ResetMachineFaultCommand { get; }
        public MachineMode? CurrentMachineMode =>
            _hasMachineSnapshot ? _machineState.MachineMode : null;
        public bool IsMachineFault => _hasMachineSnapshot && _machineState.IsMachineFault;
        public string PrepareButtonText => _hasMachineSnapshot &&
            (_machineState.MachineMode == MachineMode.Preparing || _machineState.IsPreparationBusy)
                ? "准备中" : "整机准备";
        public string PreparationStatusText => !_hasMachineSnapshot ? "准备状态未知" :
            _machineState.IsPreparationError || _machineState.IsPreparationTimeout
                ? $"整机准备{(_machineState.IsPreparationTimeout ? "超时" : "失败")}，轴{_machineState.PreparationErrorAxis}，0x{_machineState.PreparationErrorCode:X8}"
                : _machineState.IsPreparationBusy ? "整机准备中" :
                  _machineState.IsPreparationDone ? "PLC准备完成" : "整机准备";
        public string MachineRunStatusText => !_hasMachineSnapshot
            ? (IsAdsConnected ? "设备运行状态：未知（等待PLC状态）" : "设备运行状态：未知（ADS未连接）")
            : "设备运行状态：" + (_machineState.MachineMode switch
            {
                MachineMode.Disabled => "未使能",
                MachineMode.Manual => "待机",
                MachineMode.AutoStarting => "自动启动中",
                MachineMode.Preparing => "整机准备中",
                MachineMode.Automatic => "自动运行中",
                MachineMode.Homing => "回零中",
                MachineMode.Stopping => "停止中",
                MachineMode.Fault => "故障停止",
                _ => $"未知（PLC模式{(short)_machineState.MachineMode}）"
            });
        public string MachineModeText => !_hasMachineSnapshot ? "运行模式：未知"
            : "运行模式：" + (_machineState.MachineMode switch
            {
                MachineMode.Disabled => "禁用",
                MachineMode.Manual => "手动",
                MachineMode.AutoStarting => "自动启动",
                MachineMode.Preparing => "整机准备",
                MachineMode.Automatic => "自动",
                MachineMode.Homing => "回零",
                MachineMode.Stopping => "停止",
                MachineMode.Fault => "故障",
                _ => $"未知（{(short)_machineState.MachineMode}）"
            });
        public string FaultStatusText => !_hasMachineSnapshot ? "当前报警：状态未知"
            : !_machineState.IsMachineFault ? "当前报警：无"
            : "当前报警：" + MachineFaultTextResolver.Resolve(
                _machineState.MachineFaultSource, _machineState.MachineFaultAxis,
                _machineState.MachineFaultCode) +
                (_machineState.IsMachineFaultResetBlocked ? "（源故障仍存在）" : "（故障源已解除，可复位）");

        private void NotifyMachineState()
        {
            RaisePropertyChanged(nameof(CurrentMachineMode));
            RaisePropertyChanged(nameof(IsMachineFault));
            RaisePropertyChanged(nameof(PrepareButtonText));
            RaisePropertyChanged(nameof(PreparationStatusText));
            RaisePropertyChanged(nameof(MachineRunStatusText));
            RaisePropertyChanged(nameof(MachineModeText));
            RaisePropertyChanged(nameof(FaultStatusText));
            PrepareMachineCommand.RaiseCanExecuteChanged();
            ResetMachineFaultCommand.RaiseCanExecuteChanged();
            ToggleDeviceControlCommand.RaiseCanExecuteChanged();
        }

        // 仅用于UI防重复；PLC保留所有准入及恢复判断。
        private bool CanPrepareMachine() => _hasMachineSnapshot &&
            _adsConnection.IsConnected && !_deviceCommandExecuting && !_machineRequestExecuting &&
            !_prepareRequestPending && !_machineState.IsControlPrepare &&
            !_machineState.IsPreparationBusy &&
            _machineState.MachineMode == MachineMode.Manual;
        private bool CanResetMachineFault() => _hasMachineSnapshot &&
            _adsConnection.IsConnected && !_deviceCommandExecuting && !_machineRequestExecuting &&
            (_machineState.MachineMode == MachineMode.Manual ||
             _machineState.MachineMode == MachineMode.Fault);

        private async void ExecutePrepareMachine()
        {
            if (!CanPrepareMachine()) return;
            _machineRequestExecuting = true;
            NotifyMachineState();
            try
            {
                await _deviceControlService.RequestPrepareAsync();
                _prepareRequestPending = true;
                DeviceStatusText = "设备：整机准备请求已发送";
                await RefreshDeviceStateAsync();
            }
            catch (Exception exception)
            {
                DeviceStatusText = $"整机准备请求失败：{exception.Message}";
            }
            finally
            {
                _machineRequestExecuting = false;
                NotifyMachineState();
            }
        }

        private async void ExecuteResetMachineFault()
        {
            if (!CanResetMachineFault()) return;
            _machineRequestExecuting = true;
            NotifyMachineState();
            try
            {
                await _deviceControlService.RequestMachineFaultResetAsync();
                DeviceStatusText = "设备：故障恢复请求已消费，等待PLC结果";
                await RefreshDeviceStateAsync();
            }
            catch (Exception exception)
            {
                DeviceStatusText = $"故障恢复请求失败：{exception.Message}";
            }
            finally
            {
                _machineRequestExecuting = false;
                NotifyMachineState();
            }
        }

        private bool _isControlAuto;
        private bool _isAutoStopping;
        private bool _isAutoStopDone;
        private bool _isAutoStopError;
        private string _deviceStatusText =
            "设备：等待连接";
        //==============================================================
        // 设备启停按钮外观
        //==============================================================
        private string _deviceButtonText =
            "启动设备";

        private Brush _deviceButtonBackground =
            Brushes.Green;

        private Brush _deviceButtonBorderBrush =
            Brushes.Green;
        // 当前界面显示的看门狗综合状态。
        private HmiWatchdogState _watchdogState =
            HmiWatchdogState.AdsDisconnected;

        private string _title = "自  动  同  轴  度  调  整  设  备";
        private string _currentSystemTime;
        private bool _isAdsConnected;
        private string _adsStatusText;


        // 当前选中的页面导航项。
        private NavigationItem _selectedNavigationItem;
        public MainWindowViewModel(
            IRegionManager regionManager,
            IAdsConnectionService adsConnection,
            IAxisCommandService axisCommandService,
            IHmiWatchdogService hmiWatchdogService,
            IDeviceControlService deviceControlService)
        {
            _deviceControlService =
                deviceControlService ??
                throw new ArgumentNullException(
                    nameof(deviceControlService));

            _hmiWatchdogService = hmiWatchdogService
                ?? throw new ArgumentNullException(
                    nameof(hmiWatchdogService));

            _watchdogState =
                _hmiWatchdogService.State;

            _hmiWatchdogService.StateChanged +=
                OnHmiWatchdogStateChanged;
            _regionManager = regionManager// 初始化区域管理器
                ?? throw new ArgumentNullException(nameof(regionManager));
            _adsConnection = adsConnection// 初始化ADS连接服务
                ?? throw new ArgumentNullException(nameof(adsConnection));
            _axisCommandService = axisCommandService// 初始化轴命令服务
                ?? throw new ArgumentNullException(nameof(axisCommandService));

            // 初始化退出命令。
            ExitApplicationCommand =
                new DelegateCommand(ExecuteExitApplication);

            ToggleAdsConnectionCommand =
                new DelegateCommand(ExecuteToggleAdsConnection);


            ToggleDeviceControlCommand =
            new DelegateCommand(
                ExecuteToggleDeviceControl,
                CanToggleDeviceControl);

            PrepareMachineCommand = new DelegateCommand(ExecutePrepareMachine, CanPrepareMachine);
            ResetMachineFaultCommand = new DelegateCommand(ExecuteResetMachineFault, CanResetMachineFault);

            _deviceStateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };

            _deviceStateTimer.Tick +=
                OnDeviceStateTimerTick;

            _deviceStateTimer.Start();

            _isAdsConnected = _adsConnection.IsConnected;
            _adsStatusText = _adsConnection.StateMessage;
            _adsConnection.StateChanged += OnAdsConnectionStateChanged;

            // 初始化页面导航项。
            NavigationItems = new List<NavigationItem>
            {
                new("设备总览", NavigationKeys.Overview),
                new("手动调试", NavigationKeys.ManualControl),
                new("产品数据", NavigationKeys.ProductData),
                new("操作日志", NavigationKeys.OperationLog)
            };

            _selectedNavigationItem = NavigationItems[0];

            /*
             * 初始化当前系统时间
             */
            _currentSystemTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            _systemTimeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _systemTimeTimer.Tick += (_, _) =>
                CurrentSystemTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _systemTimeTimer.Start();
        }
        public string DeviceButtonText
        {
            get => _deviceButtonText;
            private set =>
                SetProperty(
                    ref _deviceButtonText,
                    value);
        }

        public Brush DeviceButtonBackground
        {
            get => _deviceButtonBackground;
            private set =>
                SetProperty(
                    ref _deviceButtonBackground,
                    value);
        }

        public Brush DeviceButtonBorderBrush
        {
            get => _deviceButtonBorderBrush;
            private set =>
                SetProperty(
                    ref _deviceButtonBorderBrush,
                    value);
        }
        public bool IsControlAuto
        {
            get => _isControlAuto;

            private set
            {
                if (SetProperty(
                        ref _isControlAuto,
                        value))
                {
                    UpdateDeviceButtonAppearance();

                    ToggleDeviceControlCommand
                        .RaiseCanExecuteChanged();
                }
            }
        }
        public bool IsAutoStopping
        {
            get => _isAutoStopping;

            private set
            {
                if (SetProperty(
                        ref _isAutoStopping,
                        value))
                {
                    UpdateDeviceButtonAppearance();

                    ToggleDeviceControlCommand
                        .RaiseCanExecuteChanged();
                }
            }
        }
        public bool IsAutoStopDone
        {
            get => _isAutoStopDone;
            private set =>
                SetProperty(ref _isAutoStopDone, value);
        }

        public bool IsAutoStopError
        {
            get => _isAutoStopError;

            private set
            {
                if (SetProperty(
                        ref _isAutoStopError,
                        value))
                {
                    UpdateDeviceButtonAppearance();

                    ToggleDeviceControlCommand
                        .RaiseCanExecuteChanged();
                }
            }
        }
        public string DeviceStatusText
        {
            get => _deviceStatusText;
            private set =>
                SetProperty(ref _deviceStatusText, value);
        }
        public DelegateCommand ToggleDeviceControlCommand
        {
            get;
        }
        /// <summary>
        /// 根据PLC实际状态更新设备启停按钮外观。
        ///
        /// 优先级：
        /// 停止异常 > 正在停止 > 自动运行 > 等待启动。
        /// </summary>
        private void UpdateDeviceButtonAppearance()
        {
            //==========================================================
            // 1. PLC停止异常
            //==========================================================
            if (IsAutoStopError)
            {
                DeviceButtonText =
                    "停止异常";

                DeviceButtonBackground =
                    Brushes.OrangeRed;

                DeviceButtonBorderBrush =
                    Brushes.OrangeRed;

                return;
            }

            //==========================================================
            // 2. PLC正在执行全轴停止
            //==========================================================
            if (IsAutoStopping)
            {
                DeviceButtonText =
                    "停止中";

                DeviceButtonBackground =
                    Brushes.Gray;

                DeviceButtonBorderBrush =
                    Brushes.Gray;

                return;
            }

            //==========================================================
            // 3. WPF正在发送启停命令
            //==========================================================
            if (_deviceCommandExecuting)
            {
                DeviceButtonText =
                    IsControlAuto
                        ? "正在停止"
                        : "正在启动";

                DeviceButtonBackground =
                    Brushes.Gray;

                DeviceButtonBorderBrush =
                    Brushes.Gray;

                return;
            }

            //==========================================================
            // 4. PLC实际处于自动运行状态
            //==========================================================
            if (IsControlAuto)
            {
                DeviceButtonText =
                    "停止设备";

                DeviceButtonBackground =
                    Brushes.Red;

                DeviceButtonBorderBrush =
                    Brushes.Red;

                return;
            }

            //==========================================================
            // 5. 等待启动
            //==========================================================
            DeviceButtonText =
                "启动设备";

            DeviceButtonBackground =
                Brushes.Green;

            DeviceButtonBorderBrush =
                Brushes.Green;
        }
        private async void ExecuteToggleDeviceControl()
        {
            if (_deviceCommandExecuting)
            {
                return;
            }

            _deviceCommandExecuting = true;

            UpdateDeviceButtonAppearance();

            ToggleDeviceControlCommand
                .RaiseCanExecuteChanged();
            NotifyMachineState();

            try
            {
                if (IsControlAuto)
                {
                    DeviceStatusText =
                        "设备：正在请求停止";

                    await _deviceControlService
                        .RequestStopAsync();

                    DeviceStatusText =
                        "设备：正在停止";
                }
                else
                {
                    DeviceStatusText =
                        "设备：正在启动";

                    await _deviceControlService
                        .StartAutoAsync();

                    DeviceStatusText =
                        "设备：启动请求已发送";
                }

                await RefreshDeviceStateAsync();
            }
            catch (Exception exception)
            {
                DeviceStatusText =
                    $"设备控制失败：{exception.Message}";
            }
            finally
            {
                _deviceCommandExecuting = false;

                UpdateDeviceButtonAppearance();

                ToggleDeviceControlCommand
                    .RaiseCanExecuteChanged();
                NotifyMachineState();
            }
        }
        private bool CanToggleDeviceControl()
        {
            if (_deviceCommandExecuting ||
                !_adsConnection.IsConnected ||
                IsAutoStopping)
            {
                return false;
            }

            // 重连后先等待有效快照，不能用缓存的自动位发送命令。
            if (!_hasMachineSnapshot) return false;

            // 自动运行期间始终优先允许人工停止。
            if (IsControlAuto)
            {
                return true;
            }

            // 停止异常后禁止直接重新启动。
            // 应先执行故障复位并确认现场状态。
            if (IsAutoStopError)
            {
                return false;
            }

            if (_machineRequestExecuting || !_hasMachineSnapshot ||
                _prepareRequestPending || _machineState.IsControlPrepare ||
                _machineState.IsPreparationBusy ||
                _machineState.MachineMode == MachineMode.Preparing ||
                _machineState.MachineMode == MachineMode.Stopping ||
                _machineState.MachineMode == MachineMode.Fault)
                return false;

            return _hmiWatchdogService.IsControlAllowed;
        }
        private async void OnDeviceStateTimerTick(
            object sender,
            EventArgs eventArgs)
        {
            if (_deviceCommandExecuting ||
                !_adsConnection.IsConnected)
            {
                return;
            }

            if (Interlocked.Exchange(
                    ref _deviceStateCycleRunning,
                    1) != 0)
            {
                return;
            }

            try
            {
                await RefreshDeviceStateAsync();
            }
            catch
            {
                IsControlAuto = false;
                IsAutoStopping = false;
                _hasMachineSnapshot = false;
                NotifyMachineState();
                DeviceStatusText = "设备：状态读取失败";
            }
            finally
            {
                Interlocked.Exchange(
                    ref _deviceStateCycleRunning,
                    0);
            }
        }

        private async Task RefreshDeviceStateAsync()
        {
            if (!_adsConnection.IsConnected)
            {
                IsControlAuto = false;
                IsAutoStopping = false;
                _hasMachineSnapshot = false;
                NotifyMachineState();
                DeviceStatusText = "设备：ADS未连接";
                return;
            }

            int readEpoch = Volatile.Read(ref _adsStateEpoch);
            DeviceControlState state =
                await _deviceControlService.ReadStateAsync();
            // 丢弃断线/重连前发起的读取，避免旧快照重新覆盖未知状态。
            if (!_adsConnection.IsConnected || readEpoch != Volatile.Read(ref _adsStateEpoch))
                return;
            _machineState = state;
            _hasMachineSnapshot = true;
            if (!state.IsControlPrepare) _prepareRequestPending = false;
            NotifyMachineState();

            IsControlAuto =
                state.IsControlAuto;

            IsAutoStopping =
                state.IsAutoStopping;

            IsAutoStopDone =
                state.IsAutoStopDone;

            IsAutoStopError =
                state.IsAutoStopError;

            if (state.IsAutoStopping)
            {
                DeviceStatusText =
                    "设备：正在停止";
            }
            else if (state.IsAutoStopError)
            {
                DeviceStatusText =
                    state.IsAutoStopTimeout
                        ? "设备：停止超时"
                        : $"设备：停止异常，轴号{state.AutoStopErrorAxis}，" +
                          $"错误0x{state.AutoStopErrorId:X8}";
            }
            else if (state.IsControlAuto)
            {
                DeviceStatusText =
                    "设备：自动运行中";
            }
            else if (state.IsAutoStopDone)
            {
                DeviceStatusText =
                    "设备：已停止，全部轴已撤销使能";
            }
            else
            {
                DeviceStatusText =
                    "设备：等待启动";
            }
        }
        /// <summary>
        /// 手动连接或断开ADS。
        ///
        /// 主动断开顺序：
        /// 1. 暂停产生新的心跳；
        /// 2. 停止全部已接入轴并撤销使能；
        /// 3. 关闭ADS。
        ///
        /// 主动连接顺序：
        /// 1. 请求ADS连接；
        /// 2. 恢复心跳；
        /// 3. PLC确认命令已清零后重新开放控制。
        /// </summary>
        private async void ExecuteToggleAdsConnection()
        {
            try
            {
                if (_adsConnection.IsConnected)
                {
                    AdsStatusText =
                        "ADS通信：正在停止全部已映射轴并撤销使能";

                    /*
                     * 先暂停新的心跳写入，
                     * 防止主动断开过程中继续产生周期ADS写请求。
                     */
                    _hmiWatchdogService.Pause();

                    try
                    {
                        /// <summary>
                        /// 手动连接或断开ADS。
                        ///
                        /// 主动断开顺序：
                        /// 1. 暂停产生新的心跳；
                        /// 2. 停止全部已映射轴并撤销使能；
                        /// 3. 关闭ADS。
                        /// </summary>
                        await _axisCommandService
                            .StopAndDisableAllMappedAxesAsync();                        /*
                         * 安全动作成功后关闭ADS。
                         */
                        await _adsConnection
                            .DisconnectAsync();
                    }
                    catch
                    {
                        /*
                         * 如果停止或断开失败且ADS仍然连接，
                         * 恢复心跳，避免PLC因为操作失败而意外超时。
                         */
                        if (_adsConnection.IsConnected)
                        {
                            _hmiWatchdogService.Resume();
                        }

                        throw;
                    }
                }
                else
                {
                    /*
                     * ConnectAsync负责恢复DesiredConnected。
                     * 即使本次连接失败，自动重连循环仍会继续尝试。
                     */
                    await _adsConnection
                        .ConnectAsync();

                    /*
                     * 恢复心跳服务。
                     * ADS尚未连上时不会写PLC；
                     * 后续自动重连成功后会自动开始写入。
                     */
                    _hmiWatchdogService.Resume();
                }
            }
            catch (Exception exception)
            {
                AdsStatusText =
                    $"ADS通信：操作失败 - {exception.Message}";
            }
        }
        private void OnAdsConnectionStateChanged(
            object sender,
            AdsConnectionStateChangedEventArgs eventArgs)
        {
            Interlocked.Increment(ref _adsStateEpoch);
            void UpdateState()
            {
                _hasMachineSnapshot = false;
                _prepareRequestPending = false;
                IsAdsConnected =
                    eventArgs.State == AdsConnectionState.Connected;
                // 即使仍为 FALSE，也强制刷新 ToggleButton，
                // 覆盖用户点击造成的临时本地 IsChecked 值。
                RaisePropertyChanged(nameof(IsAdsConnected));
                AdsStatusText = eventArgs.Message;
                NotifyMachineState();
            }

            if (Application.Current?.Dispatcher.CheckAccess() != false)
            {
                UpdateState();
            }
            else
            {
                Application.Current.Dispatcher.BeginInvoke(
                    (Action)UpdateState);
            }
        }
        /// <summary>
        /// PLC看门狗综合状态发生变化。
        /// </summary>
        private void OnHmiWatchdogStateChanged(
            object sender,
            HmiWatchdogStateChangedEventArgs eventArgs)
        {
            void UpdateState()
            {
                WatchdogState =
                    eventArgs.State;

                ToggleDeviceControlCommand
                    .RaiseCanExecuteChanged();
            }

            if (Application.Current?.Dispatcher.CheckAccess() != false)
            {
                UpdateState();
            }
            else
            {
                Application.Current.Dispatcher.BeginInvoke(
                    (Action)UpdateState);
            }
        }
        // 执行应用程序退出流程。
        private void ExecuteExitApplication()
        {
            MessageBoxResult result = MessageBox.Show(
                "确定要退出自动同轴度调整设备软件吗？",
                "退出确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            // 停止系统时间定时器
            _systemTimeTimer.Stop();
            _deviceStateTimer.Stop();
            // 退出整个应用程序
            Application.Current.Shutdown();
        }

        /// <summary>
        /// 页面导航项集合。
        /// </summary>
        public IReadOnlyList<NavigationItem> NavigationItems { get; }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// 当前选中的导航项。
        /// </summary>
        public NavigationItem SelectedNavigationItem
        {
            get => _selectedNavigationItem;
            set
            {
                if (value == null ||
                    !SetProperty(ref _selectedNavigationItem, value))
                {
                    return;
                }

                _regionManager.RequestNavigate(
                    RegionNames.ContentRegion,
                    value.NavigationKey);
            }
        }

        /// <summary>
        /// 当前系统时间。
        /// </summary>
        public string CurrentSystemTime
        {
            get => _currentSystemTime;
            private set => SetProperty(ref _currentSystemTime, value);
        }

        public bool IsAdsConnected
        {
            get => _isAdsConnected;
            private set => SetProperty(ref _isAdsConnected, value);
        }

        public string AdsStatusText
        {
            get => _adsStatusText;
            private set => SetProperty(ref _adsStatusText, value);
        }
        /// <summary>
        /// 当前HMI看门狗综合状态。
        ///
        /// XAML根据该枚举切换同一个指示灯的颜色。
        /// </summary>
        public HmiWatchdogState WatchdogState
        {
            get => _watchdogState;

            private set
            {
                if (SetProperty(
                        ref _watchdogState,
                        value))
                {
                    /*
                     * 状态变化后，
                     * 同时通知界面重新读取动态文字。
                     */
                    RaisePropertyChanged(
                        nameof(WatchdogStatusText));
                }
            }
        }

        /// <summary>
        /// 看门狗指示灯旁边显示的动态文字。
        /// </summary>
        public string WatchdogStatusText =>
            WatchdogState switch
            {
                HmiWatchdogState.AdsDisconnected =>
                    "看门狗：ADS未连接",

                HmiWatchdogState.Paused =>
                    "看门狗：已暂停",

                HmiWatchdogState.Disabled =>
                    "看门狗：未启用",

                HmiWatchdogState.WaitingHeartbeat =>
                    "看门狗：等待心跳",

                HmiWatchdogState.Online =>
                    "看门狗：正常",

                HmiWatchdogState.WaitingCommandReset =>
                    "看门狗：等待命令复位",

                HmiWatchdogState.Timeout =>
                    "看门狗：通信超时",

                _ =>
                    "看门狗：未知"
            };
        /// <summary>
        /// 退出应用程序命令。
        /// </summary>
        public DelegateCommand ExitApplicationCommand { get; }
        public DelegateCommand ToggleAdsConnectionCommand { get; }
    }
}
