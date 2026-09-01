using Prism.Mvvm;
using Prism.Regions;
using Prism.Commands;
using System;
using System.Windows;
using System.Collections.Generic;
using System.Windows.Threading;
using ZNQInterface.Infrastructure;
using ZNQInterface.Models;
using ZNQInterface.Communication.Ads;
using ZNQInterface.Services.Axes;
using ZNQInterface.Models.Axes;
using ZNQInterface.Models.Communication;

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
            IHmiHeartbeatService hmiHeartbeat,
            IHmiWatchdogService hmiWatchdogService)
        {
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
            HmiHeartbeat = hmiHeartbeat// 初始化HMI心跳服务
                ?? throw new ArgumentNullException(nameof(hmiHeartbeat));

            // 初始化退出命令。
            ExitApplicationCommand =
                new DelegateCommand(ExecuteExitApplication);

            ToggleAdsConnectionCommand =
                new DelegateCommand(ExecuteToggleAdsConnection);

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

        /// <summary>
        /// 手动连接或断开ADS。
        ///
        /// 主动断开顺序：
        /// 1. 暂停产生新的心跳；
        /// 2. 停止Axis1并撤销使能；
        /// 3. 关闭ADS。
        ///
        /// 主动连接顺序：
        /// 1. 请求ADS连接；
        /// 2. 恢复心跳；
        /// 3. PLC确认命令已清零后重新开放控制。
        // </summary>
        private async void ExecuteToggleAdsConnection()
        {
            try
            {
                if (_adsConnection.IsConnected)
                {
                    AdsStatusText =
                        "ADS通信：正在停止Axis1并撤销使能";

                    /*
                     * 先暂停新的心跳写入，
                     * 防止主动断开过程中继续产生周期ADS写请求。
                     */
                    HmiHeartbeat.Pause();

                    try
                    {
                        /*
                         * ADS仍然连接时完成主动停止和掉使能。
                         */
                        await _axisCommandService
                            .StopAndDisableAsync(
                                AxisId.DamperX);

                        /*
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
                            HmiHeartbeat.Resume();
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
                    HmiHeartbeat.Resume();
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
            void UpdateState()
            {
                IsAdsConnected =
                    eventArgs.State == AdsConnectionState.Connected;
                // 即使仍为 FALSE，也强制刷新 ToggleButton，
                // 覆盖用户点击造成的临时本地 IsChecked 值。
                RaisePropertyChanged(nameof(IsAdsConnected));
                AdsStatusText = eventArgs.Message;
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
                WatchdogState = eventArgs.State;
            }

            /*
             * 当前看门狗服务通常在UI线程触发事件，
             * 这里仍保留Dispatcher保护，避免以后改为后台轮询后
             * 出现跨线程更新ViewModel的问题。
             */
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
        /// <summary>
        /// HMI心跳服务。
        ///
        /// MainWindow.xaml直接绑定该对象的状态，
        /// 不需要在MainWindowViewModel中重复复制所有属性。
        /// </summary>
        public IHmiHeartbeatService HmiHeartbeat
        {
            get;
        }
    }
}
