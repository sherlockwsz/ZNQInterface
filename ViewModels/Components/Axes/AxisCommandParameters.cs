using Prism.Mvvm;

namespace ZNQInterface.ViewModels.Components.Axes
{
    /// <summary>
    /// 单根轴由上位机设置的运动参数。
    /// </summary>
    public class AxisCommandParameters : BindableBase
    {
        private double _targetPosition;
        private double _relativeDistance;
        private double _velocity;
        private double _acceleration;
        private double _deceleration;
        private double _torque;
        private double _zeroPosition;

        public double TargetPosition
        {
            get => _targetPosition;
            set => SetProperty(ref _targetPosition, value);
        }

        public double RelativeDistance
        {
            get => _relativeDistance;
            set => SetProperty(ref _relativeDistance, value);
        }

        public double Velocity
        {
            get => _velocity;
            set => SetProperty(ref _velocity, value);
        }

        public double Acceleration
        {
            get => _acceleration;
            set => SetProperty(ref _acceleration, value);
        }

        public double Deceleration
        {
            get => _deceleration;
            set => SetProperty(ref _deceleration, value);
        }

        public double Torque
        {
            get => _torque;
            set => SetProperty(ref _torque, value);
        }

        public double ZeroPosition
        {
            get => _zeroPosition;
            set => SetProperty(ref _zeroPosition, value);
        }
    }
}