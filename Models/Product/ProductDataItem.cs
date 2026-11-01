using System;

namespace ZNQInterface.Models.Product
{
    /// <summary>
    /// 产品检测数据表中的单条记录
    /// </summary>
    public class ProductDataItem
    {
        /// <summary>
        /// 扫码事件的本地时间（兼容原日期绑定）
        /// </summary>
        public DateTime DetectionTime { get; set; }

        /// <summary>
        /// 产品编号
        /// </summary>
        public string ProductNumber { get; set; } = string.Empty;

        /// <summary>
        /// 同轴度检测值，单位mm
        /// </summary>
        public double? Coaxiality { get; set; }

        public string NgReason { get; set; } = string.Empty;
        public double? InitialX { get; set; }
        public double? InitialY { get; set; }
        public double? InitialCoaxiality { get; set; }
        public double? FinalX { get; set; }
        public double? FinalY { get; set; }
        public uint TotalAdjustCount { get; set; }
        public double AdjustmentSeconds { get; set; }
        public DateTime CompletionTime { get; set; }
        public ulong SessionId { get; set; }
        public uint RequestId { get; set; }
        public uint CompleteSeq { get; set; }
        public string Key => $"{SessionId}:{RequestId}";

        /// <summary>
        /// 是否合格
        /// </summary>
        public bool IsQualified { get; set; }

        /// <summary>
        /// 界面显示文字
        /// </summary>
        public string QualificationText =>
            IsQualified ? "合格" : "不合格";
    }
}
