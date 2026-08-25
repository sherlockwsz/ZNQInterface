using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZNQInterface.ViewModels.Pages.Axis
{
    /// <summary>
    /// 单个运动轴的显示状态和手动设定参数。
    /// </summary>
    public class AxisItemViewModel : BindableBase
    {
        private double _setPosition;
        private double _setVelocity;
        private string _groupName;
        private string _displayName;
        // 实际运动状态。
        public double ActualPosition { get; set; }
        public double ActualVelocity { get; set; }
        public double ActualAcceleration { get; set; }
        public double ActualDeceleration { get; set; }
        public double ActualTorque { get; set; }

        // 各参数显示单位。
        public string PositionUnit { get; set; }
        public string VelocityUnit { get; set; }
        public string AccelerationUnit { get; set; }
        public string DecelerationUnit { get; set; }
        public string TorqueUnit { get; set; }
        public string RelativeDistanceUnit { get; set; }

        // 当前运动状态。
        public string MotionStatus { get; set; }

        // 使能、回零、通信和限位状态。
        public bool IsEnabled { get; set; }
        public bool IsHomed { get; set; }
        public bool IsCommunicationOk { get; set; }

        public bool PositiveLimit { get; set; }
        public bool NegativeLimit { get; set; }

        // 故障状态。
        public bool HasFault { get; set; }

        // 目标运动参数。
        public double SetPosition
        {
            get => _setPosition;
            set => SetProperty(ref _setPosition, value);
        }
        public double SetVelocity
        {
            get => _setVelocity;
            set => SetProperty(ref _setVelocity, value);
        }

        // 轴所属功能组和显示名称。
        public string GroupName
        {
            get => _groupName;
            set => SetProperty(ref _groupName, value);
        }
        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }
        /// <summary>
        /// 相对运动第一个方向按钮显示文字。
        /// 例如：前移、左移、上移、正转。
        /// </summary>
        public string RelativeButton1Text { get; set; } = "正向运动";

        /// <summary>
        /// 第一个按钮对应的坐标方向。
        /// 1表示坐标正方向，-1表示坐标负方向。
        /// </summary>
        public double RelativeButton1Factor { get; set; } = 1.0;

        /// <summary>
        /// 相对运动第二个方向按钮显示文字。
        /// 例如：后移、右移、下移、反转。
        /// </summary>
        public string RelativeButton2Text { get; set; } = "负向运动";

        /// <summary>
        /// 第二个按钮对应的坐标方向。
        /// </summary>
        public double RelativeButton2Factor { get; set; } = -1.0;

    }
}
