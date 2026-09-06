using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZNQInterface.Services.Communication.Ads;
using ZNQInterface.Models.Axes;
using ZNQInterface.ViewModels.Components.Axes;

namespace ZNQInterface.Services.Axes
{
    /// <summary>
    /// 将上位机命令映射到GVL_AxisRuntime.Axes[1..14]。
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
        /// 该动作与PLC看门狗共同防止断线期间命令位粘连。
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
        /// <summary>
        /// 停止所有已经配置ADS映射的轴，并撤销使能。
        ///
        /// 处理原则：
        /// 1. 自动查找所有IsAdsMapped为true的轴；
        /// 2. 相同ADS前缀只处理一次；
        /// 3. 先同时释放全部点动；
        /// 4. 再同时发送停止信号；
        /// 5. 停止信号只等待一个公共脉冲周期；
        /// 6. 最后撤销全部使能并清除运动命令。
        ///
        /// 以后增加新轴时，只需要配置AdsSymbolPrefix，
        /// 不需要修改本方法、MainWindowViewModel或App。
        /// </summary>
        public async Task StopAndDisableAllMappedAxesAsync(
            CancellationToken cancellationToken = default)
        {
            /*
             * ADS已经断开时，WPF无法继续发送停止命令。
             * 此时由PLC心跳看门狗负责停止并掉使能。
             */
            if (!_ads.IsConnected)
            {
                return;
            }

            /*
             * 查找所有已经配置ADS运行接口的轴。
             *
             * GroupBy用于防止错误配置时，
             * 两个WPF轴使用同一个PLC ADS前缀而被重复处理。
             */
            AxisItemViewModel[] mappedAxes =
                _axisStatus.AllAxes
                    .Where(axis =>
                        axis.Definition.IsAdsMapped)
                    .GroupBy(
                        axis =>
                            axis.Definition.AdsSymbolPrefix,
                        StringComparer.OrdinalIgnoreCase)
                    .Select(group =>
                        group.First())
                    .ToArray();

            if (mappedAxes.Length == 0)
            {
                return;
            }

            /*
             * 锁定整个多轴停止过程，
             * 防止中间插入新的使能、定位或点动命令。
             */
            await _commandGate
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            try
            {
                /*
                 * 第一步：释放所有轴的点动命令。
                 */
                foreach (AxisItemViewModel axis in mappedAxes)
                {
                    string prefix =
                        axis.Definition.AdsSymbolPrefix;

                    await _ads.WriteAsync(
                            AdsAxisSymbols.Command(
                                prefix,
                                "bJogPos"),
                            false,
                            cancellationToken)
                        .ConfigureAwait(false);

                    await _ads.WriteAsync(
                            AdsAxisSymbols.Command(
                                prefix,
                                "bJogNeg"),
                            false,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                /*
                 * 第二步：先确保所有停止信号为FALSE。
                 * 下一步统一写TRUE，保证PLC能够检测到新停止请求。
                 */
                foreach (AxisItemViewModel axis in mappedAxes)
                {
                    await _ads.WriteAsync(
                            AdsAxisSymbols.Command(
                                axis.Definition.AdsSymbolPrefix,
                                "bStop"),
                            false,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                /*
                 * 第三步：向全部已映射轴发送停止请求。
                 */
                foreach (AxisItemViewModel axis in mappedAxes)
                {
                    await _ads.WriteAsync(
                            AdsAxisSymbols.Command(
                                axis.Definition.AdsSymbolPrefix,
                                "bStop"),
                            true,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                /*
                 * 全部轴共用一次脉冲等待。
                 *
                 * 不要逐根等待100ms，否则扩展到14轴后，
                 * 仅等待时间就会达到1.4秒。
                 */
                await Task.Delay(
                        _ads.Options.CommandPulseWidth,
                        cancellationToken)
                    .ConfigureAwait(false);

                /*
                 * 第四步：释放停止信号。
                 *
                 * PLC内部的bStopExecute会继续保持，
                 * 直到MC_Stop完成受控停止。
                 */
                foreach (AxisItemViewModel axis in mappedAxes)
                {
                    await _ads.WriteAsync(
                            AdsAxisSymbols.Command(
                                axis.Definition.AdsSymbolPrefix,
                                "bStop"),
                            false,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                /*
                 * 第五步：撤销所有轴的使能请求。
                 *
                 * PLC的FB_AxisControl会在停止完成后
                 * 才真正撤销MC_Power。
                 */
                foreach (AxisItemViewModel axis in mappedAxes)
                {
                    await _ads.WriteAsync(
                            AdsAxisSymbols.Command(
                                axis.Definition.AdsSymbolPrefix,
                                "bEnable"),
                            false,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                /*
                 * 第六步：清除其余外部命令。
                 */
                string[] commandsToClear =
                {
            "bReset",
            "bMoveAbs",
            "bMoveRel",
            "bJogPos",
            "bJogNeg"
        };

                foreach (AxisItemViewModel axis in mappedAxes)
                {
                    string prefix =
                        axis.Definition.AdsSymbolPrefix;

                    foreach (string member in commandsToClear)
                    {
                        await _ads.WriteAsync(
                                AdsAxisSymbols.Command(
                                    prefix,
                                    member),
                                false,
                                cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                /*
                 * 即使停止过程中取消或出现异常，
                 * 也尽力清除所有持续型命令。
                 */
                foreach (AxisItemViewModel axis in mappedAxes)
                {
                    string prefix =
                        axis.Definition.AdsSymbolPrefix;

                    foreach (string member in new[]
                             {
                         "bStop",
                         "bJogPos",
                         "bJogNeg",
                         "bEnable"
                     })
                    {
                        try
                        {
                            await _ads.WriteAsync(
                                    AdsAxisSymbols.Command(
                                        prefix,
                                        member),
                                    false,
                                    CancellationToken.None)
                                .ConfigureAwait(false);
                        }
                        catch
                        {
                            /*
                             * ADS已经断开时无法继续写入。
                             * PLC心跳看门狗将接管停止。
                             */
                        }
                    }
                }

                _commandGate.Release();
            }
        }
        /// <summary>
        /// 将速度参数写入当前轴。
        ///
        /// 注意：
        /// 1. 本方法只写速度，不触发任何运动命令；
        /// 2. 绝对和相对运动共用定位速度；
        /// 3. 点动使用独立的点动速度；
        /// 4. 优先采用PLC ST_AxisLimit的速度范围；
        /// 5. PLC范围尚未设置时兼容原有(0, 100]范围。
        /// </summary>
        public async Task WriteVelocityAsync(
            AxisId axisId,
            ManualMotionMode motionMode,
            double velocity,
            CancellationToken cancellationToken = default)
        {
            AxisItemViewModel axis = GetMappedAxis(axisId);
            ValidatePositiveVelocity(axis, velocity);

            await _commandGate
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            try
            {
                string prefix = axis.Definition.AdsSymbolPrefix;
                string velocitySymbol;

                switch (motionMode)
                {
                    case ManualMotionMode.Absolute:
                    case ManualMotionMode.Relative:

                        // 绝对定位和相对定位共用定位速度。
                        velocitySymbol = AdsAxisSymbols.Setting(
                            prefix,
                            "Position",
                            "fVelocity");
                        break;

                    case ManualMotionMode.Jog:

                        // 正向点动和负向点动共用点动速度。
                        velocitySymbol = AdsAxisSymbols.Setting(
                            prefix,
                            "Jog",
                            "fVelocity");
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(motionMode),
                            motionMode,
                            "无法识别当前运动模式。");
                }

                // 这里只写入速度参数，不触发运动。
                await _ads.WriteAsync(
                    velocitySymbol,
                    velocity,
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _commandGate.Release();
            }
        }
        public async Task MoveAbsoluteAsync(
            AxisId axisId,
            double targetPosition,
            CancellationToken cancellationToken = default)
        {
            AxisItemViewModel axis = GetMappedAxis(axisId);
            ValidatePosition(axis, targetPosition, "目标位置");

            await _commandGate
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            try
            {
                string prefix = axis.Definition.AdsSymbolPrefix;

                // 目标位置属于本次运动命令，点击绝对运动时写入。
                await _ads.WriteAsync(
                    AdsAxisSymbols.Setting(
                        prefix,
                        "Position",
                        "fAbsolutePosition"),
                    targetPosition,
                    cancellationToken).ConfigureAwait(false);

                // 这里不再写入fVelocity。
                // 定位速度只能通过“写入参数”按钮修改。
                await PulseCommandUnsafeAsync(
                    prefix,
                    "bMoveAbs",
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _commandGate.Release();
            }
        }

        public async Task MoveRelativeAsync(
            AxisId axisId,
            double distance,
            CancellationToken cancellationToken = default)
        {
            ValidateFinite(distance, "相对距离");
            AxisItemViewModel axis = GetMappedAxis(axisId);
            ValidatePosition(
                axis,
                axis.Runtime.ActualPosition + distance,
                "相对运动目标位置");

            await _commandGate
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            try
            {
                string prefix = axis.Definition.AdsSymbolPrefix;

                // 相对距离属于本次运动命令，点击相对运动时写入。
                await _ads.WriteAsync(
                    AdsAxisSymbols.Setting(
                        prefix,
                        "Position",
                        "fRelativeDistance"),
                    distance,
                    cancellationToken).ConfigureAwait(false);

                // 这里不再写入fVelocity。
                // 相对运动使用之前通过按钮写入的定位速度。
                await PulseCommandUnsafeAsync(
                    prefix,
                    "bMoveRel",
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _commandGate.Release();
            }
        }

        public async Task StartJogAsync(
            AxisId axisId,
            bool positiveDirection,
            CancellationToken cancellationToken = default)
        {
            AxisItemViewModel axis = GetMappedAxis(axisId);

            await _commandGate
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            try
            {
                string prefix = axis.Definition.AdsSymbolPrefix;

                // 开始点动前，先保证两个方向信号均为FALSE。
                await _ads.WriteAsync(
                    AdsAxisSymbols.Command(prefix, "bJogPos"),
                    false,
                    cancellationToken).ConfigureAwait(false);

                await _ads.WriteAsync(
                    AdsAxisSymbols.Command(prefix, "bJogNeg"),
                    false,
                    cancellationToken).ConfigureAwait(false);

                // 这里不再写入Set.Jog.fVelocity。
                // 点动使用之前通过“写入参数”按钮写入的速度。
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
            AxisItemViewModel axis = _axisStatus.FindAxis(axisId);

            if (axis == null || !axis.Definition.IsAdsMapped)
            {
                throw new InvalidOperationException(
                    $"轴 {axisId} 尚未配置 ADS 映射");
            }

            if (!axis.Runtime.IsConfigurationKnown)
            {
                throw new InvalidOperationException(
                    $"轴 {axisId} 尚未读取PLC配置，不能执行控制。");
            }

            if (!axis.Runtime.IsConfigured || axis.HasConfigurationMismatch)
            {
                throw new InvalidOperationException(
                    $"轴 {axisId} 的PLC配置无效或与WPF映射不一致。");
            }

            if (!_ads.IsConnected)
            {
                throw new InvalidOperationException("ADS 尚未连接。");
            }

            return axis;
        }

        /// <summary>
        /// 校验手动调试速度。
        /// PLC设置了有效的fMinVelocity/fMaxVelocity时以PLC为准；
        /// 两者仍为默认0时使用原界面(0, 100]，保证现有行为不变。
        /// </summary>
        private static void ValidatePositiveVelocity(
            AxisItemViewModel axis,
            double velocity)
        {
            ValidateFinite(velocity, "速度");

            if (velocity <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(velocity),
                    "速度必须大于0。");
            }

            double plcMinimum = axis.Runtime.MinimumVelocity;
            double plcMaximum = axis.Runtime.MaximumVelocity;
            bool hasPlcRange =
                plcMaximum > 0.0 && plcMaximum >= plcMinimum;

            double minimum = hasPlcRange
                ? Math.Max(0.0, plcMinimum)
                : 0.0;
            double maximum = hasPlcRange
                ? plcMaximum
                : 100.0;

            if (velocity < minimum || velocity > maximum)
            {
                string interval = minimum > 0.0
                    ? $"[{minimum:F3}, {maximum:F3}]"
                    : $"(0, {maximum:F3}]";

                throw new ArgumentOutOfRangeException(
                    nameof(velocity),
                    $"速度必须在{interval}范围内。" +
                    (hasPlcRange
                        ? "该范围来自PLC ST_AxisLimit。"
                        : string.Empty));
            }
        }

        private static void ValidatePosition(
            AxisItemViewModel axis,
            double position,
            string displayName)
        {
            ValidateFinite(position, displayName);

            AxisRuntimeData runtime = axis.Runtime;
            if (!runtime.SoftLimitEnabled ||
                runtime.SoftwareLimitPositive <=
                    runtime.SoftwareLimitNegative)
            {
                return;
            }

            double minimum = runtime.SoftwareLimitNegative +
                Math.Max(0.0, runtime.SoftLimitMargin);
            double maximum = runtime.SoftwareLimitPositive -
                Math.Max(0.0, runtime.SoftLimitMargin);

            if (minimum > maximum)
            {
                throw new InvalidOperationException(
                    $"轴 {axis.Definition.AxisId} 的PLC软限位与缓冲距离无效。");
            }

            if (position < minimum || position > maximum)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(position),
                    $"{displayName}必须在[{minimum:F3}, {maximum:F3}]范围内，" +
                    "该范围已包含PLC软限位缓冲距离。");
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
