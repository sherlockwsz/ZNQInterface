using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Linq;
using ZNQInterface.ViewModels.Components.Axes;

namespace ZNQInterface.ViewModels.Pages
{
    /// <summary>
    /// 手动调试页面ViewModel。
    /// 
    /// 负责：
    /// 1. 从统一轴管理器中选择当前调试轴；
    /// 2. 管理当前轴的调试输入参数；
    /// 3. 执行绝对、相对、点动等调试命令；
    /// 4. 不再重复创建14根轴。
    /// </summary>
    public class ManualControlViewModel : BindableBase
    {
        private AxisItemViewModel _selectedAxis;

        private AxisItemViewModel _selectedDamperAxis;
        private AxisItemViewModel _selectedTrayAxis;
        private AxisItemViewModel _selectedTurntableAxis;
        private AxisItemViewModel _selectedAdjustmentAxis;
        private AxisItemViewModel _selectedScrewdriverAxis;

        public ManualControlViewModel(
            AxisStatusViewModel axisStatus)
        {
            AxisStatus =
                axisStatus
                ?? throw new ArgumentNullException(
                    nameof(axisStatus));

            /*
            必须先创建DebugInput，
            然后才能设置SelectedAxis。

            因为SelectedAxis的Setter中会调用
            InitializeDebugInputForAxis()。
            */
            RelativeMoveCommand =
                new DelegateCommand<double?>(
                    ExecuteRelativeMove);

            // 默认选择阻尼器上下料X轴。
            SelectedAxis =
                AxisStatus.DamperXAxis;

            System.Diagnostics.Debug.WriteLine(
    $"全部轴数量：{AxisStatus.AllAxes.Count}");

            System.Diagnostics.Debug.WriteLine(
                $"阻尼器轴数量：{AxisStatus.DamperLoadingGroup?.Axes.Count ?? -1}");
        }

        /// <summary>
        /// 整台设备14根轴的共享管理对象。
        /// 与设备总览使用同一个实例。
        /// </summary>
        public AxisStatusViewModel AxisStatus
        {
            get;
        }

        /// <summary>
        /// 当前调试轴的输入参数。
        /// 包括目标位置、相对距离、速度、加速度和力矩等。
        /// </summary>
        private AxisCommandParameters _debugInput =
            new AxisCommandParameters();

        public AxisCommandParameters DebugInput
        {
            get => _debugInput;

            private set => SetProperty(
                ref _debugInput,
                value);
        }

        /// <summary>
        /// 相对运动命令。
        /// CommandParameter为方向系数：
        /// 1.0表示正方向，-1.0表示负方向。
        /// </summary>
        public DelegateCommand<double?> RelativeMoveCommand
        {
            get;
        }

        /// <summary>
        /// 执行相对运动。
        /// 当前只完成输入处理，后续再调用ADS服务。
        /// </summary>
        private void ExecuteRelativeMove(
            double? directionFactor)
        {
            if (SelectedAxis == null ||
                directionFactor == null)
            {
                return;
            }

            /*
            DebugInput.RelativeDistance本身就是double，
            不需要再调用double.TryParse()。
            */
            double inputDistance =
                DebugInput.RelativeDistance;

            // 排除无效数字。
            if (double.IsNaN(inputDistance) ||
                double.IsInfinity(inputDistance))
            {
                return;
            }

            /*
            输入框只表示距离大小，
            正负方向由两个方向按钮决定。
            */
            double targetDistance =
                Math.Abs(inputDistance) *
                directionFactor.Value;

            /*
            后续通过ADS服务发送：

            AxisId axisId =
                SelectedAxis.Definition.AxisId;

            _axisCommandService.MoveRelative(
                axisId,
                targetDistance);
            */
        }

        /// <summary>
        /// 当前选中的轴。
        /// 切换任意轴组中的选中轴时，
        /// 都会同步更新该属性。
        /// </summary>
        public AxisItemViewModel SelectedAxis
        {
            get => _selectedAxis;

            private set
            {
                if (!SetProperty(
                    ref _selectedAxis,
                    value))
                {
                    return;
                }

                /*
                根据当前轴所属的轴组，
                同步五个ListBox的SelectedItem。

                当前轴属于哪个组，
                对应组的SelectedItem就等于当前轴；
                其他组的SelectedItem设置为null。
                */
                SetProperty(
                    ref _selectedDamperAxis,
                    IsAxisInGroup(
                        AxisStatus.DamperLoadingGroup,
                        value)
                        ? value
                        : null,
                    nameof(SelectedDamperAxis));

                SetProperty(
                    ref _selectedTrayAxis,
                    IsAxisInGroup(
                        AxisStatus.TrayLoadingGroup,
                        value)
                        ? value
                        : null,
                    nameof(SelectedTrayAxis));

                SetProperty(
                    ref _selectedTurntableAxis,
                    IsAxisInGroup(
                        AxisStatus.TurntableGroup,
                        value)
                        ? value
                        : null,
                    nameof(SelectedTurntableAxis));

                SetProperty(
                    ref _selectedAdjustmentAxis,
                    IsAxisInGroup(
                        AxisStatus.AdjustmentGroup,
                        value)
                        ? value
                        : null,
                    nameof(SelectedAdjustmentAxis));

                SetProperty(
                    ref _selectedScrewdriverAxis,
                    IsAxisInGroup(
                        AxisStatus.ScrewdriverGroup,
                        value)
                        ? value
                        : null,
                    nameof(SelectedScrewdriverAxis));

                // 根据新选中的轴初始化调试输入参数。
                DebugInput =
                    value?.CommandParameters
                    ?? new AxisCommandParameters();

                RaisePropertyChanged(
                    nameof(SelectedAxisTitle));

                RaisePropertyChanged(
                    nameof(SelectedAxisDetailHeader));

                RaisePropertyChanged(
                    nameof(SelectedAxisDebugHeader));
            }
        }

        /// <summary>
        /// 阻尼器上下料轴组当前选中的轴。
        /// </summary>
        public AxisItemViewModel SelectedDamperAxis
        {
            get => _selectedDamperAxis;

            set
            {
                if (SetProperty(
                        ref _selectedDamperAxis,
                        value) &&
                    value != null)
                {
                    SelectedAxis = value;
                }
            }
        }

        /// <summary>
        /// 料盘上下料轴组当前选中的轴。
        /// </summary>
        public AxisItemViewModel SelectedTrayAxis
        {
            get => _selectedTrayAxis;

            set
            {
                if (SetProperty(
                        ref _selectedTrayAxis,
                        value) &&
                    value != null)
                {
                    SelectedAxis = value;
                }
            }
        }

        /// <summary>
        /// 转台轴组当前选中的轴。
        /// </summary>
        public AxisItemViewModel SelectedTurntableAxis
        {
            get => _selectedTurntableAxis;

            set
            {
                if (SetProperty(
                        ref _selectedTurntableAxis,
                        value) &&
                    value != null)
                {
                    SelectedAxis = value;
                }
            }
        }

        /// <summary>
        /// 同轴度调整轴组当前选中的轴。
        /// </summary>
        public AxisItemViewModel SelectedAdjustmentAxis
        {
            get => _selectedAdjustmentAxis;

            set
            {
                if (SetProperty(
                        ref _selectedAdjustmentAxis,
                        value) &&
                    value != null)
                {
                    SelectedAxis = value;
                }
            }
        }

        /// <summary>
        /// 螺丝刀轴组当前选中的轴。
        /// </summary>
        public AxisItemViewModel SelectedScrewdriverAxis
        {
            get => _selectedScrewdriverAxis;

            set
            {
                if (SetProperty(
                        ref _selectedScrewdriverAxis,
                        value) &&
                    value != null)
                {
                    SelectedAxis = value;
                }
            }
        }

        /// <summary>
        /// 当前选中轴标题。
        /// 轴组名称从AxisGroupViewModel中取得，
        /// 轴名称从AxisDefinition中取得。
        /// </summary>
        public string SelectedAxisTitle
        {
            get
            {
                if (SelectedAxis == null)
                {
                    return "未选择调试轴";
                }

                AxisGroupViewModel selectedGroup =
                    AxisStatus.Groups.FirstOrDefault(
                        group =>
                            group.Axes.Contains(
                                SelectedAxis));

                string groupName =
                    selectedGroup?.DisplayName
                    ?? "未分组";

                string axisName =
                    SelectedAxis
                        .Definition
                        .DisplayName;

                return $"{groupName} —— {axisName}";
            }
        }

        /// <summary>
        /// 当前选中轴详细数据区域标题。
        /// </summary>
        public string SelectedAxisDetailHeader =>
            $"当前选中轴：{SelectedAxisTitle}";

        /// <summary>
        /// 当前调试轴区域标题。
        /// </summary>
        public string SelectedAxisDebugHeader =>
            $"当前调试轴：{SelectedAxisTitle}";

        /// <summary>
        /// 判断指定轴是否属于某个轴组。
        /// </summary>
        private static bool IsAxisInGroup(
            AxisGroupViewModel group,
            AxisItemViewModel axis)
        {
            return group != null &&
                   axis != null &&
                   group.Axes.Contains(axis);
        }
        /// <summary>
        /// 轴定义中的正方向。
        /// </summary>
        public double PositiveDirectionFactor => 1.0;

        /// <summary>
        /// 轴定义中的负方向。
        /// </summary>
        public double NegativeDirectionFactor => -1.0;
    }
}