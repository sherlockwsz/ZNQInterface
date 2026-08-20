using Prism.Mvvm;
using System.Windows.Media;

namespace ZNQInterface.ViewModels.Components
{
    /// <summary>
    /// 螺钉角度实时检测区域。
    /// </summary>
    public sealed class ScrewAngleMonitorViewModel : BindableBase
    {
        private ImageSource _image;
        private double? _currentAngle;

        /// <summary>
        /// 实时检测图像。
        /// </summary>
        public ImageSource Image
        {
            get => _image;
            set => SetProperty(ref _image, value);
        }

        /// <summary>
        /// 当前检测角度。
        /// null表示暂时没有角度数据。
        /// </summary>
        public double? CurrentAngle
        {
            get => _currentAngle;
            set => SetProperty(ref _currentAngle, value);
        }

        /// <summary>
        /// 更新检测结果。
        /// </summary>
        public void UpdateResult(
            ImageSource image,
            double angle)
        {
            Image = image;
            CurrentAngle = angle;
        }

        /// <summary>
        /// 清空检测结果。
        /// </summary>
        public void Clear()
        {
            Image = null;
            CurrentAngle = null;
        }

        /// <summary>
        /// 创建界面预览数据。
        /// </summary>
        public static ScrewAngleMonitorViewModel CreatePreview()
        {
            return new ScrewAngleMonitorViewModel
            {
                CurrentAngle = 47.68
            };
        }
    }
}