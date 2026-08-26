using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Prism.Ioc;
using Prism.Regions;
using ZNQInterface.Infrastructure;
using ZNQInterface.ViewModels.Pages;
using ZNQInterface.Views;
using ZNQInterface.Views.Pages;
using ZNQInterface.ViewModels.Components.Axes;

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
            /*
           整个应用程序只创建一个AxisStatusViewModel。

           OverviewViewModel、ManualControlViewModel以及后续ADS通信服务
           注入的都是同一个对象，确保各页面轴数据同步。
           */
            containerRegistry.RegisterSingleton<AxisStatusViewModel>();
            // 注册各功能页面的导航映射。
            containerRegistry.RegisterForNavigation
                <OverviewView, OverviewViewModel>(NavigationKeys.Overview); // 设备总览页面
            containerRegistry.RegisterForNavigation
                <ManualControlView, ManualControlViewModel>(NavigationKeys.ManualControl); // 手动调试页面
            containerRegistry.RegisterForNavigation
                <ProductDataView, ProductDataViewModel>(NavigationKeys.ProductData); // 产品数据页面
            containerRegistry.RegisterForNavigation
                <OperationLogView, OperationLogViewModel>(NavigationKeys.OperationLog); // 操作日志页面
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

        public App()
        {
            DispatcherUnhandledException +=
                OnDispatcherUnhandledException;

            AppDomain.CurrentDomain.UnhandledException +=
                OnUnhandledException;
        }

        private void OnDispatcherUnhandledException(
            object sender,
            DispatcherUnhandledExceptionEventArgs eventArgs)
        {
            string errorMessage = eventArgs.Exception.ToString();

            try
            {
                File.WriteAllText(
                    Path.Combine(
                        AppContext.BaseDirectory,
                        "startup-error.log"),
                    errorMessage);
            }
            catch
            {
                // 写入日志失败时不再抛出异常。
            }

            MessageBox.Show(
                errorMessage,
                "程序发生未处理异常",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            // 仅用于排查问题，正式程序中应根据异常决定是否继续。
            eventArgs.Handled = true;
        }

        private void OnUnhandledException(
            object sender,
            UnhandledExceptionEventArgs eventArgs)
        {
            string errorMessage =
                eventArgs.ExceptionObject?.ToString()
                ?? "未知非UI线程异常";

            try
            {
                File.WriteAllText(
                    Path.Combine(
                        AppContext.BaseDirectory,
                        "fatal-error.log"),
                    errorMessage);
            }
            catch
            {
            }

            MessageBox.Show(
                errorMessage,
                "程序发生严重异常",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
