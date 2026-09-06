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

            UpdateMotionStatusText();
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
        /// WPF设计映射与PLC bConfigured不一致。
        /// 出现该状态时禁止控制，防止轴号或PLC配置错误导致误动作。
        /// </summary>
        public bool HasConfigurationMismatch =>
            Runtime.IsConfigurationKnown &&
            Definition.IsExpectedConfigured != Runtime.IsConfigured;

        /// <summary>
        /// 当前轴已按设计接入并得到PLC配置确认。
        /// </summary>
        public bool IsAvailable =>
            Definition.IsExpectedConfigured &&
            Runtime.IsConfigurationKnown &&
            Runtime.IsConfigured &&
            !HasConfigurationMismatch;
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
            // 以下状态变化都可能改变界面运动文字：
            // 1. PLC运动状态变化；
            // 2. PLC运动方向变化；
            // 3. ADS通信状态变化。
            if (eventArgs.PropertyName ==
                    nameof(AxisRuntimeData.MotionState) ||
                eventArgs.PropertyName ==
                    nameof(AxisRuntimeData.MotionDirection) ||
                eventArgs.PropertyName ==
                    nameof(AxisRuntimeData.IsCommunicationOk) ||
                eventArgs.PropertyName ==
                    nameof(AxisRuntimeData.IsConfigurationKnown) ||
                eventArgs.PropertyName ==
                    nameof(AxisRuntimeData.IsConfigured))
            {
                RaisePropertyChanged(nameof(HasConfigurationMismatch));
                RaisePropertyChanged(nameof(IsAvailable));
                UpdateMotionStatusText();
            }
        }

        /// <summary>
        /// 根据PLC运动状态和运动方向生成界面状态文字。
        ///
        /// 显示优先级：
        /// 通信断开 > 故障/停止/复位等状态 > 运动方向。
        ///
        /// 只有PLC状态为Moving或Jogging时，
        /// 才把Positive和Negative转换为机械方向文字。
        /// </summary>
        private void UpdateMotionStatusText()
        {
            if (!Definition.IsExpectedConfigured)
            {
                MotionStatusText = HasConfigurationMismatch
                    ? "配置不一致"
                    : "未接入";
                return;
            }

            if (HasConfigurationMismatch)
            {
                MotionStatusText = "配置不一致";
                return;
            }

            // 预期接入的轴必须同时通过ADS通信和PLC配置校验。
            if (Runtime.IsMapped &&
                !Runtime.IsCommunicationOk)
            {
                MotionStatusText = "通信断开";
                return;
            }

            switch (Runtime.MotionState)
            {
                case AxisMotionState.Undefined:
                    MotionStatusText = "未知";
                    break;

                case AxisMotionState.Disabled:
                    MotionStatusText = "未使能";
                    break;

                case AxisMotionState.NotReady:
                    MotionStatusText = "未准备好";
                    break;

                case AxisMotionState.Standstill:
                    // 即使PLC仍保留旧目标位置，只要状态为Standstill，
                    // 就必须显示停止。
                    MotionStatusText = "停止";
                    break;

                case AxisMotionState.Homing:
                    MotionStatusText = "回零中";
                    break;

                case AxisMotionState.Moving:
                case AxisMotionState.Jogging:
                    // 定位和点动使用同一套方向映射。
                    MotionStatusText = GetMotionDirectionText();
                    break;

                case AxisMotionState.Stopping:
                    // 停止过程中不显示原来的运动方向。
                    MotionStatusText = "停止中";
                    break;

                case AxisMotionState.Resetting:
                    MotionStatusText = "复位中";
                    break;

                case AxisMotionState.Error:
                    MotionStatusText = "故障";
                    break;

                case AxisMotionState.TorqueRunning:
                    MotionStatusText = "力矩运行中";
                    break;

                default:
                    MotionStatusText = "未知";
                    break;
            }
        }
        /// <summary>
        /// 将PLC坐标方向转换为当前轴对应的机械方向文字。
        ///
        /// 例如：
        /// X轴：Positive=前移，Negative=后移；
        /// Y轴：Positive=左移，Negative=右移；
        /// Z轴：Positive=上移，Negative=下移。
        /// </summary>
        private string GetMotionDirectionText()
        {
            switch (Runtime.MotionDirection)
            {
                case AxisMotionDirection.Positive:

                    return string.IsNullOrWhiteSpace(
                        Definition.PositiveDirectionText)
                        ? "正向运动"
                        : Definition.PositiveDirectionText;

                case AxisMotionDirection.Negative:

                    return string.IsNullOrWhiteSpace(
                        Definition.NegativeDirectionText)
                        ? "负向运动"
                        : Definition.NegativeDirectionText;

                case AxisMotionDirection.None:
                default:

                    // 运动命令刚被PLC接受时，速度方向可能尚未建立。
                    // 此时显示“运动中”，而不是错误地猜测方向。
                    return "运动中";
            }
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
