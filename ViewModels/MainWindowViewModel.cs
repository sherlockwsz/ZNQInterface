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

        private string _title = "自  动  同  轴  度  调  整  设  备";
        private string _currentSystemTime;
        private bool _isAdsConnected;
        private string _adsStatusText;


        // 当前选中的页面导航项。
        private NavigationItem _selectedNavigationItem;
        public MainWindowViewModel(
            IRegionManager regionManager,
            IAdsConnectionService adsConnection,
            IAxisCommandService axisCommandService)
        {
            _regionManager = regionManager
                ?? throw new ArgumentNullException(nameof(regionManager));
            _adsConnection = adsConnection
                ?? throw new ArgumentNullException(nameof(adsConnection));
            _axisCommandService = axisCommandService
                ?? throw new ArgumentNullException(nameof(axisCommandService));

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
        /// 主动断开时必须先停止Axis1并撤销使能，
        /// 确认相关命令已写入PLC后再断开ADS。
        /// </summary>
        private async void ExecuteToggleAdsConnection()
        {
            try
            {
                if (_adsConnection.IsConnected)
                {
                    AdsStatusText =
                        "ADS通信：正在停止Axis1并撤销使能";

                    /*
                     * 当前只接入Axis1，
                     * 对应阻尼器上下料X轴。
                     *
                     * 此时ADS仍然连接，因此停止和掉使能
                     * 命令能够可靠写入PLC。
                     */
                    await _axisCommandService
                        .StopAndDisableAsync(
                            AxisId.DamperX);

                    /*
                     * 只有停止、掉使能和命令清理全部成功后，
                     * 才真正关闭ADS连接。
                     *
                     * 如果前面的操作抛出异常，
                     * 程序会进入catch，ADS不会继续断开。
                     */
                    await _adsConnection
                        .DisconnectAsync();
                }
                else
                {
                    await _adsConnection
                        .ConnectAsync();
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
        /// 退出应用程序命令。
        /// </summary>
        public DelegateCommand ExitApplicationCommand { get; }

        public DelegateCommand ToggleAdsConnectionCommand { get; }
    }
}
