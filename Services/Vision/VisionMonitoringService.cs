using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ZNQInterface.Services.Communication.Ads;
using ZNQInterface.ViewModels.Components.Detection;

namespace ZNQInterface.Services.Vision
{
    /// <summary>
    /// 协调双路Preview、ResultId匹配的检测图以及PLC ADS权威数据。
    /// TCP异常与ADS检测事务完全隔离。
    /// </summary>
    public sealed class VisionMonitoringService
    {
        private readonly IAdsConnectionService _ads;
        private readonly ScrewAngleMonitorViewModel _screwMonitor;
        private readonly CoaxialityMonitorViewModel _coaxMonitor;
        private readonly VisionMonitoringOptions _options;
        private readonly IReadOnlyList<AdsReadRequest> _adsRequests;
        private readonly ChannelRuntime _screwRuntime = new ChannelRuntime();
        private readonly ChannelRuntime _coaxRuntime = new ChannelRuntime();

        private CancellationTokenSource? _lifetime;
        private Task[]? _tasks;
        private long _lastScrewResultId = -1;
        private long _lastCoaxResultId = -1;

        public VisionMonitoringService(
            IAdsConnectionService ads,
            ScrewAngleMonitorViewModel screwMonitor,
            CoaxialityMonitorViewModel coaxMonitor,
            VisionMonitoringOptions options)
        {
            _ads = ads;
            _screwMonitor = screwMonitor;
            _coaxMonitor = coaxMonitor;
            _options = options;
            _adsRequests = CreateAdsRequests();
        }

        public Task StartAsync()
        {
            if (_tasks != null)
            {
                return Task.CompletedTask;
            }

            _lifetime = new CancellationTokenSource();
            CancellationToken token = _lifetime.Token;
            _tasks = new[]
            {
                Task.Run(() => RunAdsLoopAsync(token), token),
                Task.Run(() => RunPreviewLoopAsync(
                    "screw_preview", true, _screwRuntime, token), token),
                Task.Run(() => RunPreviewLoopAsync(
                    "coax_preview", false, _coaxRuntime, token), token),
                Task.Run(() => RunDetectionLoopAsync(
                    "screw", true, _screwRuntime, token), token),
                Task.Run(() => RunDetectionLoopAsync(
                    "coax", false, _coaxRuntime, token), token)
            };
            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            CancellationTokenSource? lifetime = _lifetime;
            Task[]? tasks = _tasks;
            _lifetime = null;
            _tasks = null;
            if (lifetime == null)
            {
                return;
            }

            lifetime.Cancel();
            if (tasks != null)
            {
                try
                {
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
            lifetime.Dispose();
        }

        /// <summary>
        /// UI线程退出路径只发出取消信号，避免同步等待Dispatcher更新形成互等。
        /// </summary>
        public void Stop()
        {
            _lifetime?.Cancel();
        }

        private static IReadOnlyList<AdsReadRequest> CreateAdsRequests() =>
            new AdsReadRequest[]
            {
                AdsReadRequest.Create<bool>(VisionSymbols.ScrewMeasureRequest),
                AdsReadRequest.Create<uint>(VisionSymbols.ScrewRequestId),
                AdsReadRequest.Create<bool>(VisionSymbols.ScrewResultValid),
                AdsReadRequest.Create<uint>(VisionSymbols.ScrewResultId),
                AdsReadRequest.Create<double>(VisionSymbols.ScrewDetectedAngle),
                AdsReadRequest.Create<bool>(VisionSymbols.ScrewDetected),
                AdsReadRequest.Create<bool>(VisionSymbols.ScrewResultInvalid),
                AdsReadRequest.Create<bool>(VisionSymbols.ScrewServiceFault),
                AdsReadRequest.Create<bool>(VisionSymbols.ScrewTimeout),

                AdsReadRequest.Create<bool>(VisionSymbols.CoaxMeasureRequest),
                AdsReadRequest.Create<uint>(VisionSymbols.CoaxRequestId),
                AdsReadRequest.Create<bool>(VisionSymbols.CoaxResultValid),
                AdsReadRequest.Create<uint>(VisionSymbols.CoaxResultId),
                AdsReadRequest.Create<double>(VisionSymbols.Coaxiality),
                AdsReadRequest.Create<double>(VisionSymbols.CoaxDeltaX),
                AdsReadRequest.Create<double>(VisionSymbols.CoaxDeltaY),
                AdsReadRequest.Create<bool>(VisionSymbols.CoaxResultInvalid),
                AdsReadRequest.Create<bool>(VisionSymbols.CoaxServiceFault),
                AdsReadRequest.Create<bool>(VisionSymbols.CoaxTimeout),

                AdsReadRequest.Create<bool>(VisionSymbols.AdjustmentDone),
                AdsReadRequest.Create<bool>(VisionSymbols.AdjustmentQualified),
                AdsReadRequest.Create<bool>(VisionSymbols.AdjustmentVisionFault)
            };

        private async Task RunAdsLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_ads.IsConnected)
                    {
                        IReadOnlyList<object> values = await _ads.ReadManyAsync(
                            _adsRequests,
                            cancellationToken).ConfigureAwait(false);
                        if (values.Count == _adsRequests.Count)
                        {
                            await ApplyAdsSnapshotAsync(values)
                                .ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        await SetAdsUnavailableAsync().ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    await SetAdsUnavailableAsync().ConfigureAwait(false);
                }

                await Task.Delay(
                    _options.AdsPollIntervalMs,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        private Task ApplyAdsSnapshotAsync(IReadOnlyList<object> values)
        {
            bool screwMeasuring = Convert.ToBoolean(values[0]);
            long screwRequestId = Convert.ToUInt32(values[1]);
            bool screwValid = Convert.ToBoolean(values[2]);
            long screwResultId = Convert.ToUInt32(values[3]);
            double screwAngle = Convert.ToDouble(values[4]);
            bool screwDetected = Convert.ToBoolean(values[5]);
            bool screwInvalid = Convert.ToBoolean(values[6]);
            bool screwFault = Convert.ToBoolean(values[7]);
            bool screwTimeout = Convert.ToBoolean(values[8]);

            bool coaxMeasuring = Convert.ToBoolean(values[9]);
            long coaxRequestId = Convert.ToUInt32(values[10]);
            bool coaxValid = Convert.ToBoolean(values[11]);
            long coaxResultId = Convert.ToUInt32(values[12]);
            double coaxiality = Convert.ToDouble(values[13]);
            double deltaX = Convert.ToDouble(values[14]);
            double deltaY = Convert.ToDouble(values[15]);
            bool coaxInvalid = Convert.ToBoolean(values[16]);
            bool coaxFault = Convert.ToBoolean(values[17]);
            bool coaxTimeout = Convert.ToBoolean(values[18]);
            bool adjustmentDone = Convert.ToBoolean(values[19]);
            bool adjustmentQualified = Convert.ToBoolean(values[20]);
            bool adjustmentVisionFault = Convert.ToBoolean(values[21]);

            bool screwCommitted =
                screwValid && screwResultId == screwRequestId;
            bool coaxCommitted =
                coaxValid && coaxResultId == coaxRequestId;

            if (screwCommitted && screwResultId != _lastScrewResultId)
            {
                _lastScrewResultId = screwResultId;
                _screwRuntime.SetDetectionTarget(screwResultId);
            }
            if (coaxCommitted && coaxResultId != _lastCoaxResultId)
            {
                _lastCoaxResultId = coaxResultId;
                _coaxRuntime.SetDetectionTarget(coaxResultId);
            }

            return RunOnUiAsync(() =>
            {
                if (screwCommitted)
                {
                    bool resultInvalid =
                        screwInvalid || screwFault || screwTimeout || !screwDetected;
                    _screwMonitor.CurrentAngle = resultInvalid ? null : screwAngle;
                    _screwMonitor.DetectionStatusText = resultInvalid
                        ? "检测无效 / 视觉异常"
                        : "检测完成";
                }
                else if (screwMeasuring)
                {
                    _screwMonitor.DetectionStatusText = "检测中";
                }
                else if (_lastScrewResultId < 0)
                {
                    _screwMonitor.DetectionStatusText = "未检测";
                }

                if (coaxCommitted)
                {
                    bool resultInvalid =
                        coaxInvalid || coaxFault || coaxTimeout || adjustmentVisionFault;
                    if (resultInvalid)
                    {
                        _coaxMonitor.XDeviation = null;
                        _coaxMonitor.YDeviation = null;
                        _coaxMonitor.Coaxiality = null;
                        _coaxMonitor.IsQualified = null;
                        _coaxMonitor.DetectionStatusText =
                            "检测无效 / 视觉异常";
                    }
                    else
                    {
                        _coaxMonitor.XDeviation = deltaX;
                        _coaxMonitor.YDeviation = deltaY;
                        _coaxMonitor.Coaxiality = coaxiality;
                        if (adjustmentDone)
                        {
                            _coaxMonitor.IsQualified = adjustmentQualified;
                            _coaxMonitor.DetectionStatusText = adjustmentQualified
                                ? "合格"
                                : "不合格";
                        }
                        else
                        {
                            _coaxMonitor.IsQualified = null;
                            _coaxMonitor.DetectionStatusText = "检测完成 / 调整中";
                        }
                    }
                }
                else if (coaxMeasuring)
                {
                    _coaxMonitor.IsQualified = null;
                    _coaxMonitor.DetectionStatusText = "检测中";
                }
                else if (_lastCoaxResultId < 0)
                {
                    _coaxMonitor.DetectionStatusText = "未检测";
                }
            });
        }

        private Task SetAdsUnavailableAsync() => RunOnUiAsync(() =>
        {
            _screwMonitor.DetectionStatusText = "ADS未连接";
            _coaxMonitor.DetectionStatusText = "ADS未连接";
        });

        private async Task RunPreviewLoopAsync(
            string channel,
            bool screw,
            ChannelRuntime runtime,
            CancellationToken cancellationToken)
        {
            await using VisionImageClient client =
                new VisionImageClient(_options.Host, _options.Port);
            int reconnectIndex = 0;
            long lastFrameId = -1;
            DateTime lastFreshUtc = DateTime.MinValue;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    VisionImageFrame frame = await client.GetLatestAsync(
                        channel,
                        cancellationToken).ConfigureAwait(false);
                    reconnectIndex = 0;

                    if (frame.Ok && frame.Image != null)
                    {
                        DateTime now = DateTime.UtcNow;
                        if (frame.FrameId != lastFrameId)
                        {
                            lastFrameId = frame.FrameId;
                            lastFreshUtc = now;
                        }

                        bool showPreview = runtime.UpdatePreview(frame);
                        bool stale = lastFreshUtc != DateTime.MinValue &&
                            now - lastFreshUtc > TimeSpan.FromMilliseconds(
                                _options.PreviewStaleTimeoutMs);
                        string status = frame.ImageStatus == "error"
                            ? "图像异常"
                            : stale ? "图像延迟" : "图像正常";
                        await ApplyPreviewAsync(
                            screw,
                            frame,
                            showPreview,
                            status).ConfigureAwait(false);
                    }
                    else
                    {
                        await SetImageStatusAsync(screw, "等待图像")
                            .ConfigureAwait(false);
                    }

                    await Task.Delay(
                        _options.PreviewIntervalMs,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    await SetImageStatusAsync(screw, "图像连接异常")
                        .ConfigureAwait(false);
                    int delay = _options.ReconnectBackoffMs[Math.Min(
                        reconnectIndex,
                        _options.ReconnectBackoffMs.Length - 1)];
                    reconnectIndex++;
                    await Task.Delay(delay, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }

        private async Task RunDetectionLoopAsync(
            string channel,
            bool screw,
            ChannelRuntime runtime,
            CancellationToken cancellationToken)
        {
            await using VisionImageClient client =
                new VisionImageClient(_options.Host, _options.Port);
            long attemptedResultId = -1;

            while (!cancellationToken.IsCancellationRequested)
            {
                long target = runtime.DetectionTarget;
                if (target >= 0 && target != attemptedResultId)
                {
                    bool completed = await TryShowDetectionAsync(
                        client,
                        channel,
                        screw,
                        runtime,
                        target,
                        cancellationToken).ConfigureAwait(false);
                    if (completed || runtime.DetectionTarget == target)
                    {
                        attemptedResultId = target;
                    }
                    continue;
                }

                if (runtime.TryExpireDetection(out VisionImageFrame? preview) &&
                    preview?.Image != null)
                {
                    await RestorePreviewAsync(screw, preview)
                        .ConfigureAwait(false);
                }

                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<bool> TryShowDetectionAsync(
            VisionImageClient client,
            string channel,
            bool screw,
            ChannelRuntime runtime,
            long targetResultId,
            CancellationToken cancellationToken)
        {
            for (int attempt = 0;
                 attempt < _options.DetectionRetryCount;
                 attempt++)
            {
                if (runtime.DetectionTarget != targetResultId)
                {
                    return false;
                }

                try
                {
                    VisionImageFrame frame = await client.GetLatestAsync(
                        channel,
                        cancellationToken).ConfigureAwait(false);
                    if (frame.Ok && frame.Image != null &&
                        frame.RequestId == targetResultId &&
                        runtime.TryBeginDetectionHold(targetResultId))
                    {
                        bool displayed = false;
                        await RunOnUiAsync(() =>
                        {
                            if (runtime.DetectionTarget != targetResultId)
                            {
                                return;
                            }
                            if (screw)
                            {
                                _screwMonitor.Image = frame.Image;
                                _screwMonitor.ImageSourceText = "检测结果";
                            }
                            else
                            {
                                _coaxMonitor.Image = frame.Image;
                                _coaxMonitor.ImageSourceText = "检测结果";
                            }
                            displayed = true;
                        }).ConfigureAwait(false);
                        if (displayed &&
                            runtime.DetectionTarget == targetResultId)
                        {
                            // 停留时间从正确图像真正完成UI赋值后开始。
                            runtime.ShowDetection(
                                TimeSpan.FromMilliseconds(
                                    _options.DetectionDisplayDurationMs));
                            return true;
                        }
                        runtime.CancelDetectionHold();
                        return false;
                    }
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    // Detection图像链路失败不影响已提交的ADS正式结果。
                }

                await Task.Delay(
                    _options.DetectionRetryDelayMs,
                    cancellationToken).ConfigureAwait(false);
            }
            return true;
        }

        private Task ApplyPreviewAsync(
            bool screw,
            VisionImageFrame frame,
            bool showPreview,
            string status)
        {
            string source = frame.SourceMode == "file"
                ? "本地图片"
                : "实时相机";
            return RunOnUiAsync(() =>
            {
                if (screw)
                {
                    if (showPreview)
                    {
                        _screwMonitor.Image = frame.Image;
                        _screwMonitor.ImageSourceText = source;
                    }
                    _screwMonitor.ImageStatusText = status;
                }
                else
                {
                    if (showPreview)
                    {
                        _coaxMonitor.Image = frame.Image;
                        _coaxMonitor.ImageSourceText = source;
                    }
                    _coaxMonitor.ImageStatusText = status;
                }
            });
        }

        private Task SetImageStatusAsync(bool screw, string status) =>
            RunOnUiAsync(() =>
            {
                if (screw)
                {
                    _screwMonitor.ImageStatusText = status;
                }
                else
                {
                    _coaxMonitor.ImageStatusText = status;
                }
            });

        private Task RestorePreviewAsync(
            bool screw,
            VisionImageFrame frame)
        {
            string source = frame.SourceMode == "file"
                ? "本地图片"
                : "实时相机";
            return RunOnUiAsync(() =>
            {
                if (screw)
                {
                    _screwMonitor.Image = frame.Image;
                    _screwMonitor.ImageSourceText = source;
                }
                else
                {
                    _coaxMonitor.Image = frame.Image;
                    _coaxMonitor.ImageSourceText = source;
                }
            });
        }

        private static Task RunOnUiAsync(Action action)
        {
            Application? application = Application.Current;
            if (application == null || application.Dispatcher.HasShutdownStarted)
            {
                return Task.CompletedTask;
            }
            return application.Dispatcher.InvokeAsync(action).Task;
        }

        private sealed class ChannelRuntime
        {
            private readonly object _sync = new object();
            private long _detectionTarget = -1;
            private DateTime _detectionVisibleUntilUtc = DateTime.MinValue;
            private VisionImageFrame? _latestPreview;

            public long DetectionTarget =>
                Interlocked.Read(ref _detectionTarget);

            public void SetDetectionTarget(long resultId) =>
                Interlocked.Exchange(ref _detectionTarget, resultId);

            public bool UpdatePreview(VisionImageFrame frame)
            {
                lock (_sync)
                {
                    _latestPreview = frame;
                    return DateTime.UtcNow >= _detectionVisibleUntilUtc;
                }
            }

            public void ShowDetection(TimeSpan duration)
            {
                lock (_sync)
                {
                    _detectionVisibleUntilUtc = DateTime.UtcNow + duration;
                }
            }

            public bool TryBeginDetectionHold(long resultId)
            {
                lock (_sync)
                {
                    if (Interlocked.Read(ref _detectionTarget) != resultId)
                    {
                        return false;
                    }
                    _detectionVisibleUntilUtc = DateTime.MaxValue;
                    return true;
                }
            }

            public void CancelDetectionHold()
            {
                lock (_sync)
                {
                    _detectionVisibleUntilUtc = DateTime.MinValue;
                }
            }

            public bool TryExpireDetection(out VisionImageFrame? preview)
            {
                lock (_sync)
                {
                    if (_detectionVisibleUntilUtc == DateTime.MinValue ||
                        DateTime.UtcNow < _detectionVisibleUntilUtc)
                    {
                        preview = null;
                        return false;
                    }

                    _detectionVisibleUntilUtc = DateTime.MinValue;
                    preview = _latestPreview;
                    return true;
                }
            }
        }
    }
}
