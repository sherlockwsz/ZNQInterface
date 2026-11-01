using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ZNQInterface.Views.Dialogs
{
    public partial class NgTrayConfirmWindow : Window
    {
        private readonly Func<Task> _confirmAsync;
        private bool _confirming;
        private bool _allowClose;

        public NgTrayConfirmWindow(bool isFullWait, Func<Task> confirmAsync)
        {
            _confirmAsync = confirmAsync ?? throw new ArgumentNullException(nameof(confirmAsync));
            InitializeComponent();
            Title = isFullWait ? "NG 料盘已满，请清空后确认" : "启动前 NG 料盘确认";
            HeadingText.Text = Title;
            ScenarioText.Text = isFullWait
                ? "PLC 已回到可换盘等待状态。请清空 NG 料盘；确认成功后 PLC 自动续行，无需再次启动设备。取消后本轮不会再次弹窗，PLC 将继续等待。"
                : "请先清空 NG 料盘。确认并收到本轮 PLC 成功反馈后，设备将自动启动。取消则不启动设备。";
        }

        private async void OnConfirmClick(object sender, RoutedEventArgs e)
        {
            if (_confirming) return;
            _confirming = true;
            ConfirmButton.IsEnabled = false;
            // 已发送请求不能撤回：等待本次结果期间避免取消后仍启动。
            CancelButton.IsEnabled = false;
            FeedbackText.Text = "正在等待 PLC 反馈……";
            try
            {
                await _confirmAsync();
                _allowClose = true;
                DialogResult = true;
            }
            catch (Exception exception)
            {
                FeedbackText.Text = $"NG 确认失败：{exception.Message}";
            }
            finally
            {
                _confirming = false;
                ConfirmButton.IsEnabled = true;
                CancelButton.IsEnabled = true;
            }
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            if (_confirming) return;
            _allowClose = true;
            DialogResult = false;
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            // X/系统菜单/Alt+F4/其他Close调用不能替代两个按钮。
            if (!_allowClose) e.Cancel = true;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape ||
                (e.Key == Key.System && e.SystemKey == Key.F4))
                e.Handled = true;
        }
    }
}
