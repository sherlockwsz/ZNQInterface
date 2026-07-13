using AxisControlHmi_test.Models;

namespace AxisControlHmi_test.Services
{
    public interface IAxisService
    {
        bool IsConnected { get; }
        void Connect();
        AxisStatus GetStatus();
        void Enable();
        void Reset();
        void Stop();
        void MoveRelative(double distance);
        void MoveAbsolute(double position);
        void SetJogPositive(bool isActive);
        void SetJogNegative(bool isActive);
    }
}
