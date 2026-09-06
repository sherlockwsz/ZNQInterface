using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ZNQInterface.Models.Axes;
using ZNQInterface.Services.Communication.Ads;
using ZNQInterface.ViewModels.Components.Axes;

namespace ZNQInterface.Services.Axes
{
    /// <summary>
    /// 通过ADS Sum命令监控全部轴。
    /// 快速批次跟踪运动、命令结果和报警；慢速批次读取设置值及
    /// GVL_AxisConfig.AxisLimits，避免数百次独立ADS往返。
    /// </summary>
    public sealed class AxisMonitoringService
    {
        private static readonly TimeSpan SlowPollInterval =
            TimeSpan.FromMilliseconds(250);

        private readonly IAdsConnectionService _ads;
        private readonly IReadOnlyList<AxisItemViewModel> _allAxes;
        private readonly IReadOnlyList<AxisItemViewModel> _monitoredAxes;
        private readonly IReadOnlyList<AxisReadBinding> _fastBindings;
        private readonly IReadOnlyList<AxisReadBinding> _slowBindings;
        private readonly Dictionary<AxisId, bool> _fastHealthy;
        private readonly Dictionary<AxisId, bool> _slowHealthy;

        private CancellationTokenSource _cancellation;
        private Task _monitorTask;

        public AxisMonitoringService(
            IAdsConnectionService ads,
            AxisStatusViewModel axisStatus)
        {
            _ads = ads ?? throw new ArgumentNullException(nameof(ads));

            AxisStatusViewModel status = axisStatus
                ?? throw new ArgumentNullException(nameof(axisStatus));

            _allAxes = status.AllAxes.ToArray();
            _monitoredAxes = _allAxes
                .Where(axis => axis.Definition.IsExpectedConfigured)
                .ToArray();

            _fastHealthy = _monitoredAxes.ToDictionary(
                axis => axis.Definition.AxisId,
                axis => false);
            _slowHealthy = _monitoredAxes.ToDictionary(
                axis => axis.Definition.AxisId,
                axis => false);

            _fastBindings = BuildFastBindings(_monitoredAxes);
            _slowBindings = BuildSlowBindings(_allAxes);

            if (_fastBindings.Count > 500 || _slowBindings.Count > 500)
            {
                throw new InvalidOperationException(
                    "轴监控批量变量数量超过ADS Sum命令安全上限。" +
                    $"快速={_fastBindings.Count}，慢速={_slowBindings.Count}。");
            }
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
            ResetBatchHealth();
            await SetAllCommunicationOkAsync(false).ConfigureAwait(false);
        }

        private async Task MonitorLoopAsync(
            CancellationToken cancellationToken)
        {
            DateTime nextSlowReadUtc = DateTime.MinValue;

            while (!cancellationToken.IsCancellationRequested)
            {
                if (!_ads.IsConnected)
                {
                    ResetBatchHealth();
                    nextSlowReadUtc = DateTime.MinValue;
                    await SetAllCommunicationOkAsync(false)
                        .ConfigureAwait(false);
                    await Task.Delay(
                            _ads.Options.PollInterval,
                            cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                try
                {
                    DateTime nowUtc = DateTime.UtcNow;
                    if (nowUtc >= nextSlowReadUtc)
                    {
                        await ReadAndApplyAsync(
                                _slowBindings,
                                isFastBatch: false,
                                cancellationToken)
                            .ConfigureAwait(false);
                        nextSlowReadUtc = nowUtc + SlowPollInterval;
                    }

                    await ReadAndApplyAsync(
                            _fastBindings,
                            isFastBatch: true,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // AdsConnectionService已将传输异常切换为Faulted并启动重连。
                    ResetBatchHealth();
                    await SetAllCommunicationOkAsync(false)
                        .ConfigureAwait(false);
                }

                await Task.Delay(
                        _ads.Options.PollInterval,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        private async Task ReadAndApplyAsync(
            IReadOnlyList<AxisReadBinding> bindings,
            bool isFastBatch,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<AdsReadRequest> requests = bindings
                .Select(binding => binding.Request)
                .ToArray();

            IReadOnlyList<object> values = await _ads.ReadManyAsync(
                    requests,
                    cancellationToken)
                .ConfigureAwait(false);

            if (values.Count != bindings.Count)
            {
                throw new InvalidOperationException(
                    "ADS批量读取返回数量与请求数量不一致。");
            }

            await RunOnUiThreadAsync(() =>
            {
                for (int index = 0; index < bindings.Count; index++)
                {
                    bindings[index].Apply(values[index]);
                }

                Dictionary<AxisId, bool> health = isFastBatch
                    ? _fastHealthy
                    : _slowHealthy;

                foreach (AxisItemViewModel axis in _monitoredAxes)
                {
                    health[axis.Definition.AxisId] = true;
                    bool communicationOk =
                        _fastHealthy[axis.Definition.AxisId] &&
                        _slowHealthy[axis.Definition.AxisId] &&
                        axis.Runtime.IsConfigurationKnown &&
                        axis.Runtime.IsConfigured;

                    axis.Runtime.IsCommunicationOk = communicationOk;

                    if (isFastBatch && communicationOk)
                    {
                        axis.InitializeCommandParameters(
                            axis.Runtime.ActualPosition,
                            axis.Runtime.PositionVelocitySetting,
                            axis.Runtime.JogVelocitySetting);
                    }
                }
            }).ConfigureAwait(false);
        }

        private static IReadOnlyList<AxisReadBinding> BuildFastBindings(
            IReadOnlyList<AxisItemViewModel> axes)
        {
            List<AxisReadBinding> result = new List<AxisReadBinding>();

            foreach (AxisItemViewModel axis in axes)
            {
                string prefix = axis.Definition.AdsSymbolPrefix;

                Add<double>(result, axis,
                    AdsAxisSymbols.Data(prefix, "fSetPosition"),
                    (runtime, value) => runtime.SetPosition = value);
                Add<double>(result, axis,
                    AdsAxisSymbols.Data(prefix, "fActPosition"),
                    (runtime, value) => runtime.ActualPosition = value);
                Add<double>(result, axis,
                    AdsAxisSymbols.Data(prefix, "fTargetPosition"),
                    (runtime, value) => runtime.TargetPosition = value);
                Add<double>(result, axis,
                    AdsAxisSymbols.Data(prefix, "fSetVelocity"),
                    (runtime, value) => runtime.SetVelocity = value);
                Add<double>(result, axis,
                    AdsAxisSymbols.Data(prefix, "fActVelocity"),
                    (runtime, value) => runtime.ActualVelocity = value);
                Add<double>(result, axis,
                    AdsAxisSymbols.Data(prefix, "fFollowingError"),
                    (runtime, value) => runtime.FollowingError = value);
                Add<double>(result, axis,
                    AdsAxisSymbols.Data(prefix, "fActTorque"),
                    (runtime, value) => runtime.ActualTorque = value);

                Add<bool>(result, axis,
                    AdsAxisSymbols.State(prefix, "bPowerStatus"),
                    (runtime, value) => runtime.IsEnabled = value);
                Add<bool>(result, axis,
                    AdsAxisSymbols.State(prefix, "bHomed"),
                    (runtime, value) => runtime.IsHomed = value);
                Add<bool>(result, axis,
                    AdsAxisSymbols.State(prefix, "bBusy"),
                    (runtime, value) => runtime.IsBusy = value);
                Add<bool>(result, axis,
                    AdsAxisSymbols.State(prefix, "bActive"),
                    (runtime, value) => runtime.IsActive = value);
                Add<bool>(result, axis,
                    AdsAxisSymbols.State(prefix, "bDone"),
                    (runtime, value) => runtime.CommandDone = value);
                Add<bool>(result, axis,
                    AdsAxisSymbols.State(prefix, "bCommandAborted"),
                    (runtime, value) => runtime.CommandAborted = value);
                Add<bool>(result, axis,
                    AdsAxisSymbols.State(prefix, "bCmdRejected"),
                    (runtime, value) => runtime.CommandRejected = value);
                Add<short>(result, axis,
                    AdsAxisSymbols.State(prefix, "eMotionState"),
                    ApplyMotionState);
                Add<short>(result, axis,
                    AdsAxisSymbols.State(prefix, "eMotionDirection"),
                    ApplyMotionDirection);
                Add<bool>(result, axis,
                    AdsAxisSymbols.State(prefix, "bSoftLimitPositive"),
                    (runtime, value) => runtime.PositiveLimit = value);
                Add<bool>(result, axis,
                    AdsAxisSymbols.State(prefix, "bSoftLimitNegative"),
                    (runtime, value) => runtime.NegativeLimit = value);

                Add<bool>(result, axis,
                    AdsAxisSymbols.Alarm(prefix, "bError"),
                    (runtime, value) => runtime.HasFault = value);
                Add<uint>(result, axis,
                    AdsAxisSymbols.Alarm(prefix, "nErrorID"),
                    (runtime, value) => runtime.ErrorCode = value);
                Add<short>(result, axis,
                    AdsAxisSymbols.Alarm(prefix, "eErrorSource"),
                    (runtime, value) => runtime.ErrorSource = value);
                Add<uint>(result, axis,
                    AdsAxisSymbols.Alarm(prefix, "nWarningID"),
                    (runtime, value) => runtime.WarningCode = value);
                Add<bool>(result, axis,
                    AdsAxisSymbols.Alarm(prefix, "bWarning"),
                    (runtime, value) =>
                    {
                        if (!value)
                        {
                            runtime.WarningCode = 0;
                        }
                    });
                Add<short>(result, axis,
                    AdsAxisSymbols.Alarm(prefix, "eRejectReason"),
                    (runtime, value) => runtime.RejectReason = value);
            }

            return result;
        }

        private static IReadOnlyList<AxisReadBinding> BuildSlowBindings(
            IReadOnlyList<AxisItemViewModel> axes)
        {
            List<AxisReadBinding> result = new List<AxisReadBinding>();

            foreach (AxisItemViewModel axis in axes)
            {
                string prefix = axis.Definition.AdsSymbolPrefix;
                string limitPrefix = axis.Definition.AdsLimitSymbolPrefix;

                // 14个数组项都读取bConfigured，包括保留的7号未接入轴。
                Add<bool>(result, axis,
                    AdsAxisSymbols.Limit(limitPrefix, "bConfigured"),
                    (runtime, value) =>
                    {
                        runtime.IsConfigured = value;
                        runtime.IsConfigurationKnown = true;
                    });

                if (!axis.Definition.IsExpectedConfigured)
                {
                    continue;
                }

                Add<double>(result, axis,
                    AdsAxisSymbols.Setting(prefix, "Position", "fVelocity"),
                    (runtime, value) =>
                        runtime.PositionVelocitySetting = value);
                Add<double>(result, axis,
                    AdsAxisSymbols.Setting(prefix, "Jog", "fVelocity"),
                    (runtime, value) => runtime.JogVelocitySetting = value);
                Add<double>(result, axis,
                    AdsAxisSymbols.Data(prefix, "fSetAcceleration"),
                    (runtime, value) => runtime.SetAcceleration = value);
                Add<double>(result, axis,
                    AdsAxisSymbols.Data(prefix, "fActAcceleration"),
                    (runtime, value) => runtime.ActualAcceleration = value);
                Add<double>(result, axis,
                    AdsAxisSymbols.Data(prefix, "fSetTorque"),
                    (runtime, value) => runtime.SetTorque = value);
                Add<double>(result, axis,
                    AdsAxisSymbols.Data(prefix, "fActRpm"),
                    (runtime, value) => runtime.ActualRpm = value);

                Add<bool>(result, axis,
                    AdsAxisSymbols.State(prefix, "bReady"),
                    (runtime, value) => runtime.IsReady = value);
                Add<bool>(result, axis,
                    AdsAxisSymbols.State(prefix, "bSoftLimitEnabled"),
                    (runtime, value) => runtime.SoftLimitEnabled = value);
                Add<bool>(result, axis,
                    AdsAxisSymbols.State(prefix, "bSoftLimitReady"),
                    (runtime, value) => runtime.SoftLimitReady = value);

                Add<double>(result, axis,
                    AdsAxisSymbols.Limit(limitPrefix, "fMinVelocity"),
                    (runtime, value) => runtime.MinimumVelocity = value);
                Add<double>(result, axis,
                    AdsAxisSymbols.Limit(limitPrefix, "fMaxVelocity"),
                    (runtime, value) => runtime.MaximumVelocity = value);
                Add<double>(result, axis,
                    AdsAxisSymbols.Limit(limitPrefix, "fMinAcceleration"),
                    (runtime, value) => runtime.MinimumAcceleration = value);
                Add<double>(result, axis,
                    AdsAxisSymbols.Limit(limitPrefix, "fMaxAcceleration"),
                    (runtime, value) => runtime.MaximumAcceleration = value);
                Add<double>(result, axis,
                    AdsAxisSymbols.Limit(limitPrefix, "fMinDeceleration"),
                    (runtime, value) => runtime.MinimumDeceleration = value);
                Add<double>(result, axis,
                    AdsAxisSymbols.Limit(limitPrefix, "fMaxDeceleration"),
                    (runtime, value) => runtime.MaximumDeceleration = value);
                Add<double>(result, axis,
                    AdsAxisSymbols.Limit(limitPrefix, "fSoftwareLimitNeg"),
                    (runtime, value) => runtime.SoftwareLimitNegative = value);
                Add<double>(result, axis,
                    AdsAxisSymbols.Limit(limitPrefix, "fSoftwareLimitPos"),
                    (runtime, value) => runtime.SoftwareLimitPositive = value);
                Add<bool>(result, axis,
                    AdsAxisSymbols.Limit(limitPrefix, "bSoftLimitEnable"),
                    (runtime, value) => runtime.SoftLimitEnabled = value);
                Add<double>(result, axis,
                    AdsAxisSymbols.Limit(limitPrefix, "fSoftLimitMargin"),
                    (runtime, value) => runtime.SoftLimitMargin = value);
                Add<bool>(result, axis,
                    AdsAxisSymbols.Limit(limitPrefix, "bRequireHomed"),
                    (runtime, value) => runtime.RequireHomed = value);
            }

            return result;
        }

        private static void Add<T>(
            ICollection<AxisReadBinding> bindings,
            AxisItemViewModel axis,
            string symbolName,
            Action<AxisRuntimeData, T> apply)
        {
            bindings.Add(new AxisReadBinding(
                AdsReadRequest.Create<T>(symbolName),
                value => apply(axis.Runtime, (T)value)));
        }

        private static void ApplyMotionState(
            AxisRuntimeData runtime,
            short value)
        {
            runtime.MotionState = Enum.IsDefined(
                    typeof(AxisMotionState),
                    (int)value)
                ? (AxisMotionState)value
                : AxisMotionState.Undefined;
        }

        private static void ApplyMotionDirection(
            AxisRuntimeData runtime,
            short value)
        {
            runtime.MotionDirection = Enum.IsDefined(
                    typeof(AxisMotionDirection),
                    (int)value)
                ? (AxisMotionDirection)value
                : AxisMotionDirection.None;
        }

        private void ResetBatchHealth()
        {
            foreach (AxisId axisId in _fastHealthy.Keys.ToArray())
            {
                _fastHealthy[axisId] = false;
                _slowHealthy[axisId] = false;
            }
        }

        private void OnAdsStateChanged(
            object sender,
            AdsConnectionStateChangedEventArgs eventArgs)
        {
            if (eventArgs.State != AdsConnectionState.Connected)
            {
                _ = SetAllCommunicationOkAsync(false);
            }
        }

        private Task SetAllCommunicationOkAsync(bool isOk) =>
            RunOnUiThreadAsync(() =>
            {
                foreach (AxisItemViewModel axis in _monitoredAxes)
                {
                    axis.Runtime.IsCommunicationOk = isOk;
                }
            });

        private static Task RunOnUiThreadAsync(Action action)
        {
            if (Application.Current == null ||
                Application.Current.Dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            return Application.Current.Dispatcher.InvokeAsync(action).Task;
        }

        private sealed class AxisReadBinding
        {
            public AxisReadBinding(
                AdsReadRequest request,
                Action<object> apply)
            {
                Request = request;
                Apply = apply;
            }

            public AdsReadRequest Request { get; }

            public Action<object> Apply { get; }
        }
    }
}
