using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZNQInterface.Communication.Ads;
using ZNQInterface.Models.Axes;
using ZNQInterface.ViewModels.Components.Axes;

namespace ZNQInterface.Services.Axes
{
    /// <summary>
    /// 将上位机命令映射到 GVL_AxisRuntime.Axis1。
    /// bEnable 是保持量；复位、停止和定位命令是 100 ms 脉冲；
    /// 点动命令在按下时保持 TRUE，松开时立即写回 FALSE。
    /// </summary>
    public sealed class AxisCommandService : IAxisCommandService
    {
        private readonly IAdsConnectionService _ads;
        private readonly AxisStatusViewModel _axisStatus;
        private readonly SemaphoreSlim _commandGate = new SemaphoreSlim(1, 1);

        public AxisCommandService(
            IAdsConnectionService ads,
            AxisStatusViewModel axisStatus)
        {
            _ads = ads ?? throw new ArgumentNullException(nameof(ads));
            _axisStatus = axisStatus ?? throw new ArgumentNullException(nameof(axisStatus));
            _ads.StateChanged += OnAdsStateChanged;
        }

        /// <summary>
        /// 重连成功后清除可能因上次断线而未能复位的脉冲/点动位。
        /// 本次不启用 PLC 看门狗，因此该恢复动作尤其重要。
        /// </summary>
        private void OnAdsStateChanged(
            object sender,
            AdsConnectionStateChangedEventArgs eventArgs)
        {
            if (eventArgs.State == AdsConnectionState.Connected)
            {
                _ = ReleaseAllMotionSignalsAsync();
            }
        }

        public Task SetEnableAsync(
            AxisId axisId,
            bool enable,
            CancellationToken cancellationToken = default) =>
            WriteCommandAsync(axisId, "bEnable", enable, cancellationToken);

        public Task ResetAsync(
            AxisId axisId,
            CancellationToken cancellationToken = default) =>
            PulseCommandAsync(axisId, "bReset", cancellationToken);

        public async Task StopAsync(
            AxisId axisId,
            CancellationToken cancellationToken = default)
        {
            AxisItemViewModel axis = GetMappedAxis(axisId);
            await _commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                string prefix = axis.Definition.AdsSymbolPrefix;

                // 先释放持续型点动位，再触发停止脉冲。
                await _ads.WriteAsync(
                    AdsAxisSymbols.Command(prefix, "bJogPos"),
                    false,
                    cancellationToken).ConfigureAwait(false);
                await _ads.WriteAsync(
                    AdsAxisSymbols.Command(prefix, "bJogNeg"),
                    false,
                    cancellationToken).ConfigureAwait(false);
                await PulseCommandUnsafeAsync(
                    prefix,
                    "bStop",
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _commandGate.Release();
            }
        }
        /// <summary>
        /// 主动断开 ADS 前执行受控停机。
        ///
        /// 执行顺序：
        /// 1. 释放正、负点动命令；
        /// 2. 发送停止请求；
        /// 3. 撤销轴使能；
        /// 4. 清除其余运动命令。
        ///
        /// PLC的FB_AxisControl已经实现：
        /// 运动中撤销bEnable时，先保持MC_Power完成MC_Stop，
        /// 停止完成后再真正掉使能。
        /// </summary>
        public async Task StopAndDisableAsync(
            AxisId axisId,
            CancellationToken cancellationToken = default)
        {
            /*
             * ADS如果已经意外断开，WPF无法再向PLC发送停止或掉使能。
             * 这种异常断线必须由后续PLC心跳看门狗处理。
             */
            if (!_ads.IsConnected)
            {
                return;
            }

            AxisItemViewModel axis = GetMappedAxis(axisId);

            /*
             * 锁定完整操作过程，防止停止和掉使能之间
             * 插入新的点动、定位或使能命令。
             */
            await _commandGate
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            try
            {
                string prefix =
                    axis.Definition.AdsSymbolPrefix;

                /*
                 * 第一步：释放点动命令。
                 * 点动是保持型信号，必须优先清除。
                 */
                await _ads.WriteAsync(
                    AdsAxisSymbols.Command(
                        prefix,
                        "bJogPos"),
                    false,
                    cancellationToken).ConfigureAwait(false);

                await _ads.WriteAsync(
                    AdsAxisSymbols.Command(
                        prefix,
                        "bJogNeg"),
                    false,
                    cancellationToken).ConfigureAwait(false);

                /*
                 * 第二步：发送停止请求。
                 * PulseCommandUnsafeAsync内部执行：
                 * FALSE → TRUE → 保持100ms → FALSE。
                 *
                 * PLC内部bStopExecute会继续锁存，
                 * 直到MC_Stop停止完成。
                 */
                await PulseCommandUnsafeAsync(
                    prefix,
                    "bStop",
                    cancellationToken).ConfigureAwait(false);

                /*
                 * 第三步：停止请求已经送达PLC后，
                 * 再撤销轴使能请求。
                 *
                 * 如果轴仍在减速，PLC会暂时保持MC_Power；
                 * 停止完成后才真正掉使能。
                 */
                await _ads.WriteAsync(
                    AdsAxisSymbols.Command(
                        prefix,
                        "bEnable"),
                    false,
                    cancellationToken).ConfigureAwait(false);

                /*
                 * 第四步：清除其余命令位，
                 * 防止下次连接时出现遗留命令。
                 */
                foreach (string member in new[]
                         {
                     "bReset",
                     "bMoveAbs",
                     "bMoveRel",
                     "bJogPos",
                     "bJogNeg"
                 })
                {
                    await _ads.WriteAsync(
                        AdsAxisSymbols.Command(
                            prefix,
                            member),
                        false,
                        cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _commandGate.Release();
            }
        }
        public async Task MoveAbsoluteAsync(
            AxisId axisId,
            double targetPosition,
            double velocity,
            CancellationToken cancellationToken = default)
        {
            ValidateFinite(targetPosition, "目标位置");
            ValidatePositiveVelocity(velocity);
            AxisItemViewModel axis = GetMappedAxis(axisId);

            await _commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                string prefix = axis.Definition.AdsSymbolPrefix;
                await _ads.WriteAsync(
                    AdsAxisSymbols.Setting(prefix, "Position", "fAbsolutePosition"),
                    targetPosition,
                    cancellationToken).ConfigureAwait(false);
                await _ads.WriteAsync(
                    AdsAxisSymbols.Setting(prefix, "Position", "fVelocity"),
                    velocity,
                    cancellationToken).ConfigureAwait(false);
                await PulseCommandUnsafeAsync(
                    prefix, "bMoveAbs", cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _commandGate.Release();
            }
        }

        public async Task MoveRelativeAsync(
            AxisId axisId,
            double distance,
            double velocity,
            CancellationToken cancellationToken = default)
        {
            ValidateFinite(distance, "相对距离");
            ValidatePositiveVelocity(velocity);
            AxisItemViewModel axis = GetMappedAxis(axisId);

            await _commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                string prefix = axis.Definition.AdsSymbolPrefix;
                await _ads.WriteAsync(
                    AdsAxisSymbols.Setting(prefix, "Position", "fRelativeDistance"),
                    distance,
                    cancellationToken).ConfigureAwait(false);
                await _ads.WriteAsync(
                    AdsAxisSymbols.Setting(prefix, "Position", "fVelocity"),
                    velocity,
                    cancellationToken).ConfigureAwait(false);
                await PulseCommandUnsafeAsync(
                    prefix, "bMoveRel", cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _commandGate.Release();
            }
        }

        public async Task StartJogAsync(
            AxisId axisId,
            bool positiveDirection,
            double velocity,
            CancellationToken cancellationToken = default)
        {
            ValidatePositiveVelocity(velocity);
            AxisItemViewModel axis = GetMappedAxis(axisId);

            await _commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                string prefix = axis.Definition.AdsSymbolPrefix;
                await _ads.WriteAsync(
                    AdsAxisSymbols.Command(prefix, "bJogPos"),
                    false,
                    cancellationToken).ConfigureAwait(false);
                await _ads.WriteAsync(
                    AdsAxisSymbols.Command(prefix, "bJogNeg"),
                    false,
                    cancellationToken).ConfigureAwait(false);
                await _ads.WriteAsync(
                    AdsAxisSymbols.Setting(prefix, "Jog", "fVelocity"),
                    velocity,
                    cancellationToken).ConfigureAwait(false);
                await _ads.WriteAsync(
                    AdsAxisSymbols.Command(
                        prefix,
                        positiveDirection ? "bJogPos" : "bJogNeg"),
                    true,
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _commandGate.Release();
            }
        }

        public async Task StopJogAsync(
            AxisId axisId,
            CancellationToken cancellationToken = default)
        {
            AxisItemViewModel axis = GetMappedAxis(axisId);
            await _commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                string prefix = axis.Definition.AdsSymbolPrefix;
                await _ads.WriteAsync(
                    AdsAxisSymbols.Command(prefix, "bJogPos"),
                    false,
                    cancellationToken).ConfigureAwait(false);
                await _ads.WriteAsync(
                    AdsAxisSymbols.Command(prefix, "bJogNeg"),
                    false,
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _commandGate.Release();
            }
        }

        public async Task ReleaseAllMotionSignalsAsync()
        {
            if (!_ads.IsConnected)
            {
                return;
            }

            foreach (AxisItemViewModel axis in _axisStatus.AllAxes.Where(
                         item => item.Definition.IsAdsMapped))
            {
                string prefix = axis.Definition.AdsSymbolPrefix;
                foreach (string member in new[]
                         {
                             "bReset", "bStop", "bMoveAbs", "bMoveRel",
                             "bJogPos", "bJogNeg"
                         })
                {
                    try
                    {
                        await _ads.WriteAsync(
                            AdsAxisSymbols.Command(prefix, member),
                            false).ConfigureAwait(false);
                    }
                    catch
                    {
                        // 退出阶段只做尽力释放，不能阻止应用关闭。
                        return;
                    }
                }
            }
        }

        private async Task WriteCommandAsync(
            AxisId axisId,
            string member,
            bool value,
            CancellationToken cancellationToken)
        {
            AxisItemViewModel axis = GetMappedAxis(axisId);
            await _commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _ads.WriteAsync(
                    AdsAxisSymbols.Command(axis.Definition.AdsSymbolPrefix, member),
                    value,
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _commandGate.Release();
            }
        }

        private async Task PulseCommandAsync(
            AxisId axisId,
            string member,
            CancellationToken cancellationToken)
        {
            AxisItemViewModel axis = GetMappedAxis(axisId);
            await _commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await PulseCommandUnsafeAsync(
                    axis.Definition.AdsSymbolPrefix,
                    member,
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _commandGate.Release();
            }
        }

        private async Task PulseCommandUnsafeAsync(
            string prefix,
            string member,
            CancellationToken cancellationToken)
        {
            string symbol = AdsAxisSymbols.Command(prefix, member);
            await _ads.WriteAsync(symbol, false, cancellationToken)
                .ConfigureAwait(false);
            await _ads.WriteAsync(symbol, true, cancellationToken)
                .ConfigureAwait(false);

            try
            {
                await Task.Delay(
                    _ads.Options.CommandPulseWidth,
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // 即使等待被取消，也尽力恢复为 FALSE，避免命令粘住。
                await _ads.WriteAsync(symbol, false, CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }

        private AxisItemViewModel GetMappedAxis(AxisId axisId)
        {
            AxisItemViewModel axis = _axisStatus.AllAxes.FirstOrDefault(
                item => item.Definition.AxisId == axisId);

            if (axis == null || !axis.Definition.IsAdsMapped)
            {
                throw new InvalidOperationException(
                    $"轴 {axisId} 尚未配置 ADS 映射。当前仅开放 Axis1。 ");
            }

            if (!_ads.IsConnected)
            {
                throw new InvalidOperationException("ADS 尚未连接。");
            }

            return axis;
        }

        private static void ValidatePositiveVelocity(double velocity)
        {
            ValidateFinite(velocity, "速度");
            if (velocity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(velocity),
                    "速度必须大于 0。");
            }
        }

        private static void ValidateFinite(double value, string displayName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentException($"{displayName}不是有效数字。");
            }
        }
    }
}
