#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ZNQInterface.ViewModels.Components.Workpieces;
namespace ZNQInterface.Services.Processes
{
    public sealed class WorkpieceMonitoringService
    {
        private readonly WorkpieceAdsReader _reader;
        private readonly WorkpieceStationViewModel _vm;
        private CancellationTokenSource? _cts;
        private Task? _task;
        public WorkpieceMonitoringService(WorkpieceAdsReader reader, WorkpieceStationViewModel vm) { _reader = reader; _vm = vm; }
        private void OnConnectionChanged() => Ui(() => _vm.Unavailable("ADS连接变化，等待重新读取完整快照", _reader.IsConnected));
        private static void Ui(Action action) => Application.Current?.Dispatcher.BeginInvoke(action);
        public Task StartAsync()
        {
            if (_task != null && !_task.IsCompleted) return Task.CompletedTask;
            _cts = new CancellationTokenSource(); _reader.ConnectionChanged += OnConnectionChanged;
            var ct = _cts.Token; _task = Task.Run(() => RunAsync(ct)); return Task.CompletedTask;
        }
        public void Stop() { _reader.ConnectionChanged -= OnConnectionChanged; _cts?.Cancel(); }
        private async Task RunAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        var snapshot = await _reader.ReadStationsAsync(ct).ConfigureAwait(false);
                        Ui(() => {
                            if (!ct.IsCancellationRequested && _reader.IsConnected && snapshot.ConnectionEpoch == _reader.Epoch) _vm.Apply(snapshot);
                            else _vm.Unavailable("ADS连接变化，等待重读", _reader.IsConnected);
                        });
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                    catch (Exception e) { Ui(() => _vm.Unavailable(e.Message, _reader.IsConnected)); }
                    await Task.Delay(100, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        }
    }
}
