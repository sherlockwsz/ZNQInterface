using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Prism.Commands;
using Prism.Mvvm;
using ZNQInterface.Models.Product;

namespace ZNQInterface.ViewModels.Components
{
    /// <summary>
    /// 产品数据分页管理
    /// </summary>
    public class ProductPaginationViewModel : BindableBase
    {
        private readonly List<ProductDataItem> _allItems = new();

        private int _currentPage = 1;
        private int _pageSize;

        public ProductPaginationViewModel(int pageSize = 20)
        {
            _pageSize = pageSize > 0 ? pageSize : 20;

            PagedProducts =
                new ObservableCollection<ProductDataItem>();

            FirstPageCommand = new DelegateCommand(
                GoToFirstPage,
                () => CanGoPrevious);

            PreviousPageCommand = new DelegateCommand(
                GoToPreviousPage,
                () => CanGoPrevious);

            NextPageCommand = new DelegateCommand(
                GoToNextPage,
                () => CanGoNext);

            LastPageCommand = new DelegateCommand(
                GoToLastPage,
                () => CanGoNext);

            GoToPageCommand =
                new DelegateCommand<int?>(
                    page =>
                    {
                        if (page.HasValue)
                        {
                            GoToPage(page.Value);
                        }
                    });
        }

        /// <summary>
        /// 当前页显示的数据
        /// </summary>
        public ObservableCollection<ProductDataItem> PagedProducts
        {
            get;
        }

        /// <summary>
        /// 当前筛选结果总数
        /// </summary>
        public int TotalItemCount =>
            _allItems.Count;

        /// <summary>
        /// 当前页码
        /// </summary>
        public int CurrentPage
        {
            get => _currentPage;
            private set
            {
                int newPage = Math.Clamp(
                    value,
                    1,
                    TotalPages);

                if (SetProperty(ref _currentPage, newPage))
                {
                    RefreshCurrentPage();
                }
            }
        }

        /// <summary>
        /// 每页显示数量
        /// </summary>
        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (value <= 0)
                {
                    return;
                }

                if (SetProperty(ref _pageSize, value))
                {
                    // 修改每页数量后返回第一页
                    _currentPage = 1;

                    RaisePropertyChanged(nameof(CurrentPage));
                    RaisePropertyChanged(nameof(TotalPages));

                    RefreshCurrentPage();
                }
            }
        }

        /// <summary>
        /// 总页数，没有数据时显示1页
        /// </summary>
        public int TotalPages =>
            Math.Max(
                1,
                (int)Math.Ceiling(
                    TotalItemCount / (double)PageSize));

        // 分页状态
        public bool CanGoPrevious =>
            CurrentPage > 1;

        public bool CanGoNext =>
            CurrentPage < TotalPages;

        public DelegateCommand FirstPageCommand { get; }

        public DelegateCommand PreviousPageCommand { get; }

        public DelegateCommand NextPageCommand { get; }

        public DelegateCommand LastPageCommand { get; }

        /// <summary>
        /// 接收新的产品筛选结果
        /// </summary>
        public void SetItems(
            IEnumerable<ProductDataItem> products)
        {
            _allItems.Clear();

            if (products != null)
            {
                _allItems.AddRange(products);
            }

            _currentPage = 1;

            RaisePropertyChanged(nameof(CurrentPage));
            RaisePropertyChanged(nameof(TotalItemCount));
            RaisePropertyChanged(nameof(TotalPages));

            RefreshCurrentPage();
        }

        /// <summary>
        /// 增加一条产品数据
        /// </summary>
        public void AddItem(ProductDataItem product)
        {
            if (product == null)
            {
                return;
            }

            _allItems.Add(product);

            RaisePropertyChanged(nameof(TotalItemCount));
            RaisePropertyChanged(nameof(TotalPages));

            RefreshCurrentPage();
        }

        /// <summary>
        /// 清空产品数据
        /// </summary>
        public void Clear()
        {
            _allItems.Clear();
            _currentPage = 1;

            RaisePropertyChanged(nameof(CurrentPage));
            RaisePropertyChanged(nameof(TotalItemCount));
            RaisePropertyChanged(nameof(TotalPages));

            RefreshCurrentPage();
        }

        // 页码跳转
        public void GoToPage(int page)
        {
            CurrentPage = page;
        }

        private void GoToFirstPage()
        {
            CurrentPage = 1;
        }

        private void GoToPreviousPage()
        {
            if (CanGoPrevious)
            {
                CurrentPage--;
            }
        }

        private void GoToNextPage()
        {
            if (CanGoNext)
            {
                CurrentPage++;
            }
        }

        private void GoToLastPage()
        {
            CurrentPage = TotalPages;
        }

        /// <summary>
        /// 根据页码刷新当前页数据
        /// </summary>
        private void RefreshCurrentPage()
        {
            int validPage = Math.Clamp(
                _currentPage,
                1,
                TotalPages);

            if (_currentPage != validPage)
            {
                _currentPage = validPage;
                RaisePropertyChanged(nameof(CurrentPage));
            }

            IEnumerable<ProductDataItem> currentItems =
                _allItems
                    .Skip((CurrentPage - 1) * PageSize)
                    .Take(PageSize);

            PagedProducts.Clear();

            foreach (ProductDataItem item in currentItems)
            {
                PagedProducts.Add(item);
            }

            RaisePropertyChanged(nameof(CanGoPrevious));
            RaisePropertyChanged(nameof(CanGoNext));

            FirstPageCommand.RaiseCanExecuteChanged();
            PreviousPageCommand.RaiseCanExecuteChanged();
            NextPageCommand.RaiseCanExecuteChanged();
            LastPageCommand.RaiseCanExecuteChanged();

            RefreshPageNumbers();
        }

        /// <summary>
        /// 单个页码按钮
        /// </summary>
        public class PageNumberItemViewModel
        {
            public PageNumberItemViewModel(
                int pageNumber,
                bool isCurrent)
            {
                PageNumber = pageNumber;
                IsCurrent = isCurrent;
            }

            public int PageNumber { get; }

            public bool IsCurrent { get; }
        }
        /// <summary>
        /// 当前需要显示的页码按钮
        /// </summary>
        public ObservableCollection<PageNumberItemViewModel> PageNumbers
        {
            get;
        } = new ObservableCollection<PageNumberItemViewModel>();

        /// <summary>
        /// 点击数字页码命令
        /// </summary>
        public DelegateCommand<int?> GoToPageCommand { get; private set; }

        /// <summary>
        /// 刷新页码按钮。
        /// 最多显示5个数字页码，避免页数过多时挤满界面。
        /// </summary>
        private void RefreshPageNumbers()
        {
            PageNumbers.Clear();

            const int maxVisiblePageButtons = 5;

            int startPage =
                Math.Max(
                    1,
                    CurrentPage - maxVisiblePageButtons / 2);

            int endPage =
                Math.Min(
                    TotalPages,
                    startPage + maxVisiblePageButtons - 1);

            // 靠近最后一页时，尽量保持显示5个按钮
            startPage =
                Math.Max(
                    1,
                    endPage - maxVisiblePageButtons + 1);

            for (int page = startPage;
                 page <= endPage;
                 page++)
            {
                PageNumbers.Add(
                    new PageNumberItemViewModel(
                        page,
                        page == CurrentPage));
            }
        }
    }
}