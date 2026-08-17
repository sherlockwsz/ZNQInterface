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
            containerRegistry.RegisterForNavigation <OverviewView, OverviewViewModel>(NavigationKeys.Overview);//注册OverviewView页面
            containerRegistry.RegisterForNavigation<AxisMonitorView, AxisMonitorViewModel>(NavigationKeys.AxisMonitor);//注册AxisMonitorView页面
            containerRegistry.RegisterForNavigation<ManualControlView, ManualControlViewModel>(NavigationKeys.ManualControl);//注册ManualControlView页面
            containerRegistry.RegisterForNavigation<ParameterSettingsView, ParameterSettingsViewModel>(NavigationKeys.ParameterSettings);//注册ParameterSettingsView页面
            containerRegistry.RegisterForNavigation<ProductDataView, ProductDataViewModel>(NavigationKeys.ProductData);//注册ProductDataView页面
            containerRegistry.RegisterForNavigation<AlarmView, AlarmViewModel>(NavigationKeys.Alarm);//注册AlarmView页面
            containerRegistry.RegisterForNavigation<OperationLogView, OperationLogViewModel>(NavigationKeys.OperationLog);//注册OperationLogView页面
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
