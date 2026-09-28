using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZNQInterface.Models.Axes
{
    /// <summary>
    /// 轴当前运动方向。
    ///
    /// 枚举名称和数值必须与PLC中的
    /// E_AxisMotionDirection完全一致。
    ///
    /// PLC只传递坐标正负方向，
    /// WPF再根据轴定义转换为前后、左右、上下等文字。
    /// </summary>
    public enum AxisMotionDirection
    {
        /// <summary>
        /// 当前没有有效运动方向。
        /// </summary>
        None = 0,

        /// <summary>
        /// 轴坐标正方向。
        /// </summary>
        Positive = 10,

        /// <summary>
        /// 轴坐标负方向。
        /// </summary>
        Negative = 20
    }
}