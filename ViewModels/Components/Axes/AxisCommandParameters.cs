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
        private double _positionVelocity = 20.0;
        private double _jogVelocity = 5.0;
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

        /// <summary>
        /// 绝对运动和相对运动共用的定位速度。
        /// 对应 PLC Set.Position.fVelocity。
        /// </summary>
        public double PositionVelocity
        {
            get => _positionVelocity;
            set => SetProperty(ref _positionVelocity, value);
        }

        /// <summary>
        /// 正向和负向点动共用的点动速度。
        /// 对应 PLC Set.Jog.fVelocity。
        /// </summary>
        public double JogVelocity
        {
            get => _jogVelocity;
            set => SetProperty(ref _jogVelocity, value);
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
