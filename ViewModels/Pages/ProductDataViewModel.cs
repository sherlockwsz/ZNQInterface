using System;
using Prism.Mvvm;
using ZNQInterface.Models.Product;
using ZNQInterface.ViewModels.Components;

namespace ZNQInterface.ViewModels.Pages
{
    /// <summary>
    /// 产品检测数据页面的筛选与分页协调器。
    /// </summary>
    public class ProductDataViewModel : BindableBase
    {
        public string PageTitle => "产品数据";

        // 日期范围筛选器。
        public DateRangeFilterViewModel DateFilter { get; }

        // 产品数据分页器。
        public ProductPaginationViewModel Pagination { get; }

        public ProductDataViewModel()
        {
            // 初始化筛选和分页状态。
            DateFilter =
                new DateRangeFilterViewModel();

            // 每页显示20条
            Pagination =
                new ProductPaginationViewModel(pageSize: 20);

            // 加载界面预览数据。
            AddTestData();
        }

        // 添加界面预览数据。
        private void AddTestData()
        {
            Pagination.SetItems(
                new[]
                {
                    new ProductDataItem
                    {
                        DetectionTime =
                            DateTime.Now.AddMinutes(-20),

                        ProductNumber = "ZNQ_20260822",
                        Coaxiality = 0.035,
                        IsQualified = true
                    },

                    new ProductDataItem
                    {
                        DetectionTime =
                            DateTime.Now.AddMinutes(-10),

                        ProductNumber = "ZNQ_20260821",
                        Coaxiality = 0.082,
                        IsQualified = false
                    },

                    new ProductDataItem
                    {
                        DetectionTime =
                            DateTime.Now,

                        ProductNumber = "ZNQ_20260820",
                        Coaxiality = 0.018,
                        IsQualified = true
                    }
                });
        }
    }
}