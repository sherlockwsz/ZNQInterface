using Prism.Commands;
using Prism.Mvvm;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using ZNQInterface.Models.Axes;
using ZNQInterface.Services.Axes;
using ZNQInterface.ViewModels.Components.Axes;

namespace ZNQInterface.ViewModels.Pages
{
    /// <summary>
    /// 手动调试页面 ViewModel。
    /// 只负责参数校验和操作意图；PLC 符号、写入顺序及命令脉冲
    /// 全部封装在 IAxisCommandService 中。
    /// </summary>
    public class ManualControlViewModel : BindableBase
    {
        private readonly IAxisCommandService _axisCommandService;
        private AxisItemViewModel _selectedAxis;
        private AxisItemViewModel _selectedDamperAxis;
        private AxisItemViewModel _selectedTrayAxis;
        private AxisItemViewModel _selectedTurntableAxis;
        private AxisItemViewModel _selectedAdjustmentAxis;
        private AxisItemViewModel _selectedScrewdriverAxis;
        private AxisCommandParameters _debugInput = new AxisCommandParameters();
        private ManualMotionMode _selectedMotionMode = ManualMotionMode.Absolute;
        private string _lastCommandMessage = "等待操作";

        public ManualControlViewModel(
            AxisStatusViewModel axisStatus,
            IAxisCommandService axisCommandService)
        {
            AxisStatus = axisStatus
                ?? throw new ArgumentNullException(nameof(axisStatus));
            _axisCommandService = axisCommandService
                ?? throw new ArgumentNullException(nameof(axisCommandService));

            AbsoluteMoveCommand = new DelegateCommand(ExecuteAbsoluteMove);
            RelativeMoveCommand = new DelegateCommand<double?>(ExecuteRelativeMove);
            JogStartCommand = new DelegateCommand<double?>(ExecuteJogStart);
            JogStopCommand = new DelegateCommand(ExecuteJogStop);
            ToggleEnableCommand = new DelegateCommand(ExecuteToggleEnable);
            ResetCommand = new DelegateCommand(ExecuteReset);
            StopCommand = new DelegateCommand(ExecuteStop);
            // 只有点击该命令时，速度输入值才通过ADS写入PLC。
            WriteParametersCommand =
                new DelegateCommand(ExecuteWriteParameters);
            // 首轮 ADS 联调默认定位到唯一已映射的 Axis1。
            SelectedAxis = AxisStatus.DamperXAxis;
        }

        /// <summary>
        /// 整台设备 14 根轴的共享管理对象。
        /// Overview 和本页面使用同一个单例。
        /// </summary>
        public AxisStatusViewModel AxisStatus { get; }

        /// <summary>
        /// 当前轴自己的输入参数。切换轴时保留各轴独立输入值。
        /// </summary>
        public AxisCommandParameters DebugInput
        {
            get => _debugInput;
            private set => SetProperty(ref _debugInput, value);
        }

        /// <summary>
        /// 决定运动面板及速度输入框的显示方式：
        /// 绝对/相对使用定位速度，点动使用点动速度。
        /// </summary>
        public ManualMotionMode SelectedMotionMode
        {
            get => _selectedMotionMode;
            set
            {
                if (SetProperty(ref _selectedMotionMode, value))
                {
                    // 切换运动模式后，通知同一个输入框重新读取对应速度。
                    RaisePropertyChanged(nameof(CurrentVelocity));
                    RaisePropertyChanged(nameof(CurrentVelocityLabel));
                }
            }
        }
        /// <summary>
        /// 只有“已映射且当前通信正常”的轴允许手动操作。
        /// 因此其余 13 根未映射轴即使被选中，也不会发送任何 ADS 命令。
        /// </summary>
        public bool CanControlSelectedAxis =>
            SelectedAxis?.Runtime.IsMapped == true &&
            SelectedAxis.Runtime.IsCommunicationOk;

        public string LastCommandMessage
        {
            get => _lastCommandMessage;
            private set => SetProperty(ref _lastCommandMessage, value);
        }

        public DelegateCommand AbsoluteMoveCommand { get; }
        public DelegateCommand<double?> RelativeMoveCommand { get; }
        public DelegateCommand<double?> JogStartCommand { get; }
        public DelegateCommand JogStopCommand { get; }
        public DelegateCommand ToggleEnableCommand { get; }
        public DelegateCommand ResetCommand { get; }
        public DelegateCommand StopCommand { get; }
        /// <summary>
        /// 参数写入命令。
        /// 当前阶段只写入当前运动模式对应的速度。
        /// </summary>
        public DelegateCommand WriteParametersCommand { get; }
        private async void ExecuteAbsoluteMove()
        {
            AxisItemViewModel axis = SelectedAxis;
            if (axis == null)
            {
                return;
            }

            await ExecuteSafelyAsync(
                () => _axisCommandService.MoveAbsoluteAsync(
                    axis.Definition.AxisId,
                    DebugInput.TargetPosition),
                "绝对运动命令已发送");
        }

        private async void ExecuteRelativeMove(double? directionFactor)
        {
            AxisItemViewModel axis = SelectedAxis;
            if (axis == null || directionFactor == null)
            {
                return;
            }

            double distance =
                Math.Abs(DebugInput.RelativeDistance) *
                directionFactor.Value;

            await ExecuteSafelyAsync(
                () => _axisCommandService.MoveRelativeAsync(
                    axis.Definition.AxisId,
                    distance),
                "相对运动命令已发送");
        }

        private async void ExecuteJogStart(double? directionFactor)
        {
            AxisItemViewModel axis = SelectedAxis;
            if (axis == null || directionFactor == null)
            {
                return;
            }

            await ExecuteSafelyAsync(
                () => _axisCommandService.StartJogAsync(
                    axis.Definition.AxisId,
                    directionFactor.Value > 0),
                "点动开始；松开按钮停止");
        }

        private async void ExecuteJogStop()
        {
            AxisItemViewModel axis = SelectedAxis;
            if (axis == null || !axis.Definition.IsAdsMapped)
            {
                return;
            }

            await ExecuteSafelyAsync(
                () => _axisCommandService.StopJogAsync(
                    axis.Definition.AxisId),
                "点动已停止");
        }

        private async void ExecuteToggleEnable()
        {
            AxisItemViewModel axis = SelectedAxis;
            if (axis == null)
            {
                return;
            }

            bool enable = !axis.Runtime.IsEnabled;
            await ExecuteSafelyAsync(
                () => _axisCommandService.SetEnableAsync(
                    axis.Definition.AxisId,
                    enable),
                enable ? "使能命令已发送" : "掉使能命令已发送");
        }

        private async void ExecuteReset()
        {
            AxisItemViewModel axis = SelectedAxis;
            if (axis == null)
            {
                return;
            }

            await ExecuteSafelyAsync(
                () => _axisCommandService.ResetAsync(
                    axis.Definition.AxisId),
                "故障复位命令已发送");
        }

        private async void ExecuteStop()
        {
            AxisItemViewModel axis = SelectedAxis;
            if (axis == null)
            {
                return;
            }

            await ExecuteSafelyAsync(
                () => _axisCommandService.StopAsync(
                    axis.Definition.AxisId),
                "停止命令已发送");
        }
        /// <summary>
        /// 将当前界面显示的速度写入PLC。
        ///
        /// 绝对/相对模式写入定位速度；
        /// 点动模式写入点动速度；
        /// 本方法不启动轴运动。
        /// </summary>
        private async void ExecuteWriteParameters()
        {
            AxisItemViewModel axis = SelectedAxis;

            if (axis == null)
            {
                return;
            }

            double velocity;
            string velocityName;

            switch (SelectedMotionMode)
            {
                case ManualMotionMode.Absolute:
                case ManualMotionMode.Relative:

                    velocity = DebugInput.PositionVelocity;
                    velocityName = "定位速度";
                    break;

                case ManualMotionMode.Jog:

                    velocity = DebugInput.JogVelocity;
                    velocityName = "点动速度";
                    break;

                default:
                    LastCommandMessage = "写入失败：无法识别当前运动模式";
                    return;
            }

            await ExecuteSafelyAsync(
                () => _axisCommandService.WriteVelocityAsync(
                    axis.Definition.AxisId,
                    SelectedMotionMode,
                    velocity),
                $"{velocityName}已写入：{velocity:F2} " +
                $"{axis.Definition.Units.Velocity}");
        }
        private async Task ExecuteSafelyAsync(
            Func<Task> action,
            string successMessage)
        {
            try
            {
                await action();
                LastCommandMessage = successMessage;
            }
            catch (Exception exception)
            {
                LastCommandMessage = $"命令失败：{exception.Message}";
            }
        }

        /// <summary>
        /// 当前选中轴。切换时同步五个轴组的 SelectedItem，
        /// 并把参数输入切换到该轴自己的 CommandParameters。
        /// </summary>
        public AxisItemViewModel SelectedAxis
        {
            get => _selectedAxis;
            private set
            {
                AxisItemViewModel previousAxis = _selectedAxis;
                if (!SetProperty(ref _selectedAxis, value))
                {
                    return;
                }

                if (previousAxis != null)
                {
                    previousAxis.Runtime.PropertyChanged -=
                        OnSelectedRuntimePropertyChanged;

                    // 切换轴时尽力释放旧轴点动位，防止按住期间切换选择。
                    if (previousAxis.Definition.IsAdsMapped &&
                        previousAxis.Runtime.IsCommunicationOk)
                    {
                        _ = StopJogSilentlyAsync(previousAxis);
                    }
                }

                if (value != null)
                {
                    value.Runtime.PropertyChanged +=
                        OnSelectedRuntimePropertyChanged;
                }

                SetProperty(
                    ref _selectedDamperAxis,
                    IsAxisInGroup(AxisStatus.DamperLoadingGroup, value) ? value : null,
                    nameof(SelectedDamperAxis));
                SetProperty(
                    ref _selectedTrayAxis,
                    IsAxisInGroup(AxisStatus.TrayLoadingGroup, value) ? value : null,
                    nameof(SelectedTrayAxis));
                SetProperty(
                    ref _selectedTurntableAxis,
                    IsAxisInGroup(AxisStatus.TurntableGroup, value) ? value : null,
                    nameof(SelectedTurntableAxis));
                SetProperty(
                    ref _selectedAdjustmentAxis,
                    IsAxisInGroup(AxisStatus.AdjustmentGroup, value) ? value : null,
                    nameof(SelectedAdjustmentAxis));
                SetProperty(
                    ref _selectedScrewdriverAxis,
                    IsAxisInGroup(AxisStatus.ScrewdriverGroup, value) ? value : null,
                    nameof(SelectedScrewdriverAxis));

                DebugInput = value?.CommandParameters
                    ?? new AxisCommandParameters();

                RaisePropertyChanged(nameof(SelectedAxisTitle));
                RaisePropertyChanged(nameof(SelectedAxisDetailHeader));
                RaisePropertyChanged(nameof(SelectedAxisDebugHeader));
                RaisePropertyChanged(nameof(CanControlSelectedAxis));
                // 切换轴后，刷新当前速度输入框。
                RaisePropertyChanged(nameof(CurrentVelocity));
                LastCommandMessage = value?.Definition.IsAdsMapped == true
                    ? "等待 ADS 通信"
                    : "该轴尚未接入 ADS，本次不可操作";
            }
        }

        private void OnSelectedRuntimePropertyChanged(
            object sender,
            PropertyChangedEventArgs eventArgs)
        {
            if (eventArgs.PropertyName == nameof(AxisRuntimeData.IsMapped) ||
                eventArgs.PropertyName == nameof(AxisRuntimeData.IsCommunicationOk))
            {
                RaisePropertyChanged(nameof(CanControlSelectedAxis));
            }
        }

        private async Task StopJogSilentlyAsync(AxisItemViewModel axis)
        {
            try
            {
                await _axisCommandService.StopJogAsync(
                    axis.Definition.AxisId);
            }
            catch
            {
            }
        }

        public AxisItemViewModel SelectedDamperAxis
        {
            get => _selectedDamperAxis;
            set
            {
                if (SetProperty(ref _selectedDamperAxis, value) && value != null)
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
                if (SetProperty(ref _selectedTrayAxis, value) && value != null)
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
                if (SetProperty(ref _selectedTurntableAxis, value) && value != null)
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
                if (SetProperty(ref _selectedAdjustmentAxis, value) && value != null)
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
                if (SetProperty(ref _selectedScrewdriverAxis, value) && value != null)
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

                AxisGroupViewModel selectedGroup =
                    AxisStatus.Groups.FirstOrDefault(
                        group => group.Axes.Contains(SelectedAxis));

                return $"{selectedGroup?.DisplayName ?? "未分组"} —— " +
                       SelectedAxis.Definition.DisplayName;
            }
        }
        /// <summary>
        /// 当前界面显示和编辑的速度。
        ///
        /// 该属性只是界面代理：
        /// 绝对/相对模式映射到PositionVelocity；
        /// 点动模式映射到JogVelocity。
        ///
        /// 两种速度在AxisCommandParameters中仍然独立保存。
        /// </summary>
        public double CurrentVelocity
        {
            get
            {
                return SelectedMotionMode == ManualMotionMode.Jog
                    ? DebugInput.JogVelocity
                    : DebugInput.PositionVelocity;
            }
            set
            {
                if (SelectedMotionMode == ManualMotionMode.Jog)
                {
                    DebugInput.JogVelocity = value;
                }
                else
                {
                    DebugInput.PositionVelocity = value;
                }

                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// 当前速度输入框的名称。
        /// </summary>
        public string CurrentVelocityLabel =>
            SelectedMotionMode == ManualMotionMode.Jog
                ? "点动速度"
                : "定位速度";
        public string SelectedAxisDetailHeader =>
            $"当前选中轴：{SelectedAxisTitle}";

        public string SelectedAxisDebugHeader =>
            $"当前调试轴：{SelectedAxisTitle}";

        private static bool IsAxisInGroup(
            AxisGroupViewModel group,
            AxisItemViewModel axis) =>
            group != null && axis != null && group.Axes.Contains(axis);
    }
}
