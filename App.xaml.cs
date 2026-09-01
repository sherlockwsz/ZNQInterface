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
using ZNQInterface.Communication.Ads;
using ZNQInterface.Services.Axes;
using ZNQInterface.Models.Axes;

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

            // ADS 全应用单例：一个 AdsClient、一个轮询器、一个命令入口。
            containerRegistry.RegisterSingleton
                <IAdsConnectionService, AdsConnectionService>();
            containerRegistry.RegisterSingleton
                <IAxisCommandService, AxisCommandService>();
            containerRegistry.RegisterSingleton<AxisMonitoringService>();
            /*
             * HMI看门狗服务全应用只允许一个实例：
             * 避免多个页面分别发送心跳。
             */
            containerRegistry.RegisterSingleton
                <IHmiWatchdogService, HmiWatchdogService>();
            /*
             * HMI心跳服务必须为全应用单例。
             *
             * App负责启动和停止；
             * MainWindowViewModel负责显示状态及主动断开时暂停；
             * 两者使用同一个实例。
             */
            containerRegistry.RegisterSingleton
                <IHmiHeartbeatService, HmiHeartbeatService>();
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

            // 启动后连接本机 PLC Runtime 1（851），断线时自动重连。
            _ = StartAdsAsync();

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

        private async System.Threading.Tasks.Task StartAdsAsync()
        {
            IAdsConnectionService connection =
                Container.Resolve<IAdsConnectionService>();

            IHmiWatchdogService watchdog =
                Container.Resolve<IHmiWatchdogService>();

            AxisMonitoringService monitoring =
                Container.Resolve<AxisMonitoringService>();

            /*
             * 启动顺序：
             * 1. 启动ADS连接循环；
             * 2. 启动HMI心跳；
             * 3. 启动Axis1状态监控。
             */
            await connection.StartAsync();
            await watchdog.StartAsync();
            await monitoring.StartAsync();
        }
        /// <summary>
        /// 退出前先释放所有点动/脉冲命令，再停止轮询和 ADS 连接。
        /// bEnable 不在此处强制修改，避免退出 HMI 意外改变设备使能策略。
        /// </summary>
        protected override void OnExit(ExitEventArgs eventArgs)
        {
            try
            {
                /*
                 * 先停止WPF心跳。
                 *
                 * 正常情况下，后续StopAndDisableAsync会完成受控停机；
                 * 如果退出流程卡住或失败，PLC心跳超时后仍能接管。
                 */
                Container.Resolve<IHmiWatchdogService>()
                    .Stop();
                /*
                 * 首先停止产生新的心跳写入。
                 *
                 * 后面的StopAndDisableAsync仍然使用ADS完成受控停止；
                 * PLC看门狗超时时间为2秒，正常受控停止约100ms即可送达。
                 */
                Container.Resolve<IHmiHeartbeatService>()
                    .Stop();
                /*
                * 关闭软件与主动断开连接使用同一套受控停机时序。
                * 如果ADS已经断开，StopAndDisableAsync会直接返回。
                */
                Container.Resolve<IAxisCommandService>()
                    .StopAndDisableAsync(
                        AxisId.DamperX)
                    .GetAwaiter()
                    .GetResult();
                // 3.再次清除全部脉冲及点动位。
                Container.Resolve<IAxisCommandService>()
                    .ReleaseAllMotionSignalsAsync()
                    .GetAwaiter()
                    .GetResult();
                // 4. 停止轴监控。
                // OnExit 位于 UI 线程，不同步等待轮询器投递 UI 更新，
                // 避免退出阶段形成 Dispatcher 互等。
                _ = Container.Resolve<AxisMonitoringService>()
                    .StopAsync();
                // 最后关闭ADS连接。
                Container.Resolve<IAdsConnectionService>()
                    .StopAsync()
                    .GetAwaiter()
                    .GetResult();
            }
            catch
            {
                // 退出阶段 ADS 已断开时不阻止进程关闭。
            }

            base.OnExit(eventArgs);
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
