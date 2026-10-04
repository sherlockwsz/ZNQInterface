namespace ZNQInterface.Models.Device
{
    // 与 PLC 当前枚举数值和默认 INT 存储宽度一致。
    public enum MachineFaultSource : short
    {
        None = 0,
        HmiWatchdog = 10,
        AutoStart = 20,
        MachinePreparation = 25,
        AxisHoming = 27,
        Axis = 30,
        DamperGripper = 40,
        AdjustGripper = 45,
        TurntableProcess = 50,
        ScrewdriverProcess = 55,
        TrayProcess = 56,
        DamperProcess = 57,
        AutoStop = 60,
    }
}
