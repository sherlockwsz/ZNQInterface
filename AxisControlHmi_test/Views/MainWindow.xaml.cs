using AxisControlHmi_test.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AxisControlHmi_test.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

        private void JogPositive_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ViewModel?.JogPositivePressedCommand.Execute();
            Mouse.Capture((Button)sender);
            e.Handled = true;
        }

        private void JogPositive_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ReleaseJogPositive(sender as Button);
            e.Handled = true;
        }

        private void JogPositive_MouseLeave(object sender, MouseEventArgs e)
        {
            if (Mouse.LeftButton == MouseButtonState.Pressed) ReleaseJogPositive(sender as Button);
        }

        private void JogPositive_LostMouseCapture(object sender, MouseEventArgs e)
        {
            ViewModel?.JogPositiveReleasedCommand.Execute();
        }

        private void JogNegative_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ViewModel?.JogNegativePressedCommand.Execute();
            Mouse.Capture((Button)sender);
            e.Handled = true;
        }

        private void JogNegative_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ReleaseJogNegative(sender as Button);
            e.Handled = true;
        }

        private void JogNegative_MouseLeave(object sender, MouseEventArgs e)
        {
            if (Mouse.LeftButton == MouseButtonState.Pressed) ReleaseJogNegative(sender as Button);
        }

        private void JogNegative_LostMouseCapture(object sender, MouseEventArgs e)
        {
            ViewModel?.JogNegativeReleasedCommand.Execute();
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            ViewModel?.JogPositiveReleasedCommand.Execute();
            ViewModel?.JogNegativeReleasedCommand.Execute();
        }

        private void ReleaseJogPositive(Button? button)
        {
            ViewModel?.JogPositiveReleasedCommand.Execute();
            if (button?.IsMouseCaptured == true) button.ReleaseMouseCapture();
        }

        private void ReleaseJogNegative(Button? button)
        {
            ViewModel?.JogNegativeReleasedCommand.Execute();
            if (button?.IsMouseCaptured == true) button.ReleaseMouseCapture();
        }
    }
}
