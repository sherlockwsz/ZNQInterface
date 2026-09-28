using System;

namespace ZNQInterface.Models.Axes
{
    /// <summary>
    /// 统一处理HMI机械方向与PLC坐标方向之间的转换。
    ///
    /// 注意：
    /// 1. 不转换绝对位置和实际位置；
    /// 2. 只转换点动方向、相对运动方向、运动状态和限位方向；
    /// 3. 所有方向转换必须通过本类完成，防止重复反向。
    /// </summary>
    public static class AxisDirectionMapper
    {
        /// <summary>
        /// 把HMI要求的机械方向转换为PLC正负方向。
        /// </summary>
        public static bool ToPlcPositiveDirection(
            AxisDefinition definition,
            bool hmiPositiveDirection)
        {
            EnsureDefinition(definition);

            return definition.DirectionPolarity ==
                   AxisDirectionPolarity.Reversed
                ? !hmiPositiveDirection
                : hmiPositiveDirection;
        }

        /// <summary>
        /// 把HMI机械方向的相对距离转换为PLC坐标距离。
        /// </summary>
        public static double ToPlcRelativeDistance(
            AxisDefinition definition,
            double hmiDistance)
        {
            EnsureDefinition(definition);

            return definition.DirectionPolarity ==
                   AxisDirectionPolarity.Reversed
                ? -hmiDistance
                : hmiDistance;
        }

        /// <summary>
        /// 把PLC运动方向转换为HMI机械方向。
        /// </summary>
        public static AxisMotionDirection FromPlcDirection(
            AxisDefinition definition,
            AxisMotionDirection plcDirection)
        {
            EnsureDefinition(definition);

            if (definition.DirectionPolarity !=
                AxisDirectionPolarity.Reversed)
            {
                return plcDirection;
            }

            return plcDirection switch
            {
                AxisMotionDirection.Positive =>
                    AxisMotionDirection.Negative,

                AxisMotionDirection.Negative =>
                    AxisMotionDirection.Positive,

                _ => AxisMotionDirection.None
            };
        }

        /// <summary>
        /// 把PLC的正负方向标志转换为HMI机械方向标志。
        /// true表示正方向，false表示负方向。
        /// </summary>
        public static bool FromPlcPositiveDirection(
            AxisDefinition definition,
            bool plcPositiveDirection)
        {
            EnsureDefinition(definition);

            return definition.DirectionPolarity ==
                   AxisDirectionPolarity.Reversed
                ? !plcPositiveDirection
                : plcPositiveDirection;
        }

        private static void EnsureDefinition(
            AxisDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(
                    nameof(definition));
            }
        }
    }
}