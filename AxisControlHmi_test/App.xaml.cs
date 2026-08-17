using AxisControlHmi_test.Services;
using AxisControlHmi_test.Views;
using Prism.Ioc;
using System.Windows;

namespace AxisControlHmi_test
{
    public partial class App
    {
        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterInstance(new AdsConnectionOptions
            {
                AmsNetId = string.Empty,
                Port = 851,
                TimeoutMilliseconds = 1000,
                HeartbeatTimeoutMilliseconds = 3000
            });
            containerRegistry.RegisterSingleton<IAxisService, AdsAxisService>();
        }
    }
}
