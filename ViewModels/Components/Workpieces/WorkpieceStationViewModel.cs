#nullable enable
using System.Threading;
using Prism.Mvvm;
using ZNQInterface.Services.Processes;
namespace ZNQInterface.ViewModels.Components.Workpieces
{
    public sealed class WorkpieceStationViewModel : BindableBase
    {
        private WorkpieceSnapshot? _snapshot;
        private string _transfer = "待确认", _adjustment = "待确认", _trackingStatus = "等待PLC权威工位快照", _consumerStatus = "等待扫码队列同步";
        public WorkpieceSnapshot? CurrentSnapshot => Volatile.Read(ref _snapshot);
        public string TransferDisplayCode { get => _transfer; private set => SetProperty(ref _transfer, value); }
        public string AdjustmentDisplayCode { get => _adjustment; private set => SetProperty(ref _adjustment, value); }
        public string TrackingStatus { get => _trackingStatus; private set => SetProperty(ref _trackingStatus, value); }
        public string ConsumerStatus { get => _consumerStatus; set { if (SetProperty(ref _consumerStatus, value)) RaisePropertyChanged(nameof(StatusText)); } }
        public string StatusText => TrackingStatus + "；" + ConsumerStatus;
        public void Apply(WorkpieceSnapshot snapshot)
        {
            Interlocked.Exchange(ref _snapshot, snapshot);
            TransferDisplayCode = snapshot.Transfer.DisplayCode; AdjustmentDisplayCode = snapshot.Adjustment.DisplayCode;
            TrackingStatus = $"会话 {snapshot.SessionId}，快照 {snapshot.Version}，身份诊断 {snapshot.Diagnostic}";
            RaisePropertyChanged(nameof(StatusText));
        }
        public void Unavailable(string reason, bool connected)
        {
            Interlocked.Exchange(ref _snapshot, null);
            TransferDisplayCode = AdjustmentDisplayCode = connected ? "待确认" : "通信异常";
            TrackingStatus = reason; RaisePropertyChanged(nameof(StatusText));
        }
    }
}
