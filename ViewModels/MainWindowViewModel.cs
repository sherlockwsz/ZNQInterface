using Prism.Mvvm;
using Prism.Regions;
using Prism.Commands;
using System;
using System.Windows;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using ZNQInterface.Infrastructure;
using ZNQInterface.Models;

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

        private string _title = "自  动  同  轴  度  调  整  设  备";
        private string _currentSystemTime;


        // 当前选中的页面导航项。
        private NavigationItem _selectedNavigationItem;
        public MainWindowViewModel(IRegionManager regionManager)
        {
            _regionManager = regionManager;

            // 初始化退出命令。
            ExitApplicationCommand =
                new DelegateCommand(ExecuteExitApplication);

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

        /// <summary>
        /// 退出应用程序命令。
        /// </summary>
        public DelegateCommand ExitApplicationCommand { get; }
    }
}
