using Prism.Mvvm;

namespace ZNQInterface.ViewModels.Components.MaterialSlots
{
    /// <summary>
    /// 单个料位的显示数据。
    /// XAML只负责绑定，不负责判断具体状态。
    /// </summary>
    public sealed class MaterialSlotViewModel : BindableBase
    {
        private MaterialSlotState _state;

        public MaterialSlotViewModel(
            string rowLabel,
            int columnNumber,
            MaterialSlotState state = MaterialSlotState.Empty)
        {
            RowLabel = rowLabel;
            ColumnNumber = columnNumber;
            _state = state;
        }

        /// <summary>
        /// 行号：A、B、C、D。
        /// </summary>
        public string RowLabel
        {
            get;
        }

        /// <summary>
        /// 列号：1～6。
        /// </summary>
        public int ColumnNumber
        {
            get;
        }

        /// <summary>
        /// 完整料位编号，例如A1、D6。
        /// </summary>
        public string PositionCode =>
            $"{RowLabel}{ColumnNumber}";

        /// <summary>
        /// 当前料位状态。
        /// 后续ADS接收到PLC状态后，只需要修改该属性。
        /// </summary>
        public MaterialSlotState State
        {
            get => _state;

            set
            {
                if (!SetProperty(
                    ref _state,
                    value))
                {
                    return;
                }

                // State发生变化后，
                // 同时通知所有依赖State的显示属性更新。
                RaisePropertyChanged(nameof(StatusText));
                RaisePropertyChanged(nameof(ToolTipText));
            }
        }

        /// <summary>
        /// 料位状态显示文字。
        /// </summary>
        public string StatusText => State switch
        {
            MaterialSlotState.Unknown => "未知",
            MaterialSlotState.Empty => "空",
            MaterialSlotState.Unadjusted => "未调整",
            MaterialSlotState.PendingAdjustment => "待调整",
            MaterialSlotState.Adjusting => "调整中",
            MaterialSlotState.Qualified => "合格",
            MaterialSlotState.Unqualified => "不合格",
            _ => "未知"
        };

        /// <summary>
        /// 鼠标停留在指示灯上时显示的提示。
        /// </summary>
        public string ToolTipText =>
            $"{PositionCode}：{StatusText}";
    }
}