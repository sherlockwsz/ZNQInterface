using Prism.Mvvm;
using System.Windows.Media;
using ZNQInterface.ViewModels.Components;

namespace ZNQInterface.ViewModels.Pages
{
    public class OverviewViewModel : BindableBase
    {
        /// <summary>
        /// 螺钉角度实时检测模块。
        /// </summary>
        public ScrewAngleMonitorViewModel ScrewAngleMonitor { get; } =
            ScrewAngleMonitorViewModel.CreatePreview();
        /// <summary>
        /// 同轴度实时检测模块。
        /// </summary>
        public CoaxialityMonitorViewModel CoaxialityMonitor { get; } =
            CoaxialityMonitorViewModel.CreatePreview();
    }
}