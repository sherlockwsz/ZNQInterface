using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ZNQInterface.Services.Communication.Ads
{
    /// <summary>
    /// 应用程序唯一的 ADS 连接入口。
    /// 读取、写入和句柄管理全部通过该服务串行执行，
    /// 防止多个 ViewModel 并发访问 AdsClient。
    /// </summary>
    public interface IAdsConnectionService : IDisposable
    {
        event EventHandler<AdsConnectionStateChangedEventArgs>
            StateChanged;

        AdsConnectionState State { get; }

        bool IsConnected { get; }

        bool DesiredConnected { get; }

        string StateMessage { get; }

        AdsConnectionOptions Options { get; }

        Task StartAsync();

        Task ConnectAsync();

        Task DisconnectAsync();

        Task StopAsync();

        Task<T> ReadAsync<T>(
            string symbolName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 通过一条ADS Sum命令批量读取多个PLC叶子变量。
        /// 返回值顺序与requests完全一致。
        /// </summary>
        Task<IReadOnlyList<object>> ReadManyAsync(
            IReadOnlyList<AdsReadRequest> requests,
            CancellationToken cancellationToken = default);

        Task WriteAsync<T>(
            string symbolName,
            T value,
            CancellationToken cancellationToken = default);
    }
}
