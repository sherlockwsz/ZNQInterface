#nullable enable
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Prism.Commands;
using Prism.Mvvm;
using ZNQInterface.Services.Processes;
namespace ZNQInterface.ViewModels.Dialogs
{
    public sealed class ManualCodeEntryViewModel : BindableBase
    {
        private readonly WorkpieceTrackingOptions _options;
        private readonly long _deadline;
        private string _code = "", _status = "自动扫码未识别；补录不暂停PLC运动";
        private double _remaining;
        private bool _available;
        private readonly TaskCompletionSource<string?> _decision = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<string?> Decision => _decision.Task;
        public string IdentityText { get; }
        public string Code { get => _code; set {
            if (SetProperty(ref _code, value)) {
                try { PlcString128.Encode(value.Trim(), _options.GetEncoding()); Status = "确认后等待PLC当次ACK"; }
                catch (Exception e) { Status = e.Message; }
                ConfirmCommand.RaiseCanExecuteChanged();
            }
        } }
        public string Status { get => _status; set => SetProperty(ref _status, value); }
        public double RemainingSeconds { get => _remaining; private set => SetProperty(ref _remaining, value); }
        public DelegateCommand ConfirmCommand { get; }
        public DelegateCommand SkipCommand { get; }
        public ManualCodeEntryViewModel(ScannerEvent value, long deadline, WorkpieceTrackingOptions options)
        {
            _options = options; _deadline = deadline;
            IdentityText = $"会话 {value.SessionId} / 请求 {value.RequestId} / 结果 {value.ResultId}\n来源：第{value.Tray}盘，第{value.Row}行，第{value.Column}列";
            ConfirmCommand = new DelegateCommand(Confirm, CanConfirm);
            SkipCommand = new DelegateCommand(() => _decision.TrySetResult(null), () => !_decision.Task.IsCompleted);
            Tick();
        }
        public void SetAvailability(bool available)
        {
            if (_available == available) return;
            _available = available;
            if (!available && !_decision.Task.IsCompleted) Status = "等待可信工件身份或ADS连接；确认按钮暂不可用";
            else if (available && !_decision.Task.IsCompleted) Status = "自动扫码未识别；补录不暂停PLC运动";
            ConfirmCommand.RaiseCanExecuteChanged();
        }
        public void Tick()
        {
            RemainingSeconds = Math.Max(0, (_deadline - Stopwatch.GetTimestamp()) / (double)Stopwatch.Frequency);
            if (RemainingSeconds <= 0 && !_decision.Task.IsCompleted) { Status = "补录时限已到"; _decision.TrySetResult(null); }
            ConfirmCommand.RaiseCanExecuteChanged(); SkipCommand.RaiseCanExecuteChanged();
        }
        public void Closed() => _decision.TrySetResult(null);
        private bool CanConfirm()
        {
            if (!_available || _decision.Task.IsCompleted || Stopwatch.GetTimestamp() >= _deadline) return false;
            try { PlcString128.Encode(Code.Trim(), _options.GetEncoding()); return true; }
            catch { return false; }
        }
        private void Confirm()
        {
            if (!CanConfirm()) return;
            string code = Code.Trim();
            try { PlcString128.Encode(code, _options.GetEncoding()); }
            catch (Exception e) { Status = e.Message; return; }
            _decision.TrySetResult(code); Status = "正在提交，等待PLC当次ACK";
            ConfirmCommand.RaiseCanExecuteChanged(); SkipCommand.RaiseCanExecuteChanged();
        }
    }
}
