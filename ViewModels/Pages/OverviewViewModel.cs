using Prism.Mvvm;
using ZNQInterface.ViewModels.Components;
using ZNQInterface.ViewModels.Components.MaterialSlots;

namespace ZNQInterface.ViewModels.Pages
{
    /// <summary>
    /// 设备总览页面。
    /// 只负责组合各个相对独立的功能区域。
    /// </summary>
    public class OverviewViewModel : BindableBase
    {
        public OverviewViewModel()
        {
            LoadingProcess.LoadFromFile(
                @"C:\Users\Administrator\Desktop\TZD\TwinCAT_Project\ZNQInterface\ProcessData\LoadingProcess.txt");

            AdjustmentProcess.LoadFromFile(
                @"C:\Users\Administrator\Desktop\TZD\TwinCAT_Project\ZNQInterface\ProcessData\AdjustmentProcess.txt");

        }
        /// <summary>
        /// 设备总览页面的料位状态区域。
        /// 主料位、不合格料位以及后续料位统计均由该对象管理。
        /// </summary>
        public MaterialSlotStatusViewModel MaterialSlotStatus
        {
            get;
        } = new MaterialSlotStatusViewModel();

        /// <summary>
        /// 上下料流程显示内容。
        /// </summary>
        public ProcessTextViewModel LoadingProcess
        {
            get;
        } = new ProcessTextViewModel();

        /// <summary>
        /// 同轴度调整流程显示内容。
        /// </summary>
        public ProcessTextViewModel AdjustmentProcess
        {
            get;
        } = new ProcessTextViewModel();
        /// <summary>
        /// 螺钉角度实时检测模块
        /// </summary>
        public ScrewAngleMonitorViewModel ScrewAngleMonitor { get; } =
            ScrewAngleMonitorViewModel.CreatePreview();

        /// <summary>
        /// 同轴度实时检测模块
        /// </summary>
        public CoaxialityMonitorViewModel CoaxialityMonitor { get; } =
            CoaxialityMonitorViewModel.CreatePreview();
    }
}