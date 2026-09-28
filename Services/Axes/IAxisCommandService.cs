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
        /// <summary>
        /// 将当前运动模式对应的速度写入PLC。
        ///
        /// 绝对运动、相对运动：写入 Set.Position.fVelocity；
        /// 点动运动：写入 Set.Jog.fVelocity。
        ///
        /// 修改输入框不会调用该方法，只有点击“写入参数”按钮才调用。
        /// </summary>
        Task WriteVelocityAsync(
            AxisId axisId,
            ManualMotionMode motionMode,
            double velocity,
            CancellationToken cancellationToken = default);

        Task MoveAbsoluteAsync(
            AxisId axisId,
            double targetPosition,
            CancellationToken cancellationToken = default);

        Task MoveRelativeAsync(
            AxisId axisId,
            double distance,
            CancellationToken cancellationToken = default);

        Task StartJogAsync(
            AxisId axisId,
            bool positiveDirection,
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
        /// <summary>
        /// 停止所有已经配置ADS映射的轴，并撤销使能。
        ///
        /// AxisDefinition.IsAdsMapped为true的轴会自动加入，
        /// MainWindow和App不需要逐根指定AxisId。
        /// </summary>
        Task StopAndDisableAllMappedAxesAsync(
            CancellationToken cancellationToken = default);
    }
}
