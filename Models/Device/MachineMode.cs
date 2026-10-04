namespace ZNQInterface.Models.Device
{
    // 与 PLC 当前枚举数值和默认 INT 存储宽度一致。
    public enum MachineMode : short
    {
        Disabled = 0,
        Manual = 10,
        AutoStarting = 20,
        Preparing = 25,
        Automatic = 30,
        Homing = 40,
        Stopping = 50,
        Fault = 60,
    }
}
