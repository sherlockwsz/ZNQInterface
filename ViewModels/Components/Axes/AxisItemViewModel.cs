using Prism.Mvvm;
using ZNQInterface.Models.Axes;

namespace ZNQInterface.ViewModels.Components.Axes
{
    /// <summary>
    /// 单根轴的完整界面对象。
    /// </summary>
    public class AxisItemViewModel : BindableBase
    {
        private string _motionStatusText = "未知";

        public AxisItemViewModel(
            AxisDefinition definition)
        {
            Definition = definition;
            Runtime = new AxisRuntimeData();

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
                    AxisMotionState.Unknown =>
                        "未知",

                    AxisMotionState.Disabled =>
                        "未使能",

                    AxisMotionState.Standstill =>
                        "停止",

                    AxisMotionState.Homing =>
                        "回零中",

                    AxisMotionState.MovingPositive =>
                        Definition.PositiveDirectionText,

                    AxisMotionState.MovingNegative =>
                        Definition.NegativeDirectionText,

                    AxisMotionState.InPosition =>
                        "已到位",

                    AxisMotionState.Stopping =>
                        "停止中",

                    AxisMotionState.Fault =>
                        "故障",

                    _ => "未知"
                };
        }
    }
}