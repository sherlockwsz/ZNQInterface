using AxisControlHmi_test.Models;
using System;
using System.Collections.Generic;
using TwinCAT.Ads;

namespace AxisControlHmi_test.Services
{
    public sealed class AdsAxisService : IAxisService, IDisposable
    {
        private readonly object _syncRoot = new();
        private readonly AdsConnectionOptions _options;
        private readonly Dictionary<string, uint> _handles = new(StringComparer.OrdinalIgnoreCase);
        private AdsClient? _client;
        private uint _heartbeat;
        private bool _connectionRequested;
        private bool _disposed;

        public AdsAxisService(AdsConnectionOptions options)
        {
            _options = options;
        }

        public bool IsConnected
        {
            get
            {
                lock (_syncRoot)
                {
                    return _client?.IsConnected == true;
                }
            }
        }

        public AxisStatus GetStatus()
        {
            lock (_syncRoot)
            {
                EnsureConnected();
                WriteCore(AdsSymbolNames.Heartbeat, ++_heartbeat);

                var motionState = Read<short>(AdsSymbolNames.MotionState);
                return new AxisStatus
                {
                    Position = Read<double>(AdsSymbolNames.ActualPosition),
                    Velocity = Read<double>(AdsSymbolNames.ActualVelocity),
                    Rpm = Read<double>(AdsSymbolNames.ActualRpm),
                    IsEnabled = Read<bool>(AdsSymbolNames.PowerStatus),
                    HasFault = Read<bool>(AdsSymbolNames.Error),
                    IsMoving = motionState is 2 or 3,
                    ErrorId = Read<uint>(AdsSymbolNames.ErrorId),
                    MotionState = motionState,
                    CommandRejected = Read<bool>(AdsSymbolNames.CommandRejected),
                    RejectReason = Read<uint>(AdsSymbolNames.RejectReason)
                };
            }
        }

        public void Connect()
        {
            lock (_syncRoot)
            {
                _connectionRequested = true;
                EnsureConnected();
            }
        }

        // bEnable 为保持型命令。
        public void Enable() => Write(AdsSymbolNames.Enable, true);

        // 以下命令均由 PLC 在捕获后自动复位，HMI 只负责写入 TRUE。
        public void Reset() => Write(AdsSymbolNames.Reset, true);

        public void Stop()
        {
            lock (_syncRoot)
            {
                EnsureConnected();
                WriteCore(AdsSymbolNames.JogPositive, false);
                WriteCore(AdsSymbolNames.JogNegative, false);
                WriteCore(AdsSymbolNames.Stop, true);
            }
        }

        public void MoveRelative(double distance)
        {
            lock (_syncRoot)
            {
                EnsureConnected();
                WriteCore(AdsSymbolNames.RelativeDistance, distance);
                WriteCore(AdsSymbolNames.MoveRelative, true);
            }
        }

        public void MoveAbsolute(double position)
        {
            lock (_syncRoot)
            {
                EnsureConnected();
                WriteCore(AdsSymbolNames.AbsolutePosition, position);
                WriteCore(AdsSymbolNames.MoveAbsolute, true);
            }
        }

        public void SetJogPositive(bool isActive)
        {
            lock (_syncRoot)
            {
                EnsureConnected();
                if (isActive) WriteCore(AdsSymbolNames.JogNegative, false);
                WriteCore(AdsSymbolNames.JogPositive, isActive);
            }
        }

        public void SetJogNegative(bool isActive)
        {
            lock (_syncRoot)
            {
                EnsureConnected();
                if (isActive) WriteCore(AdsSymbolNames.JogPositive, false);
                WriteCore(AdsSymbolNames.JogNegative, isActive);
            }
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_disposed) return;
                TryWriteJogOff();
                DisconnectClient();
                _disposed = true;
            }
        }

        private T Read<T>(string symbolName)
        {
            return _client!.ReadAny<T>(GetHandle(symbolName));
        }

        private void Write(string symbolName, object value)
        {
            lock (_syncRoot)
            {
                EnsureConnected();
                WriteCore(symbolName, value);
            }
        }

        private void WriteCore(string symbolName, object value)
        {
            _client!.WriteAny(GetHandle(symbolName), value);
        }

        private uint GetHandle(string symbolName)
        {
            if (_handles.TryGetValue(symbolName, out var handle)) return handle;
            handle = _client!.CreateVariableHandle(symbolName);
            _handles.Add(symbolName, handle);
            return handle;
        }

        private void EnsureConnected()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AdsAxisService));
            if (!_connectionRequested)
            {
                throw new InvalidOperationException("尚未连接 PLC，请点击“连接至PLC”。");
            }
            if (_client?.IsConnected == true) return;

            DisconnectClient();
            _client = new AdsClient { Timeout = _options.TimeoutMilliseconds };
            if (string.IsNullOrWhiteSpace(_options.AmsNetId))
            {
                _client.Connect(_options.Port);
            }
            else
            {
                _client.Connect(_options.AmsNetId, _options.Port);
            }

            // 每次重连都先清除保持型点动位，防止 HMI 断线时 PLC 遗留点动命令。
            WriteCore(AdsSymbolNames.JogPositive, false);
            WriteCore(AdsSymbolNames.JogNegative, false);
        }

        private void TryWriteJogOff()
        {
            if (_client?.IsConnected != true) return;
            try
            {
                WriteCore(AdsSymbolNames.JogPositive, false);
                WriteCore(AdsSymbolNames.JogNegative, false);
            }
            catch
            {
                // 关闭阶段尽力撤销点动命令，连接已经失效时无需继续抛出。
            }
        }

        private void DisconnectClient()
        {
            if (_client is null) return;
            foreach (var handle in _handles.Values)
            {
                try { _client.DeleteVariableHandle(handle); } catch { }
            }

            _handles.Clear();
            _client.Dispose();
            _client = null;
        }
    }
}
