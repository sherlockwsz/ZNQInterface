#nullable enable
using System;
using System.Windows;
using System.Windows.Threading;
using ZNQInterface.ViewModels.Dialogs;
namespace ZNQInterface.Views.Dialogs
{
    public partial class ManualCodeEntryWindow : Window
    {
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
        public ManualCodeEntryWindow(ManualCodeEntryViewModel vm)
        {
            InitializeComponent(); DataContext = vm;
            _timer.Tick += (_, __) => vm.Tick();
            Loaded += (_, __) => { _timer.Start(); Focus(); };
            Closed += (_, __) => { _timer.Stop(); vm.Closed(); };
        }
    }
}
