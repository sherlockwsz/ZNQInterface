using Prism.Mvvm;
using System.Windows.Media;

namespace ZNQInterface.ViewModels.Components
{
    /// <summary>
    /// 同轴度实时检测区域。
    /// </summary>
    public sealed class CoaxialityMonitorViewModel : BindableBase
    {
        private ImageSource _image;
        private double? _xDeviation;
        private double? _yDeviation;
        private double? _coaxiality;
        private bool? _isQualified;

        // 同轴度检测结果属性
        /// <summary>
        /// 同轴度实时检测图像。
        /// </summary>
        public ImageSource Image
        {
            get => _image;
            set => SetProperty(ref _image, value);
        }

        /// <summary>
        /// X方向偏差。
        /// </summary>
        public double? XDeviation
        {
            get => _xDeviation;
            set => SetProperty(ref _xDeviation, value);
        }

        /// <summary>
        /// Y方向偏差。
        /// </summary>
        public double? YDeviation
        {
            get => _yDeviation;
            set => SetProperty(ref _yDeviation, value);
        }

        /// <summary>
        /// 当前同轴度。
        /// </summary>
        public double? Coaxiality
        {
            get => _coaxiality;
            set => SetProperty(ref _coaxiality, value);
        }

        /// <summary>
        /// 同轴度是否合格。
        /// true：合格；false：不合格；null：尚未判定。
        /// </summary>
        public bool? IsQualified
        {
            get => _isQualified;
            set => SetProperty(ref _isQualified, value);
        }

        /// <summary>
        /// 更新检测结果。
        /// </summary>
        public void UpdateResult(
            ImageSource image,
            double xDeviation,
            double yDeviation,
            double coaxiality,
            bool? isQualified)
        {
            Image = image;
            XDeviation = xDeviation;
            YDeviation = yDeviation;
            Coaxiality = coaxiality;
            IsQualified = isQualified;
        }

        /// <summary>
        /// 清空检测结果。
        /// </summary>
        public void Clear()
        {
            Image = null;
            XDeviation = null;
            YDeviation = null;
            Coaxiality = null;
            IsQualified = null;
        }

        /// <summary>
        /// 创建界面预览数据。
        /// </summary>
        public static CoaxialityMonitorViewModel CreatePreview()
        {
            return new CoaxialityMonitorViewModel
            {
                XDeviation = 0.058,
                YDeviation = -0.036,
                Coaxiality = 0.026,
                IsQualified = true
            };
        }
    }
}