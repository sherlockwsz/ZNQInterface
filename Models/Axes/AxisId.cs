namespace ZNQInterface.Models.Axes
{
    /// <summary>
    /// 上位机内部使用的轴唯一标识。
    /// </summary>
    public enum AxisId
    {
        TrayX = 1,
        TrayZ = 2,

        DamperX = 3,
        DamperY = 4,
        DamperZ = 5,
        DamperRotation = 6,
        DamperGripper = 7,

        Turntable = 8,
        Clamping = 9,

        AdjustmentX = 10,
        AdjustmentY = 11,
        AdjustmentZ = 12,

        ScrewdriverY = 13,
        ScrewdriverZ = 14
    }
}
