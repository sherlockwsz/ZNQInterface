using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZNQInterface.Models.Device;
using ZNQInterface.Services.Communication.Ads;

namespace ZNQInterface.Services.Device
{
    /// <summary>
    /// 设备级自动启停控制。
    ///
    /// 只负责MAIN中的设备控制变量，
    /// 不直接逐根操作轴命令。
    /// </summary>
    public sealed class DeviceControlService :
        IDeviceControlService
    {
        private static readonly TimeSpan StopAcceptTimeout =
            TimeSpan.FromSeconds(2);

        private readonly IAdsConnectionService _ads;

        private readonly IReadOnlyList<AdsReadRequest>
            _stateReadRequests;

        public DeviceControlService(
            IAdsConnectionService ads)
        {
            _ads = ads ??
                throw new ArgumentNullException(nameof(ads));

            _stateReadRequests =
                new AdsReadRequest[]
                {
                    AdsReadRequest.Create<bool>(
                        DeviceControlSymbols.ControlAuto),

                    AdsReadRequest.Create<bool>(
                        DeviceControlSymbols.AutoStopping),

                    AdsReadRequest.Create<bool>(
                        DeviceControlSymbols.AutoStopDone),

                    AdsReadRequest.Create<bool>(
                        DeviceControlSymbols.AutoStopError),

                    AdsReadRequest.Create<bool>(
                        DeviceControlSymbols.AutoStopTimeout),

                    AdsReadRequest.Create<ushort>(
                        DeviceControlSymbols.AutoStopErrorAxis),

                    AdsReadRequest.Create<uint>(
                        DeviceControlSymbols.AutoStopErrorId),
                    AdsReadRequest.Create<bool>(DeviceControlSymbols.ControlPrepare),
                    AdsReadRequest.Create<bool>(DeviceControlSymbols.MachinePreparationBusy),
                    AdsReadRequest.Create<bool>(DeviceControlSymbols.MachinePreparationDone),
                    AdsReadRequest.Create<bool>(DeviceControlSymbols.MachinePreparationError),
                    AdsReadRequest.Create<bool>(DeviceControlSymbols.MachinePreparationTimeout),
                    AdsReadRequest.Create<ushort>(DeviceControlSymbols.MachinePreparationErrorAxis),
                    AdsReadRequest.Create<uint>(DeviceControlSymbols.MachinePreparationErrorCode),
                    AdsReadRequest.Create<short>(DeviceControlSymbols.MachineMode),
                    AdsReadRequest.Create<bool>(DeviceControlSymbols.MachineFault),
                    AdsReadRequest.Create<bool>(DeviceControlSymbols.MachineFaultResetBlocked),
                    AdsReadRequest.Create<short>(DeviceControlSymbols.MachineFaultSource),
                    AdsReadRequest.Create<ushort>(DeviceControlSymbols.MachineFaultAxis),
                    AdsReadRequest.Create<uint>(DeviceControlSymbols.MachineFaultCode)
                };
        }

        /// <summary>
        /// 启动自动流程。
        ///
        /// 必须先确认PLC没有正在停止，
        /// 然后释放停止请求，再启动自动流程。
        /// </summary>
        public async Task StartAutoAsync(
            CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            DeviceControlState state =
                await ReadStateAsync(cancellationToken)
                    .ConfigureAwait(false);

            if (state.IsAutoStopping)
            {
                throw new InvalidOperationException(
                    "设备正在停止，不能启动自动流程。");
            }

            // 确保上一次停止请求已经释放。
            await _ads.WriteAsync(
                DeviceControlSymbols.ControlStop,
                false,
                cancellationToken).ConfigureAwait(false);

            // PLC收到TRUE后自动使能当前流程使用的8根轴。
            await _ads.WriteAsync(
                DeviceControlSymbols.ControlAuto,
                true,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 请求PLC执行设备级全轴停止。
        ///
        /// 顺序：
        /// 1. 撤销自动运行；
        /// 2. 置位独立停止请求；
        /// 3. 等PLC锁存停止状态；
        /// 4. 释放停止请求。
        /// </summary>
        public async Task RequestStopAsync(
            CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            DeviceControlState beforeStop =
                await ReadStateAsync(cancellationToken)
                    .ConfigureAwait(false);

            if (beforeStop.IsAutoStopping)
            {
                return;
            }

            /*
             * 正常情况下，只在bControlAuto=TRUE时，
             * ToggleButton才会调用本方法。
             */
            if (!beforeStop.IsControlAuto)
            {
                throw new InvalidOperationException(
                    "PLC当前未处于自动运行状态，不能执行自动停止切换。");
            }

            /*
             * 自动运行期间PLC应已经清除上一次停止完成状态。
             * 如果仍为TRUE，说明PLC和WPF状态尚未同步，不能把它当作本次反馈。
             */
            if (beforeStop.IsAutoStopDone)
            {
                throw new InvalidOperationException(
                    "PLC停止完成状态尚未复位，请稍后重试。");
            }

            await _ads.WriteAsync(
                DeviceControlSymbols.ControlAuto,
                false,
                cancellationToken).ConfigureAwait(false);

            await _ads.WriteAsync(
                DeviceControlSymbols.ControlStop,
                true,
                cancellationToken).ConfigureAwait(false);

            try
            {
                await WaitForStopAcceptedAsync(
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (_ads.IsConnected)
                {
                    try
                    {
                        /*
                         * 不使用原CancellationToken。
                         * 如果调用方取消，仍应尽力释放停止请求。
                         */
                        await _ads.WriteAsync(
                            DeviceControlSymbols.ControlStop,
                            false,
                            CancellationToken.None).ConfigureAwait(false);
                    }
                    catch
                    {
                        /*
                         * 释放失败时不能覆盖前面的原始异常。
                         * PLC仍可通过保持的停止请求或看门狗完成停机。
                         */
                    }
                }
            }
        }
        public async Task<DeviceControlState> ReadStateAsync(
            CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            IReadOnlyList<object> values =
                await _ads.ReadManyAsync(
                    _stateReadRequests,
                    cancellationToken).ConfigureAwait(false);

            return new DeviceControlState
            {
                IsControlAuto =
                    Convert.ToBoolean(values[0]),

                IsAutoStopping =
                    Convert.ToBoolean(values[1]),

                IsAutoStopDone =
                    Convert.ToBoolean(values[2]),

                IsAutoStopError =
                    Convert.ToBoolean(values[3]),

                IsAutoStopTimeout =
                    Convert.ToBoolean(values[4]),

                AutoStopErrorAxis =
                    Convert.ToUInt16(values[5]),

                AutoStopErrorId =
                    Convert.ToUInt32(values[6]),
                IsControlPrepare = Convert.ToBoolean(values[7]),
                IsPreparationBusy = Convert.ToBoolean(values[8]),
                IsPreparationDone = Convert.ToBoolean(values[9]),
                IsPreparationError = Convert.ToBoolean(values[10]),
                IsPreparationTimeout = Convert.ToBoolean(values[11]),
                PreparationErrorAxis = Convert.ToUInt16(values[12]),
                PreparationErrorCode = Convert.ToUInt32(values[13]),
                MachineMode = (MachineMode)Convert.ToInt16(values[14]),
                IsMachineFault = Convert.ToBoolean(values[15]),
                IsMachineFaultResetBlocked = Convert.ToBoolean(values[16]),
                MachineFaultSource = (MachineFaultSource)Convert.ToInt16(values[17]),
                MachineFaultAxis = Convert.ToUInt16(values[18]),
                MachineFaultCode = Convert.ToUInt32(values[19])
            };
        }

        public async Task RequestPrepareAsync(
            CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            // 请求由PLC完成、拒绝或异常分支消费；WPF不定时清零。
            await _ads.WriteAsync(DeviceControlSymbols.ControlPrepare,
                true, cancellationToken).ConfigureAwait(false);
        }

        private readonly SemaphoreSlim _resetRequestGate = new SemaphoreSlim(1, 1);

        public async Task RequestMachineFaultResetAsync(
            CancellationToken cancellationToken = default)
        {
            await _resetRequestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                EnsureConnected();
                await _ads.WriteAsync(DeviceControlSymbols.ResetMachineFault,
                    false, cancellationToken).ConfigureAwait(false);
                // 让PLC扫描到低电平，保证下一次R_TRIG产生边沿。
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                await _ads.WriteAsync(DeviceControlSymbols.ResetMachineFault,
                    true, cancellationToken).ConfigureAwait(false);
                // 配套PLC在消费边沿后清零；这里只确认请求已消费，不推断恢复成功。
                DateTime deadline = DateTime.UtcNow + StopAcceptTimeout;
                while (await _ads.ReadAsync<bool>(DeviceControlSymbols.ResetMachineFault,
                    cancellationToken).ConfigureAwait(false))
                {
                    if (DateTime.UtcNow >= deadline)
                        throw new TimeoutException("PLC未在规定时间内消费故障恢复请求。");
                    await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                try
                {
                    if (_ads.IsConnected)
                        await _ads.WriteAsync(DeviceControlSymbols.ResetMachineFault,
                            false, CancellationToken.None).ConfigureAwait(false);
                }
                finally
                {
                    _resetRequestGate.Release();
                }
            }
        }

        private async Task WaitForStopAcceptedAsync(
            CancellationToken cancellationToken)
        {
            DateTime deadline =
                DateTime.UtcNow + StopAcceptTimeout;

            while (DateTime.UtcNow < deadline)
            {
                DeviceControlState state =
                    await ReadStateAsync(cancellationToken)
                        .ConfigureAwait(false);

                if (state.IsAutoStopping ||
                    state.IsAutoStopDone)
                {
                    return;
                }

                await Task.Delay(
                    50,
                    cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException(
                "PLC在规定时间内没有接受设备停止请求。");
        }

        private void EnsureConnected()
        {
            if (!_ads.IsConnected)
            {
                throw new InvalidOperationException(
                    "ADS尚未连接，无法控制设备。");
            }
        }
    }
}