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
        private double _setPosition;
        private double _targetPosition;
        private double _actualVelocity;
        private double _setVelocity;
        /*
         * 操作员设置的定位速度。
         * 对应PLC：
         * GVL_AxisRuntime.Axis1.Set.Position.fVelocity
         *
         * 注意：这不是NC实时SetVelo。
         */
        private double _positionVelocitySetting;

        /*
         * 操作员设置的点动速度。
         * 对应PLC：
         * GVL_AxisRuntime.Axis1.Set.Jog.fVelocity
         */
        private double _jogVelocitySetting;
        private double _actualAcceleration;
        private double _setAcceleration;
        private double _followingError;
        private double _setTorque;
        private double _actualTorque;
        private double _actualRpm;

        private double _softwareLimitNegative;
        private double _softwareLimitPositive;

        private AxisMotionState _motionState;
        private AxisMotionDirection _motionDirection = AxisMotionDirection.None;// PLC反馈的轴当前坐标运动方向。
        private bool _isMapped;
        private bool _isReady;
        private bool _isBusy;
        private bool _isActive;
        private bool _isEnabled;
        private bool _isHomed;
        private bool _isCommunicationOk;
        private bool _positiveLimit;
        private bool _negativeLimit;
        private bool _hasFault;
        private bool _softLimitEnabled;
        private bool _softLimitReady;
        private bool _commandDone;
        private bool _commandAborted;
        private bool _commandRejected;

        private uint _errorCode;
        private uint _warningCode;
        private short _errorSource;
        private short _rejectReason;

        public bool IsMapped
        {
            get => _isMapped;
            set => SetProperty(ref _isMapped, value);
        }

        public double ActualPosition
        {
            get => _actualPosition;
            set => SetProperty(ref _actualPosition, value);
        }

        public double SetPosition
        {
            get => _setPosition;
            set => SetProperty(ref _setPosition, value);
        }

        public double TargetPosition
        {
            get => _targetPosition;
            set => SetProperty(ref _targetPosition, value);
        }

        public double ActualVelocity
        {
            get => _actualVelocity;
            set => SetProperty(ref _actualVelocity, value);
        }

        public double SetVelocity
        {
            get => _setVelocity;
            set => SetProperty(ref _setVelocity, value);
        }
        /// <summary>
        /// 定位运动设置速度。
        /// 对应PLC Set.Position.fVelocity。
        /// </summary>
        public double PositionVelocitySetting
        {
            get => _positionVelocitySetting;

            set => SetProperty(
                ref _positionVelocitySetting,
                value);
        }

        /// <summary>
        /// 点动运动设置速度。
        /// 对应PLC Set.Jog.fVelocity。
        /// </summary>
        public double JogVelocitySetting
        {
            get => _jogVelocitySetting;

            set => SetProperty(
                ref _jogVelocitySetting,
                value);
        }
        public double ActualAcceleration
        {
            get => _actualAcceleration;
            set => SetProperty(ref _actualAcceleration, value);
        }

        public double SetAcceleration
        {
            get => _setAcceleration;
            set => SetProperty(ref _setAcceleration, value);
        }

        public double FollowingError
        {
            get => _followingError;
            set => SetProperty(ref _followingError, value);
        }

        public double SetTorque
        {
            get => _setTorque;
            set => SetProperty(ref _setTorque, value);
        }

        public double ActualTorque
        {
            get => _actualTorque;
            set => SetProperty(ref _actualTorque, value);
        }

        public double ActualRpm
        {
            get => _actualRpm;
            set => SetProperty(ref _actualRpm, value);
        }

        public double SoftwareLimitNegative
        {
            get => _softwareLimitNegative;
            set => SetProperty(ref _softwareLimitNegative, value);
        }

        public double SoftwareLimitPositive
        {
            get => _softwareLimitPositive;
            set => SetProperty(ref _softwareLimitPositive, value);
        }

        public AxisMotionState MotionState
        {
            get => _motionState;
            set => SetProperty(ref _motionState, value);
        }
        /// <summary>
        /// PLC反馈的当前运动方向。
        ///
        /// 该属性只保存None、Positive或Negative，
        /// 不直接保存“前移、后移”等界面文字。
        /// </summary>
        public AxisMotionDirection MotionDirection
        {
            get => _motionDirection;
            set => SetProperty(ref _motionDirection, value);
        }
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        public bool IsReady
        {
            get => _isReady;
            set => SetProperty(ref _isReady, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
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

        public bool SoftLimitEnabled
        {
            get => _softLimitEnabled;
            set => SetProperty(ref _softLimitEnabled, value);
        }

        public bool SoftLimitReady
        {
            get => _softLimitReady;
            set => SetProperty(ref _softLimitReady, value);
        }

        public bool CommandDone
        {
            get => _commandDone;
            set => SetProperty(ref _commandDone, value);
        }

        public bool CommandAborted
        {
            get => _commandAborted;
            set => SetProperty(ref _commandAborted, value);
        }

        public bool CommandRejected
        {
            get => _commandRejected;
            set => SetProperty(ref _commandRejected, value);
        }

        public uint ErrorCode
        {
            get => _errorCode;
            set => SetProperty(ref _errorCode, value);
        }

        public uint WarningCode
        {
            get => _warningCode;
            set => SetProperty(ref _warningCode, value);
        }

        public short ErrorSource
        {
            get => _errorSource;
            set => SetProperty(ref _errorSource, value);
        }

        public short RejectReason
        {
            get => _rejectReason;
            set => SetProperty(ref _rejectReason, value);
        }
    }
}
