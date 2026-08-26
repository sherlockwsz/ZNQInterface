namespace ZNQInterface.Models.Axes
{
    /// <summary>
    /// 一根轴使用的单位集合。
    /// 单位属于固定配置信息，程序运行期间通常不会改变。
    /// </summary>
    public class AxisUnitSet
    {
        public AxisUnitSet(
            string position,
            string velocity,
            string acceleration,
            string torque)
        {
            Position = position;
            Velocity = velocity;
            Acceleration = acceleration;
            Torque = torque;
        }

        /// <summary>
        /// 位置或角度单位。
        /// </summary>
        public string Position { get; }

        /// <summary>
        /// 速度单位。
        /// </summary>
        public string Velocity { get; }

        /// <summary>
        /// 加速度单位。
        /// </summary>
        public string Acceleration { get; }

        /// <summary>
        /// 力矩单位。
        /// </summary>
        public string Torque { get; }
    }
}