using System;
using Prism.Mvvm;

namespace ZNQInterface.ViewModels.Components
{
    /// <summary>
    /// 日期范围筛选条件
    /// </summary>
    public class DateRangeFilterViewModel : BindableBase
    {
        private DateTime? _startDate;
        private DateTime? _endDate;

        /// <summary>
        /// 开始日期
        /// </summary>
        public DateTime? StartDate
        {
            get => _startDate;
            set
            {
                if (SetProperty(ref _startDate, value))
                {
                    RaisePropertyChanged(nameof(IsRangeValid));
                    RaisePropertyChanged(nameof(ValidationMessage));
                }
            }
        }

        /// <summary>
        /// 结束日期
        /// </summary>
        public DateTime? EndDate
        {
            get => _endDate;
            set
            {
                if (SetProperty(ref _endDate, value))
                {
                    RaisePropertyChanged(nameof(IsRangeValid));
                    RaisePropertyChanged(nameof(ValidationMessage));
                }
            }
        }

        /// <summary>
        /// 日期范围是否合法
        /// </summary>
        public bool IsRangeValid
        {
            get
            {
                if (!StartDate.HasValue || !EndDate.HasValue)
                {
                    return true;
                }

                return StartDate.Value.Date <= EndDate.Value.Date;
            }
        }

        /// <summary>
        /// 日期范围校验提示
        /// </summary>
        public string ValidationMessage =>
            IsRangeValid ? string.Empty : "开始日期不能晚于结束日期";

        /// <summary>
        /// 是否启用了日期筛选
        /// </summary>
        public bool HasDateFilter =>
            StartDate.HasValue || EndDate.HasValue;

        /// <summary>
        /// 清空日期范围
        /// </summary>
        public void Clear()
        {
            StartDate = null;
            EndDate = null;
        }

        /// <summary>
        /// 设置为最近若干天
        /// </summary>
        public void SetRecentDays(int days)
        {
            if (days <= 0)
            {
                return;
            }

            EndDate = DateTime.Today;
            StartDate = DateTime.Today.AddDays(-(days - 1));
        }

        /// <summary>
        /// 判断指定时间是否处于筛选范围内
        /// </summary>
        public bool Contains(DateTime dateTime)
        {
            DateTime date = dateTime.Date;

            if (StartDate.HasValue &&
                date < StartDate.Value.Date)
            {
                return false;
            }

            if (EndDate.HasValue &&
                date > EndDate.Value.Date)
            {
                return false;
            }

            return true;
        }
    }
}