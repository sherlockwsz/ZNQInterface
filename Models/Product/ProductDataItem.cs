using System;

namespace ZNQInterface.Models.Product
{
    /// <summary>
    /// 产品检测数据表中的单条记录
    /// </summary>
    public class ProductDataItem
    {
        /// <summary>
        /// 检测完成时间
        /// </summary>
        public DateTime DetectionTime { get; set; }

        /// <summary>
        /// 产品编号
        /// </summary>
        public string ProductNumber { get; set; } = string.Empty;

        /// <summary>
        /// 同轴度检测值，单位mm
        /// </summary>
        public double Coaxiality { get; set; }

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