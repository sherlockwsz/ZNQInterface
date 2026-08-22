using Prism.Mvvm;
using ZNQInterface.Models.Product;
using System.Collections.ObjectModel;

namespace ZNQInterface.ViewModels.Pages
{
    /// <summary>
    /// 操作日志页面的数据和当前选中项。
    /// </summary>
    public class OperationLogViewModel : BindableBase
    {
        // 当前筛选后的产品记录。
        public ObservableCollection<ProductDataItem> FilteredProducts { get; }
            = new ObservableCollection<ProductDataItem>();

        // 当前选中的产品记录。
        private ProductDataItem? _selectedProduct;

        public ProductDataItem? SelectedProduct
        {
            get => _selectedProduct;
            set => SetProperty(ref _selectedProduct, value);
        }
    }
}