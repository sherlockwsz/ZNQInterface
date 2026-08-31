using System.Threading;
using System.Threading.Tasks;
using ZNQInterface.Models.Axes;

namespace ZNQInterface.Services.Axes
{
    /// <summary>
    /// 手动运动命令接口。界面只表达操作意图，
    /// ADS 变量名、写入顺序和脉冲宽度由实现类统一负责。
    /// </summary>
    public interface IAxisCommandService
    {
        Task SetEnableAsync(
            AxisId axisId,
            bool enable,
            CancellationToken cancellationToken = default);

        Task ResetAsync(
            AxisId axisId,
            CancellationToken cancellationToken = default);

        Task StopAsync(
            AxisId axisId,
            CancellationToken cancellationToken = default);

        Task MoveAbsoluteAsync(
            AxisId axisId,
            double targetPosition,
            double velocity,
            CancellationToken cancellationToken = default);

        Task MoveRelativeAsync(
            AxisId axisId,
            double distance,
            double velocity,
            CancellationToken cancellationToken = default);

        Task StartJogAsync(
            AxisId axisId,
            bool positiveDirection,
            double velocity,
            CancellationToken cancellationToken = default);

        Task StopJogAsync(
            AxisId axisId,
            CancellationToken cancellationToken = default);
        /// <summary>
        /// 主动断开 ADS 或退出 HMI 前执行受控停机：
        /// 先停止运动，再撤销使能，最后清除所有运动命令位。
        /// </summary>
        Task StopAndDisableAsync(
            AxisId axisId,
            CancellationToken cancellationToken = default);
        Task ReleaseAllMotionSignalsAsync();
    }
}
