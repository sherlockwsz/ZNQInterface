#nullable enable
using System;
using System.Globalization;
using System.Linq;
using Prism.Commands;
using Prism.Mvvm;
using ZNQInterface.Services.Product;
using ZNQInterface.ViewModels.Components.Product;

namespace ZNQInterface.ViewModels.Pages
{
    public class ProductDataViewModel : BindableBase
    {
        private readonly ProductDataRecordingService _records;
        private string _search = "", _minimum = "", _maximum = "", _qualification = "All", _filterError = "";
        public DateRangeFilterViewModel DateFilter { get; } = new DateRangeFilterViewModel();
        // Use the existing pagination default; no second page-size definition.
        public ProductPaginationViewModel Pagination { get; } = new ProductPaginationViewModel();
        public DelegateCommand SearchProductCommand { get; }
        public string RecordingStatus => string.IsNullOrEmpty(_filterError) ? _records.Status : _filterError + "；" + _records.Status;
        public string ProductSearchText { get => _search; set { if (SetProperty(ref _search, value)) ApplyFilters(); } }
        public string MinimumCoaxialityText { get => _minimum; set { if (SetProperty(ref _minimum, value)) ApplyFilters(); } }
        public string MaximumCoaxialityText { get => _maximum; set { if (SetProperty(ref _maximum, value)) ApplyFilters(); } }
        public string SelectedQualificationStatus { get => _qualification; set { if (SetProperty(ref _qualification, value)) ApplyFilters(); } }
        public ProductDataViewModel(ProductDataRecordingService records)
        {
            _records = records;
            SearchProductCommand = new DelegateCommand(() => { _records.RequestHistoryReload(); ApplyFilters(); });
            DateFilter.PropertyChanged += (_, e) => {
                if (e.PropertyName == nameof(DateFilter.StartDate) || e.PropertyName == nameof(DateFilter.EndDate))
                { _records.RequestHistoryReload(); ApplyFilters(); }
            };
            // Recording service dispatches both Items and Status notifications to the UI thread.
            _records.PropertyChanged += (_, e) => {
                if (e.PropertyName == nameof(_records.Items)) ApplyFilters();
                if (e.PropertyName == nameof(_records.Status)) RaisePropertyChanged(nameof(RecordingStatus));
            };
            ApplyFilters();
        }
        private static bool Parse(string text, out double? number)
        {
            number = null;
            if (string.IsNullOrWhiteSpace(text)) return true;
            if (!(double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) || !double.IsFinite(value)) return false;
            number = value; return true;
        }
        private void ApplyFilters()
        {
            bool validMinimum = Parse(_minimum, out double? minimum), validMaximum = Parse(_maximum, out double? maximum);
            _filterError = !DateFilter.IsRangeValid ? DateFilter.ValidationMessage
                : !validMinimum || !validMaximum ? "同轴度范围请输入有效数值"
                : minimum.HasValue && maximum.HasValue && minimum > maximum ? "同轴度下限不能大于上限" : "";
            RaisePropertyChanged(nameof(RecordingStatus));
            Pagination.SetItems(_records.Items.Where(item => _filterError.Length == 0
                && (string.IsNullOrWhiteSpace(_search) || item.ProductNumber.Contains(_search.Trim(), StringComparison.OrdinalIgnoreCase))
                && DateFilter.Contains(item.DetectionTime)
                && (!minimum.HasValue || (item.Coaxiality.HasValue && item.Coaxiality.Value >= minimum.Value))
                && (!maximum.HasValue || (item.Coaxiality.HasValue && item.Coaxiality.Value <= maximum.Value))
                && (_qualification != "Qualified" || item.IsQualified)
                && (_qualification != "Unqualified" || !item.IsQualified)));
        }
    }
}
