namespace ZNQInterface.Services.Vision
{
    /// <summary>
    /// 视觉监控使用的PLC叶子符号。WPF只读取，不参与工艺判定。
    /// </summary>
    internal static class VisionSymbols
    {
        private const string Screw = "GVL_ScrewVision.stInterface";
        private const string Coax = "GVL_CoaxVision.stInterface";

        public const string ScrewMeasureRequest = Screw + ".bMeasureRequest";
        public const string ScrewRequestId = Screw + ".udiRequestId";
        public const string ScrewResultValid = Screw + ".bResultValid";
        public const string ScrewResultId = Screw + ".udiResultId";
        public const string ScrewDetectedAngle = Screw + ".fDetectedAngle";
        public const string ScrewDetected = Screw + ".bDetected";
        public const string ScrewResultInvalid = Screw + ".bResultInvalid";
        public const string ScrewServiceFault = Screw + ".bServiceFault";
        public const string ScrewTimeout = Screw + ".bTimeout";

        public const string CoaxMeasureRequest = Coax + ".bMeasureRequest";
        public const string CoaxRequestId = Coax + ".udiRequestId";
        public const string CoaxResultValid = Coax + ".bResultValid";
        public const string CoaxResultId = Coax + ".udiResultId";
        public const string Coaxiality = Coax + ".fCoaxiality";
        public const string CoaxDeltaX = Coax + ".fDeltaX";
        public const string CoaxDeltaY = Coax + ".fDeltaY";
        public const string CoaxResultInvalid = Coax + ".bResultInvalid";
        public const string CoaxServiceFault = Coax + ".bServiceFault";
        public const string CoaxTimeout = Coax + ".bTimeout";

        // 最终合格判定由PLC FB_AdjustmentProcess产生，WPF禁止重算阈值。
        public const string AdjustmentDone =
            "MAIN.fbAdjustmentProcess.bAdjustmentDone";
        public const string AdjustmentQualified =
            "MAIN.fbAdjustmentProcess.bAdjustmentQualified";
        public const string AdjustmentVisionFault =
            "MAIN.fbAdjustmentProcess.bVisionFault";
    }
}
