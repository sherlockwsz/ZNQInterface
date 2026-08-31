using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ZNQInterface.Models.Axes;
using ZNQInterface.ViewModels.Components.Axes;

namespace ZNQInterface.Communication.Ads
{
    /// <summary>
    /// 以 50 ms 周期读取唯一已映射的 Axis1，
    /// 并把 PLC 反馈写入阻尼器上下料 X 轴的共享 Runtime。
    /// Overview 和 ManualControl 因此看到同一份实时数据。
    /// </summary>
    public sealed class AxisMonitoringService
    {
        private readonly IAdsConnectionService _ads;
        private readonly AxisItemViewModel _axis;
        private CancellationTokenSource _cancellation;
        private Task _monitorTask;

        public AxisMonitoringService(
            IAdsConnectionService ads,
            AxisStatusViewModel axisStatus)
        {
            _ads = ads ?? throw new ArgumentNullException(nameof(ads));
            _axis = (axisStatus ?? throw new ArgumentNullException(nameof(axisStatus)))
                .DamperXAxis;
        }

        public Task StartAsync()
        {
            if (_monitorTask != null)
            {
                return Task.CompletedTask;
            }

            _ads.StateChanged += OnAdsStateChanged;
            _cancellation = new CancellationTokenSource();
            _monitorTask = Task.Run(
                () => MonitorLoopAsync(_cancellation.Token));
            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            _ads.StateChanged -= OnAdsStateChanged;
            _cancellation?.Cancel();

            if (_monitorTask != null)
            {
                try
                {
                    await _monitorTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }

            _monitorTask = null;
            _cancellation?.Dispose();
            _cancellation = null;
            await SetCommunicationOkAsync(false).ConfigureAwait(false);
        }

        private async Task MonitorLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!_ads.IsConnected)
                {
                    await SetCommunicationOkAsync(false).ConfigureAwait(false);
                    await Task.Delay(
                        _ads.Options.PollInterval,
                        cancellationToken).ConfigureAwait(false);
                    continue;
                }

                try
                {
                    PlcAxisSnapshot snapshot =
                        await ReadSnapshotAsync(cancellationToken)
                            .ConfigureAwait(false);

                    await ApplySnapshotAsync(snapshot)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // AdsConnectionService 已记录异常并触发重连。
                    await SetCommunicationOkAsync(false).ConfigureAwait(false);
                }

                await Task.Delay(
                    _ads.Options.PollInterval,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<PlcAxisSnapshot> ReadSnapshotAsync(
            CancellationToken cancellationToken)
        {
            string prefix = _axis.Definition.AdsSymbolPrefix;
            string limitPrefix = _axis.Definition.AdsLimitSymbolPrefix;

            return new PlcAxisSnapshot
            {
                PositionVelocity = await ReadSettingAsync<double>(
                    prefix, "Position", "fVelocity", cancellationToken),
                JogVelocity = await ReadSettingAsync<double>(
                    prefix, "Jog", "fVelocity", cancellationToken),

                SetPosition = await ReadDataAsync<double>(prefix, "fSetPosition", cancellationToken),
                ActualPosition = await ReadDataAsync<double>(prefix, "fActPosition", cancellationToken),
                TargetPosition = await ReadDataAsync<double>(prefix, "fTargetPosition", cancellationToken),
                SetVelocity = await ReadDataAsync<double>(prefix, "fSetVelocity", cancellationToken),
                ActualVelocity = await ReadDataAsync<double>(prefix, "fActVelocity", cancellationToken),
                SetAcceleration = await ReadDataAsync<double>(prefix, "fSetAcceleration", cancellationToken),
                ActualAcceleration = await ReadDataAsync<double>(prefix, "fActAcceleration", cancellationToken),
                FollowingError = await ReadDataAsync<double>(prefix, "fFollowingError", cancellationToken),
                SetTorque = await ReadDataAsync<double>(prefix, "fSetTorque", cancellationToken),
                ActualTorque = await ReadDataAsync<double>(prefix, "fActTorque", cancellationToken),
                ActualRpm = await ReadDataAsync<double>(prefix, "fActRpm", cancellationToken),

                Ready = await ReadStateAsync<bool>(prefix, "bReady", cancellationToken),
                PowerStatus = await ReadStateAsync<bool>(prefix, "bPowerStatus", cancellationToken),
                Homed = await ReadStateAsync<bool>(prefix, "bHomed", cancellationToken),
                Busy = await ReadStateAsync<bool>(prefix, "bBusy", cancellationToken),
                Active = await ReadStateAsync<bool>(prefix, "bActive", cancellationToken),
                Done = await ReadStateAsync<bool>(prefix, "bDone", cancellationToken),
                CommandAborted = await ReadStateAsync<bool>(prefix, "bCommandAborted", cancellationToken),
                CommandRejected = await ReadStateAsync<bool>(prefix, "bCmdRejected", cancellationToken),
                MotionState = await ReadStateAsync<short>(prefix, "eMotionState", cancellationToken),
                SoftLimitEnabled = await ReadStateAsync<bool>(prefix, "bSoftLimitEnabled", cancellationToken),
                SoftLimitReady = await ReadStateAsync<bool>(prefix, "bSoftLimitReady", cancellationToken),
                SoftLimitPositive = await ReadStateAsync<bool>(prefix, "bSoftLimitPositive", cancellationToken),
                SoftLimitNegative = await ReadStateAsync<bool>(prefix, "bSoftLimitNegative", cancellationToken),

                Error = await ReadAlarmAsync<bool>(prefix, "bError", cancellationToken),
                ErrorId = await ReadAlarmAsync<uint>(prefix, "nErrorID", cancellationToken),
                ErrorSource = await ReadAlarmAsync<short>(prefix, "eErrorSource", cancellationToken),
                Warning = await ReadAlarmAsync<bool>(prefix, "bWarning", cancellationToken),
                WarningId = await ReadAlarmAsync<uint>(prefix, "nWarningID", cancellationToken),
                RejectReason = await ReadAlarmAsync<short>(prefix, "eRejectReason", cancellationToken),

                SoftwareLimitNegative = await _ads.ReadAsync<double>(
                    AdsAxisSymbols.Limit(limitPrefix, "fSoftwareLimitNeg"), cancellationToken),
                SoftwareLimitPositive = await _ads.ReadAsync<double>(
                    AdsAxisSymbols.Limit(limitPrefix, "fSoftwareLimitPos"), cancellationToken)
            };
        }

        private Task<T> ReadSettingAsync<T>(
            string prefix,
            string group,
            string member,
            CancellationToken cancellationToken) =>
            _ads.ReadAsync<T>(
                AdsAxisSymbols.Setting(prefix, group, member),
                cancellationToken);

        private Task<T> ReadDataAsync<T>(
            string prefix,
            string member,
            CancellationToken cancellationToken) =>
            _ads.ReadAsync<T>(
                AdsAxisSymbols.Data(prefix, member),
                cancellationToken);

        private Task<T> ReadStateAsync<T>(
            string prefix,
            string member,
            CancellationToken cancellationToken) =>
            _ads.ReadAsync<T>(
                AdsAxisSymbols.State(prefix, member),
                cancellationToken);

        private Task<T> ReadAlarmAsync<T>(
            string prefix,
            string member,
            CancellationToken cancellationToken) =>
            _ads.ReadAsync<T>(
                AdsAxisSymbols.Alarm(prefix, member),
                cancellationToken);

        private async Task ApplySnapshotAsync(PlcAxisSnapshot snapshot)
        {
            await RunOnUiThreadAsync(() =>
            {
                AxisRuntimeData runtime = _axis.Runtime;
                /*
                 * NC每周期轨迹数据。
                 * SetPosition/SetVelocity不是操作员输入值，
                 * 而是NC轨迹发生器当前计算出的给定值。
                 */
                runtime.SetPosition =
                    snapshot.SetPosition;

                runtime.ActualPosition =
                    snapshot.ActualPosition;

                runtime.TargetPosition =
                    snapshot.TargetPosition;

                runtime.SetVelocity =
                    snapshot.SetVelocity;

                runtime.ActualVelocity =
                    snapshot.ActualVelocity;

                /*
                 * 操作员运动参数。
                 * 这两个值分别来自PLC的Position和Jog参数区。
                 */
                runtime.PositionVelocitySetting =
                    snapshot.PositionVelocity;

                runtime.JogVelocitySetting = snapshot.JogVelocity; 
                runtime.SetAcceleration = snapshot.SetAcceleration;
                runtime.ActualAcceleration = snapshot.ActualAcceleration;
                runtime.FollowingError = snapshot.FollowingError;
                runtime.SetTorque = snapshot.SetTorque;
                runtime.ActualTorque = snapshot.ActualTorque;
                runtime.ActualRpm = snapshot.ActualRpm;

                runtime.IsReady = snapshot.Ready;
                runtime.IsEnabled = snapshot.PowerStatus;
                runtime.IsHomed = snapshot.Homed;
                runtime.IsBusy = snapshot.Busy;
                runtime.IsActive = snapshot.Active;
                runtime.CommandDone = snapshot.Done;
                runtime.CommandAborted = snapshot.CommandAborted;
                runtime.CommandRejected = snapshot.CommandRejected;
                runtime.MotionState = Enum.IsDefined(
                        typeof(AxisMotionState),
                        (int)snapshot.MotionState)
                    ? (AxisMotionState)snapshot.MotionState
                    : AxisMotionState.Undefined;
                runtime.SoftLimitEnabled = snapshot.SoftLimitEnabled;
                runtime.SoftLimitReady = snapshot.SoftLimitReady;
                runtime.PositiveLimit = snapshot.SoftLimitPositive;
                runtime.NegativeLimit = snapshot.SoftLimitNegative;

                runtime.HasFault = snapshot.Error;
                runtime.ErrorCode = snapshot.ErrorId;
                runtime.WarningCode = snapshot.Warning ? snapshot.WarningId : 0;
                runtime.ErrorSource = snapshot.ErrorSource;
                runtime.RejectReason = snapshot.RejectReason;
                runtime.SoftwareLimitNegative = snapshot.SoftwareLimitNegative;
                runtime.SoftwareLimitPositive = snapshot.SoftwareLimitPositive;
                runtime.IsCommunicationOk = true;
                /*
                 * 首次通信成功后：
                 * 目标位置使用PLC当前实际位置，
                 * 两个速度使用PLC当前设置参数。
                 */
                _axis.InitializeCommandParameters(
                    snapshot.ActualPosition,
                    snapshot.PositionVelocity,
                    snapshot.JogVelocity);
            }).ConfigureAwait(false);
        }

        private Task SetCommunicationOkAsync(bool isOk) =>
            RunOnUiThreadAsync(() =>
                _axis.Runtime.IsCommunicationOk = isOk);

        private static Task RunOnUiThreadAsync(Action action)
        {
            if (Application.Current == null ||
                Application.Current.Dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            return Application.Current.Dispatcher
                .InvokeAsync(action)
                .Task;
        }

        private void OnAdsStateChanged(
            object sender,
            AdsConnectionStateChangedEventArgs eventArgs)
        {
            if (eventArgs.State != AdsConnectionState.Connected)
            {
                _ = SetCommunicationOkAsync(false);
            }
        }
    }
}
