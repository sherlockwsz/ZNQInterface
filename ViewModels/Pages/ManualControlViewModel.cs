using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using ZNQInterface.ViewModels.Components;
using ZNQInterface.ViewModels.Pages.Axis;

namespace ZNQInterface.ViewModels.Pages
{
    public class ManualControlViewModel : BindableBase
    {
        // 当前调试输入区域。
        public AxisDebugInputViewModel DebugInput { get; }
        // 当前选中轴及各功能组的同步引用。
        private AxisItemViewModel _selectedAxis;
        private AxisItemViewModel _selectedDamperAxis;
        private AxisItemViewModel _selectedTrayAxis;
        private AxisItemViewModel _selectedTurntableAxis;
        private AxisItemViewModel _selectedAdjustmentAxis;
        private AxisItemViewModel _selectedScrewdriverAxis;
        private double _setPosition;
        private double _setVelocity;

        public ManualControlViewModel()
        {
            DebugInput = new AxisDebugInputViewModel();

            RelativeMoveCommand =
                new DelegateCommand<double?>(ExecuteRelativeMove);

            // 阻尼器上下料轴。
            DamperLoadingAxes = new ObservableCollection<AxisItemViewModel>
            {
                // X轴模拟
                new AxisItemViewModel
                {
                    GroupName = "阻尼器上下料",
                    DisplayName = "X(前后)轴",
                    ActualPosition = 125.360,
                    ActualVelocity = 20.00,
                    SetPosition = 130.00,
                    SetVelocity = 0.042,
                    PositionUnit = "mm",
                    VelocityUnit = "mm/s",
                    AccelerationUnit = "mm/s²",
                    DecelerationUnit = "mm/s²",
                    RelativeDistanceUnit = "mm",
                    TorqueUnit = "N·m",
                    MotionStatus = "回零中",
                    IsEnabled = true,
                    IsHomed = true,
                    IsCommunicationOk = false,
                    PositiveLimit = true,
                    NegativeLimit = false,
                    HasFault = true,
                    RelativeButton1Text = "前移",
                    RelativeButton1Factor = 1.0,

                    RelativeButton2Text = "后移",
                    RelativeButton2Factor = -1.0
                },
                // Y轴模拟
                new AxisItemViewModel
                {
                    GroupName = "阻尼器上下料",
                    DisplayName = "Y(左右)轴",
                    ActualPosition = 48.200,
                    ActualVelocity = 0,
                    SetPosition = 150.00,
                    SetVelocity = 50,
                    AccelerationUnit = "mm/s²",
                    DecelerationUnit = "mm/s²",
                    RelativeDistanceUnit = "mm",
                    TorqueUnit = "N·m",

                    PositionUnit = "mm",
                    VelocityUnit = "mm/s",
                    MotionStatus = "定位中",
                    IsEnabled = false,
                    IsHomed = false,
                    IsCommunicationOk = true,
                    RelativeButton1Text = "左移",
                    RelativeButton1Factor = 1.0,

                    RelativeButton2Text = "右移",
                    RelativeButton2Factor = -1.0

                },
                // Z轴模拟
                new AxisItemViewModel
                {
                    GroupName = "阻尼器上下料",
                    DisplayName = "Z(上下)轴",
                    ActualPosition = 76.200,
                    ActualVelocity = 0,
                    SetPosition = 110.00,
                    SetVelocity = 60,
                    AccelerationUnit = "mm/s²",
                    DecelerationUnit = "mm/s²",
                    RelativeDistanceUnit = "mm",
                    TorqueUnit = "N·m",

                    PositionUnit = "mm",
                    VelocityUnit = "mm/s",
                    MotionStatus = "已到位",
                    IsEnabled = false,
                    IsHomed = false,
                    IsCommunicationOk = true,
                    RelativeButton1Text = "上移",
                    RelativeButton1Factor = 1.0,

                    RelativeButton2Text = "下移",
                    RelativeButton2Factor = -1.0

                },
                // 转轴
                new AxisItemViewModel
                {
                    GroupName = "阻尼器上下料",
                    DisplayName = "转轴",
                    ActualPosition = 76.200,
                    ActualVelocity = 0,
                    SetPosition = 110.00,
                    SetVelocity = 60,
                    AccelerationUnit = "mm/s²",
                    DecelerationUnit = "mm/s²",
                    RelativeDistanceUnit = "mm",
                    TorqueUnit = "N·m",
                    PositionUnit = "mm",
                    VelocityUnit = "mm/s",
                    MotionStatus = "已到位",
                    IsEnabled = false,
                    IsHomed = false,
                    IsCommunicationOk = true,
                    RelativeButton1Text = "顺时针",
                    RelativeButton1Factor = 1.0,

                    RelativeButton2Text = "逆时针",
                    RelativeButton2Factor = -1.0

                },

                // 夹爪
                new AxisItemViewModel
                {
                    GroupName = "阻尼器上下料",
                    DisplayName = "夹爪轴",
                    ActualPosition = 0,
                    ActualVelocity = 0,
                    SetPosition = 0,
                    SetVelocity = 0,
                    AccelerationUnit = "mm/s²",
                    DecelerationUnit = "mm/s²",
                    RelativeDistanceUnit = "mm",
                    TorqueUnit = "N·m",

                    PositionUnit = "mm",
                    VelocityUnit = "mm/s",
                    MotionStatus = "已到位",
                    IsEnabled = false,
                    IsHomed = false,
                    IsCommunicationOk = true,
                    RelativeButton1Text = "夹紧",
                    RelativeButton1Factor = 1.0,

                    RelativeButton2Text = "松开",
                    RelativeButton2Factor = -1.0
                }

            };
            // 托盘上下料轴。
            TrayLoadingAxes = new ObservableCollection<AxisItemViewModel>()
            {
                // X轴
                new AxisItemViewModel
                {
                    GroupName = "托盘上下料",
                    DisplayName = "X(前后)轴",
                    ActualPosition = 76.200,
                    ActualVelocity = 0,
                    SetPosition = 110.00,
                    SetVelocity = 40,
                    AccelerationUnit = "mm/s²",
                    DecelerationUnit = "mm/s²",
                    RelativeDistanceUnit = "mm",
                    TorqueUnit = "N·m",

                    PositionUnit = "mm",
                    VelocityUnit = "mm/s",
                    MotionStatus = "已到位",
                    IsEnabled = false,
                    IsHomed = false,
                    IsCommunicationOk = true,
                    RelativeButton1Text = "前移",
                    RelativeButton1Factor = 1.0,

                    RelativeButton2Text = "后移",
                    RelativeButton2Factor = -1.0
                },
                // Y轴
                new AxisItemViewModel
                {
                    GroupName = "托盘上下料",
                    DisplayName = "Y(左右)轴",
                    ActualPosition = 76.200,
                    ActualVelocity = 0,
                    SetPosition = 110.00,
                    SetVelocity = 60,
                    AccelerationUnit = "mm/s²",
                    DecelerationUnit = "mm/s²",
                    RelativeDistanceUnit = "mm",
                    TorqueUnit = "N·m",

                    PositionUnit = "mm",
                    VelocityUnit = "mm/s",
                    MotionStatus = "已到位",
                    IsEnabled = false,
                    IsHomed = false,
                    IsCommunicationOk = true,
                    RelativeButton1Text = "左移",
                    RelativeButton1Factor = 1.0,

                    RelativeButton2Text = "右移",
                    RelativeButton2Factor = -1.0
                }

            };
            // 转台轴。
            TurntableAxes = new ObservableCollection<AxisItemViewModel>()
            {
                // 转轴
                new AxisItemViewModel
                {
                    GroupName = "转台轴",
                    DisplayName = "转轴",
                    ActualPosition = 76.200,
                    ActualVelocity = 0,
                    SetPosition = 110.00,
                    SetVelocity = 60,
                    AccelerationUnit = "mm/s²",
                    DecelerationUnit = "mm/s²",
                    RelativeDistanceUnit = "mm",
                    TorqueUnit = "N·m",
                    PositionUnit = "mm",
                    VelocityUnit = "mm/s",
                    MotionStatus = "已到位",
                    IsEnabled = false,
                    IsHomed = false,
                    IsCommunicationOk = true,
                    RelativeButton1Text = "顺时针",
                    RelativeButton1Factor = 1.0,

                    RelativeButton2Text = "逆时针",
                    RelativeButton2Factor = -1.0
                },

                // 夹爪
                new AxisItemViewModel
                {
                    GroupName = "转台轴",
                    DisplayName = "夹紧轴",
                    ActualPosition = 0,
                    ActualVelocity = 0,
                    SetPosition = 0,
                    SetVelocity = 0,
                    AccelerationUnit = "mm/s²",
                    DecelerationUnit = "mm/s²",
                    RelativeDistanceUnit = "mm",
                    TorqueUnit = "N·m",

                    PositionUnit = "mm",
                    VelocityUnit = "mm/s",
                    MotionStatus = "已到位",
                    IsEnabled = false,
                    IsHomed = false,
                    IsCommunicationOk = true,
                    RelativeButton1Text = "上移",
                    RelativeButton1Factor = 1.0,

                    RelativeButton2Text = "下移",
                    RelativeButton2Factor = -1.0
                }

            };
            // 同轴度调整轴。
            AdjustmentAxes = new ObservableCollection<AxisItemViewModel>()
            {
                // X轴
                new AxisItemViewModel
                {
                    GroupName = "同轴度调整",
                    DisplayName = "X(前后)轴",
                    ActualPosition = 76.200,
                    ActualVelocity = 0,
                    SetPosition = 110.00,
                    SetVelocity = 40,
                    AccelerationUnit = "mm/s²",
                    DecelerationUnit = "mm/s²",
                    RelativeDistanceUnit = "mm",
                    TorqueUnit = "N·m",

                    PositionUnit = "mm",
                    VelocityUnit = "mm/s",
                    MotionStatus = "已到位",
                    IsEnabled = false,
                    IsHomed = false,
                    IsCommunicationOk = true,
                    RelativeButton1Text = "前移",
                    RelativeButton1Factor = 1.0,

                    RelativeButton2Text = "后移",
                    RelativeButton2Factor = -1.0
                },
                // Y
                new AxisItemViewModel
                {
                    GroupName = "同轴度调整",
                    DisplayName = "Y(左右)轴",
                    ActualPosition = 76.200,
                    ActualVelocity = 0,
                    SetPosition = 110.00,
                    SetVelocity = 60,
                    AccelerationUnit = "mm/s²",
                    DecelerationUnit = "mm/s²",
                    RelativeDistanceUnit = "mm",
                    TorqueUnit = "N·m",

                    PositionUnit = "mm",
                    VelocityUnit = "mm/s",
                    MotionStatus = "已到位",
                    IsEnabled = false,
                    IsHomed = false,
                    IsCommunicationOk = true,
                    RelativeButton1Text = "左移",
                    RelativeButton1Factor = 1.0,

                    RelativeButton2Text = "右移",
                    RelativeButton2Factor = -1.0
                },
                // Z
                new AxisItemViewModel
                {
                    GroupName = "同轴度调整",
                    DisplayName = "Z(上下)轴",
                    ActualPosition = 76.200,
                    ActualVelocity = 0,
                    SetPosition = 110.00,
                    SetVelocity = 60,
                    AccelerationUnit = "mm/s²",
                    DecelerationUnit = "mm/s²",
                    RelativeDistanceUnit = "mm",
                    TorqueUnit = "N·m",

                    PositionUnit = "mm",
                    VelocityUnit = "mm/s",
                    MotionStatus = "已到位",
                    IsEnabled = false,
                    IsHomed = false,
                    IsCommunicationOk = true,
                    RelativeButton1Text = "上移",
                    RelativeButton1Factor = 1.0,

                    RelativeButton2Text = "下移",
                    RelativeButton2Factor = -1.0
                },

            };
            // 螺丝刀轴。
            ScrewdriverAxes = new ObservableCollection<AxisItemViewModel>()
            {
                // Y轴模拟
                new AxisItemViewModel
                {
                    GroupName = "螺丝刀轴",
                    DisplayName = "Y(左右)轴",
                    ActualPosition = 76.200,
                    ActualVelocity = 0,
                    SetPosition = 110.00,
                    SetVelocity = 60,
                    AccelerationUnit = "mm/s²",
                    DecelerationUnit = "mm/s²",
                    RelativeDistanceUnit = "mm",
                    TorqueUnit = "N·m",
                    PositionUnit = "mm",
                    VelocityUnit = "mm/s",
                    MotionStatus = "已到位",
                    IsEnabled = false,
                    IsHomed = false,
                    IsCommunicationOk = true,
                    RelativeButton1Text = "左移",
                    RelativeButton1Factor = 1.0,

                    RelativeButton2Text = "右移",
                    RelativeButton2Factor = -1.0

                },
                // Z轴模拟
                new AxisItemViewModel
                {
                    GroupName = "螺丝刀轴",
                    DisplayName = "Z(上下)轴",
                    ActualPosition = 76.200,
                    ActualVelocity = 0,
                    SetPosition = 110.00,
                    SetVelocity = 60,
                    AccelerationUnit = "mm/s²",
                    DecelerationUnit = "mm/s²",
                    RelativeDistanceUnit = "mm",
                    TorqueUnit = "N·m",
                    PositionUnit = "mm",
                    VelocityUnit = "mm/s",
                    MotionStatus = "已到位",
                    IsEnabled = false,
                    IsHomed = false,
                    IsCommunicationOk = true,
                    RelativeButton1Text = "上移",
                    RelativeButton1Factor = 1.0,

                    RelativeButton2Text = "下移",
                    RelativeButton2Factor = -1.0
                }

            };

            // 默认选中第一根轴。
            SelectedAxis = DamperLoadingAxes.FirstOrDefault();
        }
        /// <summary>
        /// 相对运动命令。
        /// 参数为运动方向系数：1.0或-1.0。
        /// </summary>
        public DelegateCommand<double?> RelativeMoveCommand
        {
            get;
        }
        private void ExecuteRelativeMove(double? directionFactor)
        {
            if (SelectedAxis == null ||
                directionFactor == null)
            {
                return;
            }

            // 读取用户输入的相对距离
            if (!double.TryParse(
                DebugInput.RelativeDistanceText,
                out double inputDistance))
            {
                return;
            }

            // 输入框只表示距离大小，正负方向由按钮决定
            double targetDistance =
                Math.Abs(inputDistance) *
                directionFactor.Value;

            /*
            后续通过ADS发送：

            1. 当前选中的轴 SelectedAxis
            2. 相对运动距离 targetDistance
            3. 相对运动触发命令

            例如：
            _axisService.MoveRelative(
                SelectedAxis.AxisIndex,
                targetDistance);
            */
        }
        // 各功能组轴集合。
        public ObservableCollection<AxisItemViewModel> DamperLoadingAxes { get; }
        public ObservableCollection<AxisItemViewModel> TrayLoadingAxes { get; }
        public ObservableCollection<AxisItemViewModel> TurntableAxes { get; }
        public ObservableCollection<AxisItemViewModel> AdjustmentAxes { get; }
        public ObservableCollection<AxisItemViewModel> ScrewdriverAxes { get; }

        public string SelectedAxisDetailHeader =>
            $"当前选中轴：{SelectedAxisTitle}";

        public string SelectedAxisDebugHeader =>
            $"当前调试轴：{SelectedAxisTitle}";

        // 当前选中轴及各组联动属性。
        public AxisItemViewModel SelectedAxis
        {
            get => _selectedAxis;
            private set
            {
                if (!SetProperty(ref _selectedAxis, value))
                {
                    return;
                }

                SetProperty(
                    ref _selectedDamperAxis,
                    value != null && DamperLoadingAxes.Contains(value)
                        ? value
                        : null,
                    nameof(SelectedDamperAxis));

                SetProperty(
                    ref _selectedTrayAxis,
                    value != null && TrayLoadingAxes.Contains(value)
                        ? value
                        : null,
                    nameof(SelectedTrayAxis));

                SetProperty(
                    ref _selectedTurntableAxis,
                    value != null && TurntableAxes.Contains(value)
                        ? value
                        : null,
                    nameof(SelectedTurntableAxis));

                SetProperty(
                    ref _selectedAdjustmentAxis,
                    value != null && AdjustmentAxes.Contains(value)
                        ? value
                        : null,
                    nameof(SelectedAdjustmentAxis));

                SetProperty(
                    ref _selectedScrewdriverAxis,
                    value != null && ScrewdriverAxes.Contains(value)
                        ? value
                        : null,
                    nameof(SelectedScrewdriverAxis));
                DebugInput.InitializeForAxis(value);
                RaisePropertyChanged(nameof(SelectedAxisTitle));
                RaisePropertyChanged(nameof(SelectedAxisDetailHeader));
                RaisePropertyChanged(nameof(SelectedAxisDebugHeader));
            }
        }
        public AxisItemViewModel SelectedDamperAxis
        {
            get => _selectedDamperAxis;
            set
            {
                if (SetProperty(ref _selectedDamperAxis, value) &&
                    value != null)
                {
                    SelectedAxis = value;
                }
            }
        }

        public AxisItemViewModel SelectedTrayAxis
        {
            get => _selectedTrayAxis;
            set
            {
                if (SetProperty(ref _selectedTrayAxis, value) &&
                    value != null)
                {
                    SelectedAxis = value;
                }
            }
        }

        public AxisItemViewModel SelectedTurntableAxis
        {
            get => _selectedTurntableAxis;
            set
            {
                if (SetProperty(ref _selectedTurntableAxis, value) &&
                    value != null)
                {
                    SelectedAxis = value;
                }
            }
        }

        public AxisItemViewModel SelectedAdjustmentAxis
        {
            get => _selectedAdjustmentAxis;
            set
            {
                if (SetProperty(ref _selectedAdjustmentAxis, value) &&
                    value != null)
                {
                    SelectedAxis = value;
                }
            }
        }

        public AxisItemViewModel SelectedScrewdriverAxis
        {
            get => _selectedScrewdriverAxis;
            set
            {
                if (SetProperty(ref _selectedScrewdriverAxis, value) &&
                    value != null)
                {
                    SelectedAxis = value;
                }
            }
        }
        // 当前选中轴标题。
        public string SelectedAxisTitle
        {
            get
            {
                if (SelectedAxis == null)
                {
                    return "未选择调试轴";
                }

                return $"{SelectedAxis.GroupName} —— " +
                       $"{SelectedAxis.DisplayName}";
            }
        }

    }
}