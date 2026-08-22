using System;
using System.Windows;
using System.Windows.Threading;
using Prism.Ioc;
using Prism.Regions;
using ZNQInterface.Infrastructure;
using ZNQInterface.ViewModels.Pages;
using ZNQInterface.Views;
using ZNQInterface.Views.Pages;

namespace ZNQInterface
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 注册各功能页面的导航映射。
            containerRegistry.RegisterForNavigation<OverviewView, OverviewViewModel>(NavigationKeys.Overview); // 设备总览页面
            containerRegistry.RegisterForNavigation<ManualControlView, ManualControlViewModel>(NavigationKeys.ManualControl); // 手动调试页面
            containerRegistry.RegisterForNavigation<ProductDataView, ProductDataViewModel>(NavigationKeys.ProductData); // 产品数据页面
            containerRegistry.RegisterForNavigation<OperationLogView, OperationLogViewModel>(NavigationKeys.OperationLog); // 操作日志页面
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();

            // 等主窗口及ContentRegion加载完成后，
            // 默认显示“设备总览”
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(() =>
                {
                    Container.Resolve<IRegionManager>()
                        .RequestNavigate(
                            RegionNames.ContentRegion,
                            NavigationKeys.Overview);
                }));
        }
    }
}
