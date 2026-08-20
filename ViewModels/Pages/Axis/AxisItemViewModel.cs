using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZNQInterface.ViewModels.Pages.Axis
{
    public class AxisItemViewModel : BindableBase
    {
        private double _setPosition;
        private double _setVelocity;
        private string _groupName;
        private string _displayName;
        public double ActualPosition { get; set; }
        public double ActualVelocity { get; set; }

        public string PositionUnit { get; set; }
        public string VelocityUnit { get; set; }

        public string MotionStatus { get; set; }

        public bool IsEnabled { get; set; }
        public bool IsHomed { get; set; }
        public bool IsCommunicationOk { get; set; }

        public bool PositiveLimit { get; set; }
        public bool NegativeLimit { get; set; }

        public bool HasFault { get; set; }

        public double SetPosition
        {
            get => _setPosition;
            set => SetProperty(ref _setPosition, value);
        }
        public double SetVelocity
        {
            get => _setVelocity;
            set => SetProperty(ref _setVelocity, value);
        }
        public string GroupName
        {
            get => _groupName;
            set => SetProperty(ref _groupName, value);
        }
        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }
    }
}
