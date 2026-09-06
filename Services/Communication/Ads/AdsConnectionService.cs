using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TwinCAT;
using TwinCAT.Ads;
using TwinCAT.Ads.SumCommand;

namespace ZNQInterface.Services.Communication.Ads
{
    /// <summary>
    /// 负责 AdsClient 生命周期、变量句柄缓存和自动重连。
    ///
    /// 注意：
    /// 1. 所有同步 ADS API 都放到后台线程执行，不阻塞 WPF UI；
    /// 2. 所有读写由 _ioGate 串行化；
    /// 3. 通信异常后保留 DesiredConnected，后台循环自动重连。
    /// </summary>
    public sealed class AdsConnectionService :
        IAdsConnectionService
    {
        private readonly SemaphoreSlim _ioGate =
            new SemaphoreSlim(1, 1);

        private readonly Dictionary<string, uint> _variableHandles =
            new Dictionary<string, uint>(
                StringComparer.OrdinalIgnoreCase);

        private CancellationTokenSource _lifetimeCts;
        private Task _connectionLoopTask;
        private AdsClient _client;
        private bool _disposed;

        public AdsConnectionService()
        {
            Options = new AdsConnectionOptions();
            SetState(
                AdsConnectionState.Disconnected,
                "ADS通信：未连接");
        }

        public event EventHandler<AdsConnectionStateChangedEventArgs>
            StateChanged;

        public AdsConnectionOptions Options { get; }

        public AdsConnectionState State { get; private set; }

        public bool IsConnected =>
            State == AdsConnectionState.Connected;

        public bool DesiredConnected { get; private set; }

        public string StateMessage { get; private set; }

        /// <summary>
        /// 启动应用级自动连接循环。
        /// </summary>
        public Task StartAsync()
        {
            ThrowIfDisposed();

            if (_connectionLoopTask != null)
            {
                return Task.CompletedTask;
            }

            DesiredConnected = true;
            _lifetimeCts = new CancellationTokenSource();
            _connectionLoopTask = Task.Run(
                () => ConnectionLoopAsync(_lifetimeCts.Token));

            return Task.CompletedTask;
        }

        /// <summary>
        /// 用户主动请求连接；失败后仍由自动重连循环继续尝试。
        /// </summary>
        public async Task ConnectAsync()
        {
            ThrowIfDisposed();
            DesiredConnected = true;

            if (_connectionLoopTask == null)
            {
                await StartAsync().ConfigureAwait(false);
            }

            await TryConnectAsync(
                isReconnect: false,
                CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// 用户主动断开；在再次点击连接之前不自动重连。
        /// </summary>
        public async Task DisconnectAsync()
        {
            DesiredConnected = false;
            await CloseClientAsync(
                AdsConnectionState.Disconnected,
                "ADS通信：已断开").ConfigureAwait(false);
        }

        public async Task StopAsync()
        {
            DesiredConnected = false;

            if (_lifetimeCts != null)
            {
                _lifetimeCts.Cancel();
            }

            if (_connectionLoopTask != null)
            {
                try
                {
                    await _connectionLoopTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }

            await CloseClientAsync(
                AdsConnectionState.Disconnected,
                "ADS通信：已停止").ConfigureAwait(false);

            _connectionLoopTask = null;
            _lifetimeCts?.Dispose();
            _lifetimeCts = null;
        }

        public Task<T> ReadAsync<T>(
            string symbolName,
            CancellationToken cancellationToken = default)
        {
            ValidateSymbolName(symbolName);

            // AdsClient.ReadAny 是同步 API，放入后台线程防止阻塞 UI。
            return Task.Run(
                async () =>
                {
                    await _ioGate
                        .WaitAsync(cancellationToken)
                        .ConfigureAwait(false);

                    try
                    {
                        EnsureConnected();
                        uint handle = GetOrCreateHandleUnsafe(symbolName);
                        object value =
                            _client.ReadAny(handle, typeof(T));
                        return (T)value;
                    }
                    catch (Exception exception)
                        when (!(exception is OperationCanceledException))
                    {
                        MarkCommunicationFaultUnsafe(exception);
                        throw;
                    }
                    finally
                    {
                        _ioGate.Release();
                    }
                },
                cancellationToken);
        }

        public Task<IReadOnlyList<object>> ReadManyAsync(
            IReadOnlyList<AdsReadRequest> requests,
            CancellationToken cancellationToken = default)
        {
            if (requests == null)
            {
                throw new ArgumentNullException(nameof(requests));
            }

            if (requests.Count == 0)
            {
                return Task.FromResult<IReadOnlyList<object>>(
                    Array.Empty<object>());
            }

            // Beckhoff建议单条Sum命令最多500个子命令。
            if (requests.Count > 500)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requests),
                    "单次ADS批量读取不能超过500个变量。");
            }

            foreach (AdsReadRequest request in requests)
            {
                if (request == null)
                {
                    throw new ArgumentException(
                        "ADS批量读取项不能为null。",
                        nameof(requests));
                }

                ValidateSymbolName(request.SymbolName);
            }

            return Task.Run(
                async () =>
                {
                    await _ioGate
                        .WaitAsync(cancellationToken)
                        .ConfigureAwait(false);

                    try
                    {
                        EnsureConnected();

                        uint[] handles = new uint[requests.Count];
                        Type[] valueTypes = new Type[requests.Count];

                        for (int index = 0;
                            index < requests.Count;
                            index++)
                        {
                            AdsReadRequest request = requests[index];

                            handles[index] =
                                GetOrCreateHandleUnsafe(
                                    request.SymbolName);

                            valueTypes[index] =
                                request.ValueType;
                        }

                        SumHandleReadAnyType command =
                            new SumHandleReadAnyType(
                                _client,
                                handles,
                                valueTypes);

                        ResultSumValues result =
                            command.Read();

                        if (result.OverallFailed)
                        {
                            throw new InvalidOperationException(
                                "ADS批量读取失败：" +
                                $"{result.FirstSubError}。");
                        }

                        return (IReadOnlyList<object>)
                            result.Values;
                    }
                    catch (Exception exception)
                        when (!(exception is OperationCanceledException))
                    {
                        MarkCommunicationFaultUnsafe(exception);
                        throw;
                    }
                    finally
                    {
                        _ioGate.Release();
                    }
                },
                cancellationToken);
        }

        public Task WriteAsync<T>(
            string symbolName,
            T value,
            CancellationToken cancellationToken = default)
        {
            ValidateSymbolName(symbolName);

            return Task.Run(
                async () =>
                {
                    await _ioGate
                        .WaitAsync(cancellationToken)
                        .ConfigureAwait(false);

                    try
                    {
                        EnsureConnected();
                        uint handle = GetOrCreateHandleUnsafe(symbolName);
                        _client.WriteAny(handle, value);
                    }
                    catch (Exception exception)
                        when (!(exception is OperationCanceledException))
                    {
                        MarkCommunicationFaultUnsafe(exception);
                        throw;
                    }
                    finally
                    {
                        _ioGate.Release();
                    }
                },
                cancellationToken);
        }

        private async Task ConnectionLoopAsync(
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (DesiredConnected && !IsConnected)
                {
                    await TryConnectAsync(
                        isReconnect: State == AdsConnectionState.Faulted,
                        cancellationToken).ConfigureAwait(false);
                }

                await Task.Delay(
                    Options.ReconnectInterval,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task TryConnectAsync(
            bool isReconnect,
            CancellationToken cancellationToken)
        {
            if (!DesiredConnected || IsConnected)
            {
                return;
            }

            await _ioGate
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            try
            {
                if (!DesiredConnected || IsConnected)
                {
                    return;
                }

                SetState(
                    isReconnect
                        ? AdsConnectionState.Reconnecting
                        : AdsConnectionState.Connecting,
                    isReconnect
                        ? "ADS通信：正在重连"
                        : "ADS通信：正在连接");

                CloseClientUnsafe();

                AdsClient newClient = new AdsClient();
                newClient.Connect(
                    AmsNetId.Local,
                    Options.AmsPort);

                _client = newClient;
                _variableHandles.Clear();

                SetState(
                    AdsConnectionState.Connected,
                    $"ADS通信：已连接（端口{Options.AmsPort}）");
            }
            catch (Exception exception)
                when (!(exception is OperationCanceledException))
            {
                CloseClientUnsafe();
                SetState(
                    AdsConnectionState.Faulted,
                    $"ADS通信：连接失败 - {exception.Message}");
            }
            finally
            {
                _ioGate.Release();
            }
        }

        private async Task CloseClientAsync(
            AdsConnectionState state,
            string message)
        {
            await _ioGate.WaitAsync().ConfigureAwait(false);

            try
            {
                CloseClientUnsafe();
                SetState(state, message);
            }
            finally
            {
                _ioGate.Release();
            }
        }

        private uint GetOrCreateHandleUnsafe(string symbolName)
        {
            if (_variableHandles.TryGetValue(
                    symbolName,
                    out uint handle))
            {
                return handle;
            }

            handle = _client.CreateVariableHandle(symbolName);
            _variableHandles.Add(symbolName, handle);
            return handle;
        }

        private void MarkCommunicationFaultUnsafe(Exception exception)
        {
            CloseClientUnsafe();
            SetState(
                AdsConnectionState.Faulted,
                $"ADS通信：异常 - {exception.Message}");
        }

        private void CloseClientUnsafe()
        {
            if (_client == null)
            {
                _variableHandles.Clear();
                return;
            }

            // 连接有效时主动释放变量句柄；断线时释放失败不再抛出。
            foreach (uint handle in _variableHandles.Values)
            {
                try
                {
                    _client.DeleteVariableHandle(handle);
                }
                catch
                {
                }
            }

            _variableHandles.Clear();

            try
            {
                _client.Disconnect();
            }
            catch
            {
            }

            _client.Dispose();
            _client = null;
        }

        private void SetState(
            AdsConnectionState state,
            string message)
        {
            State = state;
            StateMessage = message;

            StateChanged?.Invoke(
                this,
                new AdsConnectionStateChangedEventArgs(
                    state,
                    message));
        }

        private void EnsureConnected()
        {
            if (!IsConnected || _client == null)
            {
                throw new InvalidOperationException(
                    "ADS尚未连接，不能读写PLC变量。");
            }
        }

        private static void ValidateSymbolName(string symbolName)
        {
            if (string.IsNullOrWhiteSpace(symbolName))
            {
                throw new ArgumentException(
                    "ADS符号名不能为空。",
                    nameof(symbolName));
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    nameof(AdsConnectionService));
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                StopAsync().GetAwaiter().GetResult();
            }
            catch
            {
            }

            _disposed = true;
            _ioGate.Dispose();
        }
    }
}
