using Prism.Mvvm;

namespace ZNQInterface.ViewModels.Components
{
    /// <summary>
    /// 单个料位的显示状态
    /// </summary>
    public sealed class MaterialSlotViewModel : BindableBase
    {
        private bool _isQualified;

        public MaterialSlotViewModel(
            string rowLabel,
            int columnNumber,
            bool isQualified = true)
        {
            RowLabel = rowLabel;
            ColumnNumber = columnNumber;
            _isQualified = isQualified;
        }

        /// <summary>
        /// 行号：A、B、C、D
        /// </summary>
        public string RowLabel { get; }

        /// <summary>
        /// 列号：1～6
        /// </summary>
        public int ColumnNumber { get; }

        /// <summary>
        /// 完整料位编号，例如A1、D6
        /// </summary>
        public string PositionCode =>
            $"{RowLabel}{ColumnNumber}";

        /// <summary>
        /// 是否合格
        /// </summary>
        public bool IsQualified
        {
            get => _isQualified;
            set
            {
                if (SetProperty(ref _isQualified, value))
                {
                    RaisePropertyChanged(nameof(StatusText));
                }
            }
        }

        /// <summary>
        /// 状态显示文字
        /// </summary>
        public string StatusText =>
            IsQualified ? "合格" : "不合格";
    }
}