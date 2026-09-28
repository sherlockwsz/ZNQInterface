using System.Threading;
using System.Threading.Tasks;
using ZNQInterface.Models.Device;

namespace ZNQInterface.Services.Device
{
    public interface IDeviceControlService
    {
        Task StartAutoAsync(
            CancellationToken cancellationToken = default);

        Task RequestStopAsync(
            CancellationToken cancellationToken = default);

        Task<DeviceControlState> ReadStateAsync(
            CancellationToken cancellationToken = default);
    }
}