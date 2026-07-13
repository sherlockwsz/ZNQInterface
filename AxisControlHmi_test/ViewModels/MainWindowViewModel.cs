using AxisControlHmi_test.Models;
using AxisControlHmi_test.Services;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace AxisControlHmi_test.ViewModels
{
    public sealed class MainWindowViewModel : BindableBase
    {
        private readonly IAxisService _axisService;
        private readonly DispatcherTimer _statusTimer;
        private double _actualPosition;
        private double _actualVelocity;
        private double _actualRpm;
        private double _relativeDistance = 10;
        private double _absolutePosition;
        private bool _isEnabled;
        private bool _hasFault;
        private bool _isMoving;
        private bool _isAdsConnected;
        private bool _jogPositiveActive;
        private bool _jogNegativeActive;
        private short _motionState;
        private uint _lastErrorId;
        private uint _lastRejectReason;
        private bool _wasCommandRejected;
        private string? _lastConnectionError;

        public MainWindowViewModel(IAxisService axisService)
        {
            _axisService = axisService;
            ConnectCommand = new DelegateCommand(ConnectToPlc);
            EnableCommand = new DelegateCommand(EnableAxis);
            ResetCommand = new DelegateCommand(ResetAxis);
            StopCommand = new DelegateCommand(StopAxis);
            MoveRelativeCommand = new DelegateCommand(MoveRelative);
            MoveAbsoluteCommand = new DelegateCommand(MoveAbsolute);
            JogPositivePressedCommand = new DelegateCommand(BeginJogPositive);
            JogPositiveReleasedCommand = new DelegateCommand(EndJogPositive);
            JogNegativePressedCommand = new DelegateCommand(BeginJogNegative);
            JogNegativeReleasedCommand = new DelegateCommand(EndJogNegative);
            ClearLogCommand = new DelegateCommand(Logs.Clear);

            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _statusTimer.Tick += (_, _) => RefreshStatus();
            _statusTimer.Start();

            AddLog("信息", "HMI 已启动，请点击“连接至PLC”建立 ADS 通信。");
        }

        public string Title => "Axis Control HMI";
        public ObservableCollection<LogEntry> Logs { get; } = new();

        public double ActualPosition
        {
            get => _actualPosition;
            private set => SetProperty(ref _actualPosition, value);
        }

        public double ActualVelocity
        {
            get => _actualVelocity;
            private set => SetProperty(ref _actualVelocity, value);
        }

        public double ActualRpm
        {
            get => _actualRpm;
            private set => SetProperty(ref _actualRpm, value);
        }

        public double RelativeDistance
        {
            get => _relativeDistance;
            set => SetProperty(ref _relativeDistance, value);
        }

        public double AbsolutePosition
        {
            get => _absolutePosition;
            set => SetProperty(ref _absolutePosition, value);
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            private set
            {
                if (SetProperty(ref _isEnabled, value)) RaiseAxisStateChanged();
            }
        }

        public bool HasFault
        {
            get => _hasFault;
            private set
            {
                if (SetProperty(ref _hasFault, value)) RaiseAxisStateChanged();
            }
        }

        public bool IsMoving
        {
            get => _isMoving;
            private set
            {
                if (SetProperty(ref _isMoving, value)) RaiseAxisStateChanged();
            }
        }

        public bool IsAdsConnected
        {
            get => _isAdsConnected;
            private set
            {
                if (SetProperty(ref _isAdsConnected, value)) RaiseAxisStateChanged();
            }
        }

        public string AxisStateText => !IsAdsConnected ? "ADS 未连接" : _motionState switch
        {
            0 => "未使能",
            1 => "已使能 / 就绪",
            2 => "定位运动中",
            3 => "点动中",
            4 => "停止中",
            5 => "故障",
            6 => "复位中",
            _ => "状态未知"
        };

        public string AxisStateColor => !IsAdsConnected ? "#F59E0B" : HasFault ? "#EF4444" : !IsEnabled ? "#94A3B8" : IsMoving ? "#38BDF8" : "#22C55E";

        public DelegateCommand ConnectCommand { get; }
        public DelegateCommand EnableCommand { get; }
        public DelegateCommand ResetCommand { get; }
        public DelegateCommand StopCommand { get; }
        public DelegateCommand MoveRelativeCommand { get; }
        public DelegateCommand MoveAbsoluteCommand { get; }
        public DelegateCommand JogPositivePressedCommand { get; }
        public DelegateCommand JogPositiveReleasedCommand { get; }
        public DelegateCommand JogNegativePressedCommand { get; }
        public DelegateCommand JogNegativeReleasedCommand { get; }
        public DelegateCommand ClearLogCommand { get; }

        private void ConnectToPlc()
        {
            if (ExecuteAxisAction("已连接 TwinCAT ADS（PLC Runtime 1 / 端口 851）。", _axisService.Connect))
            {
                _lastConnectionError = null;
                RefreshStatus();
            }
        }

        private void EnableAxis() => ExecuteAxisAction("轴使能命令已置位。", _axisService.Enable);
        private void ResetAxis() => ExecuteAxisAction("复位命令已发送，PLC 将自动复位命令位。", _axisService.Reset);
        private void StopAxis() => ExecuteAxisAction("已撤销点动并发送停止命令。", _axisService.Stop);

        private void MoveRelative()
        {
            if (!ValidateTarget(RelativeDistance, "相对距离")) return;
            ExecuteAxisAction($"相对运动：距离 {RelativeDistance:F3} mm。",
                () => _axisService.MoveRelative(RelativeDistance));
        }

        private void MoveAbsolute()
        {
            if (!ValidateTarget(AbsolutePosition, "绝对位置")) return;
            ExecuteAxisAction($"绝对运动：目标 {AbsolutePosition:F3} mm。",
                () => _axisService.MoveAbsolute(AbsolutePosition));
        }

        private void BeginJogPositive()
        {
            if (_jogPositiveActive) return;
            if (ExecuteAxisAction("点动正向按下：bJogPos = TRUE。", () => _axisService.SetJogPositive(true)))
            {
                _jogPositiveActive = true;
                _jogNegativeActive = false;
            }
        }

        private void EndJogPositive()
        {
            if (!_jogPositiveActive) return;
            _jogPositiveActive = false;
            ExecuteAxisAction("点动正向松开：bJogPos = FALSE。", () => _axisService.SetJogPositive(false));
        }

        private void BeginJogNegative()
        {
            if (_jogNegativeActive) return;
            if (ExecuteAxisAction("点动反向按下：bJogNeg = TRUE。", () => _axisService.SetJogNegative(true)))
            {
                _jogNegativeActive = true;
                _jogPositiveActive = false;
            }
        }

        private void EndJogNegative()
        {
            if (!_jogNegativeActive) return;
            _jogNegativeActive = false;
            ExecuteAxisAction("点动反向松开：bJogNeg = FALSE。", () => _axisService.SetJogNegative(false));
        }

        private bool ExecuteAxisAction(string successMessage, Action action)
        {
            try
            {
                action();
                IsAdsConnected = _axisService.IsConnected;
                AddLog("操作", successMessage);
                return true;
            }
            catch (Exception exception)
            {
                IsAdsConnected = false;
                AddLog("错误", $"ADS 操作失败：{exception.Message}");
                return false;
            }
        }

        private bool ValidateTarget(double value, string fieldName)
        {
            if (!double.IsNaN(value) && !double.IsInfinity(value) && value >= -6000 && value <= 6000)
            {
                return true;
            }

            AddLog("错误", $"{fieldName}必须在 PLC 限制范围 -6000.000 至 6000.000 mm 内。");
            return false;
        }

        private void RefreshStatus()
        {
            if (!_axisService.IsConnected)
            {
                if (IsAdsConnected)
                {
                    IsAdsConnected = false;
                    AddLog("错误", "TwinCAT ADS 连接已断开，请重新点击“连接至PLC”。");
                }
                return;
            }

            try
            {
                var status = _axisService.GetStatus();
                ActualPosition = status.Position;
                ActualVelocity = status.Velocity;
                ActualRpm = status.Rpm;
                IsEnabled = status.IsEnabled;
                HasFault = status.HasFault;
                IsMoving = status.IsMoving;
                if (_motionState != status.MotionState)
                {
                    _motionState = status.MotionState;
                    RaiseAxisStateChanged();
                }
                IsAdsConnected = true;

                if (status.HasFault && status.ErrorId != 0 && status.ErrorId != _lastErrorId)
                {
                    AddLog("错误", $"PLC 轴错误，ErrorID = 0x{status.ErrorId:X8}。");
                }
                _lastErrorId = status.ErrorId;

                if (status.CommandRejected && (!_wasCommandRejected || status.RejectReason != _lastRejectReason))
                {
                    AddLog("警告", GetRejectMessage(status.RejectReason));
                }
                _wasCommandRejected = status.CommandRejected;
                _lastRejectReason = status.RejectReason;

                if (_lastConnectionError is not null)
                {
                    AddLog("信息", "TwinCAT ADS 已连接。");
                    _lastConnectionError = null;
                }
            }
            catch (Exception exception)
            {
                IsAdsConnected = false;
                var error = exception.GetBaseException().Message;
                if (!string.Equals(_lastConnectionError, error, StringComparison.Ordinal))
                {
                    AddLog("错误", $"TwinCAT ADS 连接/读取失败：{error}");
                    _lastConnectionError = error;
                }
            }
        }

        private void RaiseAxisStateChanged()
        {
            RaisePropertyChanged(nameof(AxisStateText));
            RaisePropertyChanged(nameof(AxisStateColor));
        }

        private static string GetRejectMessage(uint reason)
        {
            return reason switch
            {
                1 => "PLC 拒绝相对运动命令。",
                2 => "PLC 拒绝绝对运动命令。",
                3 => "PLC 检测到正反点动命令冲突，已撤销点动。",
                4 => "PLC 拒绝点动：使能、忙碌或错误条件不满足。",
                _ => $"PLC 拒绝命令，原因码：{reason}。"
            };
        }

        private void AddLog(string level, string message)
        {
            Logs.Insert(0, new LogEntry(level, message));
            while (Logs.Count > 500) Logs.RemoveAt(Logs.Count - 1);
        }
    }
}
