using Prism.Mvvm;
using System.Linq;
using System.Collections.ObjectModel;
using ZNQInterface.ViewModels.Pages.Axis;

namespace ZNQInterface.ViewModels.Pages
{
    public class ManualControlViewModel : BindableBase
    {
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
            DamperLoadingAxes = new ObservableCollection<AxisItemViewModel>
            {
                //X轴模拟
                new AxisItemViewModel
                {
                    GroupName = "阻尼器上下料",
                    DisplayName = "X(左右)轴",
                    ActualPosition = 125.360,
                    ActualVelocity = 20.00,

                    SetPosition = 130.00,
                    SetVelocity = 0.042,
                    PositionUnit = "mm",
                    VelocityUnit = "mm/s",
                    MotionStatus = "回零中",
                    IsEnabled = true,
                    IsHomed = true,
                    IsCommunicationOk = false,
                    PositiveLimit = true,
                    NegativeLimit = false,
                    HasFault = true
                },
                //Y轴模拟
                new AxisItemViewModel
                {
                    GroupName = "阻尼器上下料",
                    DisplayName = "Y(前后)轴",
                    ActualPosition = 48.200,
                    ActualVelocity = 0,
                    SetPosition = 150.00,
                    SetVelocity = 50,

                    PositionUnit = "mm",
                    VelocityUnit = "mm/s",
                    MotionStatus = "定位中",
                    IsEnabled = false,
                    IsHomed = false,
                    IsCommunicationOk = true
                },
                //Z轴模拟
                new AxisItemViewModel
                {
                    GroupName = "阻尼器上下料",
                    DisplayName = "Z(上下)轴",
                    ActualPosition = 76.200,
                    ActualVelocity = 0,
                    SetPosition = 110.00,
                    SetVelocity = 60,

                    PositionUnit = "mm",
                    VelocityUnit = "mm/s",
                    MotionStatus = "已到位",
                    IsEnabled = false,
                    IsHomed = false,
                    IsCommunicationOk = true
                },

            };

            TrayLoadingAxes = new ObservableCollection<AxisItemViewModel>();
            TurntableAxes = new ObservableCollection<AxisItemViewModel>();
            //调整轴模拟
            AdjustmentAxes = new ObservableCollection<AxisItemViewModel>()
            {
                //X轴
                new AxisItemViewModel
                {
                    GroupName = "同轴度调整",
                    DisplayName = "X(左右)轴",
                    ActualPosition = 76.200,
                    ActualVelocity = 0,
                    SetPosition = 110.00,
                    SetVelocity = 40,

                    PositionUnit = "mm",
                    VelocityUnit = "mm/s",
                    MotionStatus = "已到位",
                    IsEnabled = false,
                    IsHomed = false,
                    IsCommunicationOk = true
                },
                //Y
                new AxisItemViewModel
                {
                    GroupName = "同轴度调整",
                    DisplayName = "Y(前后)轴",
                    ActualPosition = 76.200,
                    ActualVelocity = 0,
                    SetPosition = 110.00,
                    SetVelocity = 60,

                    PositionUnit = "mm",
                    VelocityUnit = "mm/s",
                    MotionStatus = "已到位",
                    IsEnabled = false,
                    IsHomed = false,
                    IsCommunicationOk = true
                },
                                new AxisItemViewModel
                {
                    GroupName = "同轴度调整",
                    DisplayName = "Z(上下)轴",
                    ActualPosition = 76.200,
                    ActualVelocity = 0,
                    SetPosition = 110.00,
                    SetVelocity = 60,

                    PositionUnit = "mm",
                    VelocityUnit = "mm/s",
                    MotionStatus = "已到位",
                    IsEnabled = false,
                    IsHomed = false,
                    IsCommunicationOk = true
                },

            };
            ScrewdriverAxes = new ObservableCollection<AxisItemViewModel>();

            SelectedAxis = DamperLoadingAxes.FirstOrDefault();
        }

        public ObservableCollection<AxisItemViewModel> DamperLoadingAxes { get; }
        public ObservableCollection<AxisItemViewModel> TrayLoadingAxes { get; }
        public ObservableCollection<AxisItemViewModel> TurntableAxes { get; }
        public ObservableCollection<AxisItemViewModel> AdjustmentAxes { get; }
        public ObservableCollection<AxisItemViewModel> ScrewdriverAxes { get; }

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

                RaisePropertyChanged(nameof(SelectedAxisTitle));
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
        public string SelectedAxisTitle
        {
            get
            {
                if (SelectedAxis == null)
                {
                    return "未选择调试轴";
                }

                return $"{SelectedAxis.GroupName} —— " +
                       $"{SelectedAxis.DisplayName} 详细信息";
            }
        }
    }
}