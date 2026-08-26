namespace ZNQInterface.Models.Axes
{
    /// <summary>
    /// 上位机内部使用的轴唯一标识。
    /// </summary>
    public enum AxisId
    {
        DamperX = 1,
        DamperY = 2,
        DamperZ = 3,
        DamperRotation = 4,
        DamperGripper = 5,

        TrayX = 6,
        TrayZ = 7,

        Turntable = 8,
        Clamping = 9,

        AdjustmentX = 10,
        AdjustmentY = 11,
        AdjustmentZ = 12,

        ScrewdriverY = 13,
        ScrewdriverZ = 14
    }
}