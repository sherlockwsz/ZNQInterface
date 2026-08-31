using Prism.Mvvm;
using System;
using ZNQInterface.Models.Axes;

namespace ZNQInterface.ViewModels.Components.Axes
{
    /// <summary>
    /// 单根轴的完整界面对象。
    /// </summary>
    public class AxisItemViewModel : BindableBase
    {
        private string _motionStatusText = "未知";
        private bool _commandParametersInitialized;

        public AxisItemViewModel(
            AxisDefinition definition)
        {
            Definition = definition;
            Runtime = new AxisRuntimeData();
            Runtime.IsMapped = definition.IsAdsMapped;

            Runtime.PropertyChanged +=
                OnRuntimePropertyChanged;
        }

        /// <summary>
        /// 固定配置信息：轴号、名称、方向和单位。
        /// </summary>
        public AxisDefinition Definition { get; }

        /// <summary>
        /// PLC反馈的实时数据。
        /// </summary>
        public AxisRuntimeData Runtime { get; }
        /// <summary>
        /// 当前轴自己的调试参数。
        /// </summary>
        public AxisCommandParameters CommandParameters
        {
            get;
        } = new AxisCommandParameters();
        /// <summary>
        /// 界面显示的运动状态文字。
        /// </summary>
        public string MotionStatusText
        {
            get => _motionStatusText;
            private set => SetProperty(
                ref _motionStatusText,
                value);
        }

        private void OnRuntimePropertyChanged(
            object sender,
            System.ComponentModel.PropertyChangedEventArgs eventArgs)
        {
            if (eventArgs.PropertyName ==
                nameof(AxisRuntimeData.MotionState))
            {
                UpdateMotionStatusText();
            }
        }

        private void UpdateMotionStatusText()
        {
            MotionStatusText =
                Runtime.MotionState switch
                {
                    AxisMotionState.Undefined =>
                        "未知",

                    AxisMotionState.Disabled =>
                        "未使能",

                    AxisMotionState.NotReady =>
                        "未准备好",

                    AxisMotionState.Standstill =>
                        "停止",

                    AxisMotionState.Homing =>
                        "回零中",

                    AxisMotionState.Moving =>
                        "定位运动中",

                    AxisMotionState.Jogging =>
                        "点动中",

                    AxisMotionState.Stopping =>
                        "停止中",

                    AxisMotionState.Resetting =>
                        "复位中",

                    AxisMotionState.Error =>
                        "故障",

                    AxisMotionState.TorqueRunning =>
                        "力矩运行中",

                    _ => "未知"
                };
        }

        /// <summary>
        /// 第一次成功读取PLC后初始化手动调试输入参数。
        ///
        /// 目标位置使用当前实际位置，而不是默认0或PLC上一次目标，
        /// 防止WPF重新打开后误点绝对运动导致轴返回0位置。
        /// </summary>
        public void InitializeCommandParameters(
            double actualPosition,
            double positionVelocity,
            double jogVelocity)
        {
            if (_commandParametersInitialized)
            {
                return;
            }

            /*
             * 绝对运动输入框默认等于当前实际位置。
             * 即使操作员没有重新输入而误点按钮，
             * 也不会从当前位置突然运动到默认0位置。
             */
            CommandParameters.TargetPosition =
                Math.Round(
                    actualPosition,
                    3,
                    MidpointRounding.AwayFromZero);
            /*
             * 相对距离默认清零，
             * 防止以后加入参数保存后误用旧距离。
             */
            CommandParameters.RelativeDistance = 0.0;

            // 使用PLC当前定位速度初始化输入框。
            CommandParameters.PositionVelocity =
                positionVelocity;

            // 使用PLC当前点动速度初始化输入框。
            CommandParameters.JogVelocity =
                jogVelocity;

            _commandParametersInitialized = true;
        }
    }
}
