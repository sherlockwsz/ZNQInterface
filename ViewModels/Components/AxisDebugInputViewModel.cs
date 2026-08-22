using Prism.Mvvm;
using ZNQInterface.ViewModels.Pages.Axis;

namespace ZNQInterface.ViewModels
{
    /// <summary>
    /// 当前选中轴的手动调试输入参数
    /// </summary>
    public class AxisDebugInputViewModel : BindableBase
    {
        // 手动调试输入参数文本
        private string _absolutePositionText = "0.00"; // 绝对位置
        private string _velocityText = "10.00"; // 速度
        private string _relativeDistanceText = "1.000"; // 相对距离
        private string _accelerationText = "100.00"; // 加速度
        private string _decelerationText = "100.00"; // 减速度
        private string _torqueText = "100.00"; // 扭矩

        public string AbsolutePositionText
        {
            get => _absolutePositionText;
            set => SetProperty(ref _absolutePositionText, value);
        }

        public string VelocityText
        {
            get => _velocityText;
            set => SetProperty(ref _velocityText, value);
        }

        public string RelativeDistanceText
        {
            get => _relativeDistanceText;
            set => SetProperty(ref _relativeDistanceText, value);
        }

        public string AccelerationText
        {
            get => _accelerationText;
            set => SetProperty(ref _accelerationText, value);
        }

        public string DecelerationText
        {
            get => _decelerationText;
            set => SetProperty(ref _decelerationText, value);
        }
        public string TorqueText
        {
            get => _torqueText;
            set => SetProperty(ref _torqueText, value);
        }

        // 根据当前选中轴初始化调试输入值
        public void InitializeForAxis(AxisItemViewModel axis)
        {
            if (axis == null)
            {
                AbsolutePositionText = "0.00";
                return;
            }

            // 切换轴时，将目标位置初始化为实际位置
            AbsolutePositionText = axis.ActualPosition.ToString("F2");
            VelocityText = axis.ActualVelocity.ToString("F2");
            RelativeDistanceText = axis.ActualPosition.ToString("F2");
            AccelerationText = axis.ActualAcceleration.ToString("F2");
            DecelerationText = axis.ActualDeceleration.ToString("F2");
            TorqueText = axis.ActualTorque.ToString("F2");

            // 速度不建议使用实际速度初始化，
            // 可以保留上一次设定值，或读取该轴默认调试速度
        }
    }
}