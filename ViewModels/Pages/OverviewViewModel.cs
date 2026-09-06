using System;
using System.IO;
using Prism.Mvvm;
using ZNQInterface.ViewModels.Components.MaterialSlots;
using ZNQInterface.ViewModels.Components.Warehouse;
using ZNQInterface.ViewModels.Components.Axes;
using ZNQInterface.ViewModels.Components.Detection;
using ZNQInterface.ViewModels.Components.Processes;

namespace ZNQInterface.ViewModels.Pages
{
    /// <summary>
    /// 设备总览页面。
    /// 只负责组合各个相对独立的功能区域。
    /// </summary>
    public class OverviewViewModel : BindableBase
    {
        public OverviewViewModel(AxisStatusViewModel axisStatus)
        {
            /*
            该对象由Prism容器提供。
            因为已经在App.xaml.cs中注册为单例，
            所以这里不会重新创建14根轴。
            */
            AxisStatus = axisStatus;

            /*
             * 流程文本随程序一起发布，使用运行目录拼接路径。
             * 不再依赖开发电脑的固定绝对路径。
             */
            string processDataDirectory = Path.Combine(
                AppContext.BaseDirectory,
                "Data",
                "Processes");

            LoadingProcess.LoadFromFile(
                Path.Combine(
                    processDataDirectory,
                    "LoadingProcess.txt"));

            AdjustmentProcess.LoadFromFile(
                Path.Combine(
                    processDataDirectory,
                    "AdjustmentProcess.txt"));

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
        /// 设备总览页面的五层料仓状态模块。
        /// </summary>
        public WarehouseStatusViewModel WarehouseStatus
        {
            get;
        } = new WarehouseStatusViewModel();

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
        /// 整台设备14根轴的共享状态。
        /// 设备总览只负责读取和显示，不负责创建轴。
        /// </summary>
        public AxisStatusViewModel AxisStatus { get; }
    }
}
