using System;
using System.Collections.Generic;

namespace ZNQInterface.Models.Axes
{
    /// <summary>
    /// WPF侧14轴唯一映射表。
    ///
    /// 编号与PLC GVL_AxisIndex、GVL_AxisRuntime.Axes、
    /// GVL_AxisConfig.AxisLimits和GVL_AxisRef.AxisRefs完全一致。
    /// 页面、ViewModel和ADS服务不得再次维护另一份轴号表。
    /// </summary>
    public static class AxisCatalog
    {
        public const int AxisCount = 14;

        public static IReadOnlyList<AxisDefinition> Definitions
        {
            get;
        } = Array.AsReadOnly(
            new[]
            {
                // 料盘上下料机构。
                Create(
                    AxisId.TrayX,
                    AxisGroupId.TrayLoading,
                    "X轴（前后）",
                    AxisType.Linear,
                    "前移",
                    "后移",
                    AxisUnits.Linear),

                Create(
                    AxisId.TrayZ,
                    AxisGroupId.TrayLoading,
                    "Z轴（上下）",
                    AxisType.Linear,
                    "上移",
                    "下移",
                    AxisUnits.Linear),

                // 阻尼器上下料机构。
                Create(
                    AxisId.DamperX,
                    AxisGroupId.DamperLoading,
                    "X轴（前后）",
                    AxisType.Linear,
                    "前移",
                    "后移",
                    AxisUnits.Linear),

                Create(
                    AxisId.DamperY,
                    AxisGroupId.DamperLoading,
                    "Y轴（左右）",
                    AxisType.Linear,
                    "左移",
                    "右移",
                    AxisUnits.Linear),

                Create(
                    AxisId.DamperZ,
                    AxisGroupId.DamperLoading,
                    "Z轴（上下）",
                    AxisType.Linear,
                    "上移",
                    "下移",
                    AxisUnits.Linear),

                Create(
                    AxisId.DamperRotation,
                    AxisGroupId.DamperLoading,
                    "阻尼器旋转轴",
                    AxisType.Rotary,
                    "顺时针",
                    "逆时针",
                    AxisUnits.Rotary),

                Create(
                    AxisId.DamperGripper,
                    AxisGroupId.DamperLoading,
                    "阻尼器夹紧轴",
                    AxisType.Gripper,
                    "夹紧",
                    "松开",
                    AxisUnits.Linear,
                    isExpectedConfigured: false),

                // 转台及夹紧机构。
                Create(
                    AxisId.Turntable,
                    AxisGroupId.Turntable,
                    "转台轴",
                    AxisType.Rotary,
                    "顺时针",
                    "逆时针",
                    AxisUnits.Rotary),

                Create(
                    AxisId.Clamping,
                    AxisGroupId.Turntable,
                    "夹紧轴",
                    AxisType.Gripper,
                    "夹紧",
                    "松开",
                    AxisUnits.Linear),

                // 同轴度调整机构。
                Create(
                    AxisId.AdjustmentX,
                    AxisGroupId.Adjustment,
                    "X轴（前后）",
                    AxisType.Linear,
                    "前移",
                    "后移",
                    AxisUnits.Linear),

                Create(
                    AxisId.AdjustmentY,
                    AxisGroupId.Adjustment,
                    "Y轴（左右）",
                    AxisType.Linear,
                    "左移",
                    "右移",
                    AxisUnits.Linear),

                Create(
                    AxisId.AdjustmentZ,
                    AxisGroupId.Adjustment,
                    "Z轴（上下）",
                    AxisType.Linear,
                    "上移",
                    "下移",
                    AxisUnits.Linear),

                // 螺丝刀机构。
                Create(
                    AxisId.ScrewdriverY,
                    AxisGroupId.Screwdriver,
                    "Y轴（左右）",
                    AxisType.Linear,
                    "左移",
                    "右移",
                    AxisUnits.Linear),

                Create(
                    AxisId.ScrewdriverZ,
                    AxisGroupId.Screwdriver,
                    "Z轴（上下）",
                    AxisType.Linear,
                    "上移",
                    "下移",
                    AxisUnits.Linear)
            });

        private static AxisDefinition Create(
            AxisId axisId,
            AxisGroupId groupId,
            string displayName,
            AxisType axisType,
            string positiveDirectionText,
            string negativeDirectionText,
            AxisUnitSet units,
            bool isExpectedConfigured = true)
        {
            int plcAxisNumber = (int)axisId;

            return new AxisDefinition
            {
                AxisId = axisId,
                PlcAxisNumber = plcAxisNumber,
                IsExpectedConfigured = isExpectedConfigured,
                GroupId = groupId,
                DisplayName = displayName,
                AxisType = axisType,
                PositiveDirectionText = positiveDirectionText,
                NegativeDirectionText = negativeDirectionText,
                Units = units,
                AdsSymbolPrefix =
                    $"GVL_AxisRuntime.Axes[{plcAxisNumber}]",
                AdsLimitSymbolPrefix =
                    $"GVL_AxisConfig.AxisLimits[{plcAxisNumber}]"
            };
        }
    }
}
