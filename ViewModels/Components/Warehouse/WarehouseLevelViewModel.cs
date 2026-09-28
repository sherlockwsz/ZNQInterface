using Prism.Mvvm;
namespace ZNQInterface.ViewModels.Components.Warehouse
{
    public sealed class WarehouseLevelViewModel : BindableBase
    {
        private WarehouseLevelState _state;
        public WarehouseLevelViewModel(
            int levelNumber,
            WarehouseLevelState state = WarehouseLevelState.Empty)
        {
            LevelNumber = levelNumber;
            _state = state;
        }

        /// <summary>
        /// 料仓层号。
        /// </summary>
        public int LevelNumber { get; }
        /// <summary>
        /// 料仓状态。
        /// </summary>
        public WarehouseLevelState State
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
        /// 料仓层号的界面显示文字。
        /// </summary>
        public string LevelText =>
            LevelNumber switch
            {
                1 => "一层",
                2 => "二层",
                3 => "三层",
                4 => "四层",
                5 => "五层",
                _ => $"{LevelNumber}层"
            };
        public string StatusText => State switch
        {
            WarehouseLevelState.Unknown => "未知",
            WarehouseLevelState.Empty => "无料盘",
            WarehouseLevelState.WaitingInspection => "待检",
            WarehouseLevelState.Inspecting => "检测中",
            WarehouseLevelState.Inspected => "已检",
            _ => "未知"
        };
        /// <summary>
        /// 完整料仓编号，例如1层。
        /// </summary>
        public string PositionCode =>
            LevelText;

        /// <summary>
        /// 鼠标停留在指示灯上时显示的提示。
        /// </summary>
        public string ToolTipText =>
            $"{PositionCode}：{StatusText}";
    }

}
