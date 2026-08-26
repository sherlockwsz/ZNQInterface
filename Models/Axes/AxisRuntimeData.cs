using Prism.Mvvm;
using ZNQInterface.Models.Axes;

namespace ZNQInterface.ViewModels.Components.Axes
{
    /// <summary>
    /// 单根轴由PLC反馈的实时运行数据。
    /// </summary>
    public class AxisRuntimeData : BindableBase
    {
        private double _actualPosition;
        private double _actualVelocity;
        private double _actualTorque;

        private AxisMotionState _motionState;

        private bool _isEnabled;
        private bool _isHomed;
        private bool _isCommunicationOk;
        private bool _positiveLimit;
        private bool _negativeLimit;
        private bool _hasFault;

        private uint _errorCode;

        public double ActualPosition
        {
            get => _actualPosition;
            set => SetProperty(ref _actualPosition, value);
        }

        public double ActualVelocity
        {
            get => _actualVelocity;
            set => SetProperty(ref _actualVelocity, value);
        }

        public double ActualTorque
        {
            get => _actualTorque;
            set => SetProperty(ref _actualTorque, value);
        }

        public AxisMotionState MotionState
        {
            get => _motionState;
            set => SetProperty(ref _motionState, value);
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        public bool IsHomed
        {
            get => _isHomed;
            set => SetProperty(ref _isHomed, value);
        }

        public bool IsCommunicationOk
        {
            get => _isCommunicationOk;
            set => SetProperty(ref _isCommunicationOk, value);
        }

        public bool PositiveLimit
        {
            get => _positiveLimit;
            set => SetProperty(ref _positiveLimit, value);
        }

        public bool NegativeLimit
        {
            get => _negativeLimit;
            set => SetProperty(ref _negativeLimit, value);
        }

        public bool HasFault
        {
            get => _hasFault;
            set => SetProperty(ref _hasFault, value);
        }

        public uint ErrorCode
        {
            get => _errorCode;
            set => SetProperty(ref _errorCode, value);
        }
    }
}