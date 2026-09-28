using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ZNQInterface.Services.Communication.Ads;
using ZNQInterface.ViewModels.Components.MaterialSlots;
using ZNQInterface.ViewModels.Components.Warehouse;

namespace ZNQInterface.Services.Processes
{
    /// <summary>
    /// 周期读取阻尼器24个料位、功能3缓冲位1以及五层料盘状态。
    ///
    /// 所有状态使用一次ADS Sum批量读取，避免逐变量读取造成通信负担。
    /// </summary>
    public sealed class ProcessStatusMonitoringService
    {
        private const int DamperRowCount = 4;
        private const int DamperColumnCount = 6;
        private const int TrayLevelCount = 5;

        private static readonly TimeSpan PollInterval =
            TimeSpan.FromMilliseconds(200);

        private readonly IAdsConnectionService _adsConnectionService;
        private readonly MaterialSlotStatusViewModel _materialSlotStatus;
        private readonly WarehouseStatusViewModel _warehouseStatus;
        private readonly IReadOnlyList<AdsReadRequest> _readRequests;

        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _monitorTask;

        public ProcessStatusMonitoringService(
            IAdsConnectionService adsConnectionService,
            MaterialSlotStatusViewModel materialSlotStatus,
            WarehouseStatusViewModel warehouseStatus)
        {
            _adsConnectionService = adsConnectionService;
            _materialSlotStatus = materialSlotStatus;
            _warehouseStatus = warehouseStatus;

            // ADS请求只需要建立一次，不要在每个扫描周期重复创建。
            _readRequests = CreateReadRequests();
        }

        /// <summary>
        /// 启动状态读取循环。
        /// </summary>
        public Task StartAsync()
        {
            if (_monitorTask != null && !_monitorTask.IsCompleted)
            {
                return Task.CompletedTask;
            }

            _cancellationTokenSource = new CancellationTokenSource();

            _monitorTask = Task.Run(
                () => MonitorLoopAsync(_cancellationTokenSource.Token));

            return Task.CompletedTask;
        }

        /// <summary>
        /// 停止状态读取循环。
        /// </summary>
        public async Task StopAsync()
        {
            CancellationTokenSource? cancellation =
                _cancellationTokenSource;

            Task? monitorTask = _monitorTask;

            _cancellationTokenSource = null;
            _monitorTask = null;

            if (cancellation == null)
            {
                return;
            }

            cancellation.Cancel();

            if (monitorTask != null)
            {
                try
                {
                    await monitorTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // 正常停止，不作为异常处理。
                }
            }

            cancellation.Dispose();
        }

        /// <summary>
        /// 建立30个批量读取请求：
        /// 24个料位 + 1个功能3缓冲位 + 5层料盘。
        /// </summary>
        private static IReadOnlyList<AdsReadRequest> CreateReadRequests()
        {
            List<AdsReadRequest> requests =
                new List<AdsReadRequest>(30);

            // 保持逐行顺序：
            // (1,1)～(1,6)、(2,1)～(2,6)……(4,1)～(4,6)。
            for (int row = 1; row <= DamperRowCount; row++)
            {
                for (int column = 1;
                     column <= DamperColumnCount;
                     column++)
                {
                    requests.Add(
                        AdsReadRequest.Create<short>(
                            ProcessStatusSymbols.DamperSlot(
                                row,
                                column)));
                }
            }

            // 只读取位置3并映射至WPF缓冲位1。
            // 不读取转台位置1和位置2。
            requests.Add(
                AdsReadRequest.Create<short>(
                    ProcessStatusSymbols.DamperBufferPosition));

            for (int level = 1;
                 level <= TrayLevelCount;
                 level++)
            {
                requests.Add(
                    AdsReadRequest.Create<short>(
                        ProcessStatusSymbols.TrayLevel(level)));
            }

            return requests;
        }

        /// <summary>
        /// 后台周期读取循环。
        /// </summary>
        private async Task MonitorLoopAsync(
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_adsConnectionService.IsConnected)
                    {
                        IReadOnlyList<object> values =
                            await _adsConnectionService.ReadManyAsync(
                                _readRequests,
                                cancellationToken)
                            .ConfigureAwait(false);

                        if (values.Count == _readRequests.Count)
                        {
                            await ApplyValuesOnUiThreadAsync(values)
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            await SetUnknownOnUiThreadAsync()
                                .ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        await SetUnknownOnUiThreadAsync()
                            .ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    // 通信中断、符号不存在或ADS读取失败时，
                    // 界面显示未知，不能保留最后一次数据冒充实时状态。
                    await SetUnknownOnUiThreadAsync()
                        .ConfigureAwait(false);
                }

                await Task.Delay(PollInterval, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 将ADS读取结果应用至界面。
        /// ObservableCollection及绑定属性必须在UI线程修改。
        /// </summary>
        private Task ApplyValuesOnUiThreadAsync(
            IReadOnlyList<object> values)
        {
            return Application.Current.Dispatcher.InvokeAsync(() =>
            {
                int valueIndex = 0;

                // 前24项对应主料盘24个物理料位。
                for (int slotIndex = 0;
                     slotIndex < DamperRowCount * DamperColumnCount;
                     slotIndex++)
                {
                    short rawState =
                        Convert.ToInt16(values[valueIndex++]);

                    _materialSlotStatus.MaterialSlots[slotIndex].State =
                        ConvertDamperState(rawState);
                }

                // 第25项是PLC位置3。
                // 只写入WPF四个缓冲位中的第1个。
                short bufferRawState =
                    Convert.ToInt16(values[valueIndex++]);

                _materialSlotStatus.SetBufferSlotState(
                    1,
                    ConvertDamperState(bufferRawState));

                // 缓冲位2～4不修改，继续保持Empty。

                // 最后5项对应五层料盘，顺序为1～5。
                for (int level = 1;
                     level <= TrayLevelCount;
                     level++)
                {
                    short rawState =
                        Convert.ToInt16(values[valueIndex++]);

                    _warehouseStatus.SetWarehouseLevelState(
                        level,
                        ConvertTrayState(rawState));
                }
            }).Task;
        }

        /// <summary>
        /// ADS断开或读取失败后重置实时数据显示。
        /// </summary>
        private Task SetUnknownOnUiThreadAsync()
        {
            return Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (MaterialSlotViewModel slot
                         in _materialSlotStatus.MaterialSlots)
                {
                    slot.State = MaterialSlotState.Unknown;
                }

                // 只有缓冲位1绑定PLC。
                _materialSlotStatus.SetBufferSlotState(
                    1,
                    MaterialSlotState.Unknown);

                // 缓冲位2～4保持Empty，不进行修改。
                for (int level = 1;
                     level <= TrayLevelCount;
                     level++)
                {
                    _warehouseStatus.SetWarehouseLevelState(
                        level,
                        WarehouseLevelState.Unknown);
                }
            }).Task;
        }

        /// <summary>
        /// 转换PLC阻尼器状态。
        /// 未知数值不能直接强制转换后显示为合法状态。
        /// </summary>
        private static MaterialSlotState ConvertDamperState(short value)
        {
            return value switch
            {
                0 => MaterialSlotState.Empty,
                10 => MaterialSlotState.Unadjusted,
                20 => MaterialSlotState.PendingAdjustment,
                30 => MaterialSlotState.Adjusting,
                40 => MaterialSlotState.Qualified,
                50 => MaterialSlotState.Unqualified,
                _ => MaterialSlotState.Unknown
            };
        }

        /// <summary>
        /// 转换PLC五层料盘状态。
        /// </summary>
        private static WarehouseLevelState ConvertTrayState(short value)
        {
            return value switch
            {
                0 => WarehouseLevelState.Empty,
                1 => WarehouseLevelState.WaitingInspection,
                2 => WarehouseLevelState.Inspecting,
                3 => WarehouseLevelState.Inspected,
                _ => WarehouseLevelState.Unknown
            };
        }
    }
}