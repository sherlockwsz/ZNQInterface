namespace ZNQInterface.Models.Axes
{
    /// <summary>
    /// 设备中的轴功能组。
    /// 不直接使用中文字符串判断轴所属分组。
    /// </summary>
    public enum AxisGroupId
    {
        DamperLoading = 1,
        TrayLoading = 2,
        Turntable = 3,
        Adjustment = 4,
        Screwdriver = 5
    }
}