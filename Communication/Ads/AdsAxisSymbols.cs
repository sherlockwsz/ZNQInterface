namespace ZNQInterface.Communication.Ads
{
    /// <summary>
    /// 集中生成 ZNQ_MoveCtrl 轴结构中的叶子变量名。
    /// PLC 结构名变化时只需在这里或 AxisDefinition 中调整，
    /// 不让字符串散落在 ViewModel 和命令代码里。
    /// </summary>
    internal static class AdsAxisSymbols
    {
        public static string Command(string prefix, string member) =>
            $"{prefix}.Cmd.{member}";

        public static string Setting(string prefix, string group, string member) =>
            $"{prefix}.Set.{group}.{member}";

        public static string Data(string prefix, string member) =>
            $"{prefix}.Data.{member}";

        public static string State(string prefix, string member) =>
            $"{prefix}.State.{member}";

        public static string Alarm(string prefix, string member) =>
            $"{prefix}.Alarm.{member}";

        public static string Limit(string prefix, string member) =>
            $"{prefix}.{member}";
    }
}
