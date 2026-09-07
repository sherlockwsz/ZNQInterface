namespace ZNQInterface.Services.Processes
{
    /// <summary>
    /// 阻尼器料位和料盘状态对应的PLC ADS符号。
    ///
    /// 如果以后PLC功能块实例名称发生变化，只需集中修改本文件。
    /// </summary>
    public static class ProcessStatusSymbols
    {
        /// <summary>
        /// 获取当前工作料盘指定料位的状态符号。
        /// PLC数组范围：行1～4，列1～6。
        /// </summary>
        public static string DamperSlot(int row, int column)
        {
            return $"MAIN.fbDamperProcess.aSlots[{row},{column}].eState";
        }

        /// <summary>
        /// 功能3缓冲位置状态。
        /// 只映射至WPF四个缓冲位中的第1个。
        /// </summary>
        public const string DamperBufferPosition =
            "MAIN.fbDamperProcess.eBufferPositionState";

        /// <summary>
        /// 获取五层料盘中指定层的状态符号。
        /// PLC数组范围：1～5。
        /// </summary>
        public static string TrayLevel(int level)
        {
            return $"MAIN.fbTrayProcess.aTray[{level}].eState";
        }
    }
}