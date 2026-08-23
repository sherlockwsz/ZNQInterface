using System.Collections.ObjectModel;
using Prism.Mvvm;
using ZNQInterface.ViewModels.Components;

namespace ZNQInterface.ViewModels.Pages
{
    /// <summary>
    /// 设备总览页面
    /// </summary>
    public class OverviewViewModel : BindableBase
    {
        public OverviewViewModel()
        {
            MaterialSlots =
                new ObservableCollection<MaterialSlotViewModel>();
            UnqualifiedMaterialSlots =
    new ObservableCollection<MaterialSlotViewModel>();
            string[] rowLabels =
            {
                "A",
                "B",
                "C",
                "D"
            };

            foreach (string rowLabel in rowLabels)
            {
                for (int columnNumber = 1;
                     columnNumber <= 6;
                     columnNumber++)
                {
                    MaterialSlots.Add(
                        new MaterialSlotViewModel(
                            rowLabel,
                            columnNumber,
                            isQualified: true));
                    // 不合格料位：暂时全部设置为不合格
                    UnqualifiedMaterialSlots.Add(
                        new MaterialSlotViewModel(
                            rowLabel,
                            columnNumber,
                            isQualified: false));
                }
            }
            LoadingProcess.LoadFromFile(
                @"C:\Users\Administrator\Desktop\TZD\TwinCAT_Project\ZNQInterface\ProcessData\LoadingProcess.txt");

            AdjustmentProcess.LoadFromFile(
                @"C:\Users\Administrator\Desktop\TZD\TwinCAT_Project\ZNQInterface\ProcessData\AdjustmentProcess.txt");

        }
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
        /// A1～D6共24个料位
        /// </summary>
        public ObservableCollection<MaterialSlotViewModel> MaterialSlots
        {
            get;
        }
        /// <summary>
        /// 不合格料位A1～D6，共24个料位。
        /// </summary>
        public ObservableCollection<MaterialSlotViewModel>
            UnqualifiedMaterialSlots
        {
            get;
        }
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