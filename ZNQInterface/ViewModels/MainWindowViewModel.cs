using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using ZNQInterface.Infrastructure;
using ZNQInterface.Models;

namespace ZNQInterface.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {

        private readonly IRegionManager _regionManager;//区域管理器
        private readonly DispatcherTimer _systemTimeTimer;//系统时间计时器

        private string _title = "自  动  同  轴  度  调  整  设  备";
        private string _currentSystemTime;


        private NavigationItem _selectedNavigationItem;//选中的导航项
        public MainWindowViewModel(IRegionManager regionManager)
        {
            _regionManager = regionManager;

            NavigationItems = new List<NavigationItem>
            {
                new("设备总览", NavigationKeys.Overview),
                new("手动调试", NavigationKeys.ManualControl),
                new("产品数据", NavigationKeys.ProductData),
                new("操作日志", NavigationKeys.OperationLog)
            };

            _selectedNavigationItem = NavigationItems[0];
            _currentSystemTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            _systemTimeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _systemTimeTimer.Tick += (_, _) =>
                CurrentSystemTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _systemTimeTimer.Start();
        }

        public IReadOnlyList<NavigationItem> NavigationItems { get; }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

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

        public string CurrentSystemTime
        {
            get => _currentSystemTime;
            private set => SetProperty(ref _currentSystemTime, value);
        }
    }
}
