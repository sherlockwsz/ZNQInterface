using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ZNQInterface.Models.Axes;

namespace ZNQInterface.ViewModels.Components.Axes
{
    /// <summary>
    /// 整台设备所有轴的统一状态管理器。
    ///
    /// 负责：
    /// 1. 创建整台设备的14根轴；
    /// 2. 创建5个轴功能组；
    /// 3. 提供按AxisId和PLC轴号查找轴的方法；
    /// 4. 为设备总览、手动调试和ADS通信提供同一批轴对象。
    /// </summary>
    public class AxisStatusViewModel : BindableBase
    {
        private readonly IReadOnlyDictionary
            <AxisId, AxisItemViewModel> _axesById;

        private readonly IReadOnlyDictionary
            <int, AxisItemViewModel> _axesByPlcNumber;

        public AxisStatusViewModel()
        {
            #region 第一步：创建14根轴

            // 阻尼器上下料X轴。
            DamperXAxis =
                CreateAxis(
                    AxisId.DamperX,
                    plcAxisNumber: 1,
                    AxisGroupId.DamperLoading,
                    groupName: "阻尼器上下料",
                    displayName: "X轴（前后）",
                    AxisType.Linear,
                    positiveDirectionText: "前移",
                    negativeDirectionText: "后移",
                    AxisUnits.Linear,
                    adsSymbolPrefix:
                        "GVL_AxisRuntime.Axis1",
                    adsLimitSymbolPrefix:
                        "GVL_AxisConfig.Axis1Limit");

            // 阻尼器上下料Y轴。
            DamperYAxis =
                CreateAxis(
                    AxisId.DamperY,
                    plcAxisNumber: 2,
                    AxisGroupId.DamperLoading,
                    groupName: "阻尼器上下料",
                    displayName: "Y轴（左右）",
                    AxisType.Linear,
                    positiveDirectionText: "左移",
                    negativeDirectionText: "右移",
                    AxisUnits.Linear);

            // 阻尼器上下料Z轴。
            DamperZAxis =
                CreateAxis(
                    AxisId.DamperZ,
                    plcAxisNumber: 3,
                    AxisGroupId.DamperLoading,
                    groupName: "阻尼器上下料",
                    displayName: "Z轴（上下）",
                    AxisType.Linear,
                    positiveDirectionText: "上移",
                    negativeDirectionText: "下移",
                    AxisUnits.Linear);

            // 阻尼器螺钉角度旋转轴。
            DamperRotationAxis =
                CreateAxis(
                    AxisId.DamperRotation,
                    plcAxisNumber: 4,
                    AxisGroupId.DamperLoading,
                    groupName: "阻尼器上下料",
                    displayName: "螺钉角度转轴",
                    AxisType.Rotary,
                    positiveDirectionText: "顺时针",
                    negativeDirectionText: "逆时针",
                    AxisUnits.Rotary);

            // 阻尼器夹爪轴。
            DamperGripperAxis =
                CreateAxis(
                    AxisId.DamperGripper,
                    plcAxisNumber: 5,
                    AxisGroupId.DamperLoading,
                    groupName: "阻尼器上下料",
                    displayName: "夹爪轴",
                    AxisType.Gripper,
                    positiveDirectionText: "夹紧",
                    negativeDirectionText: "松开",
                    AxisUnits.Linear);

            // 料盘上下料X轴。
            TrayXAxis =
                CreateAxis(
                    AxisId.TrayX,
                    plcAxisNumber: 6,
                    AxisGroupId.TrayLoading,
                    groupName: "料盘上下料",
                    displayName: "X轴（前后）",
                    AxisType.Linear,
                    positiveDirectionText: "前移",
                    negativeDirectionText: "后移",
                    AxisUnits.Linear);

            // 料盘上下料Z轴。
            TrayZAxis =
                CreateAxis(
                    AxisId.TrayZ,
                    plcAxisNumber: 7,
                    AxisGroupId.TrayLoading,
                    groupName: "料盘上下料",
                    displayName: "Z轴（上下）",
                    AxisType.Linear,
                    positiveDirectionText: "上移",
                    negativeDirectionText: "下移",
                    AxisUnits.Linear);

            // 转台旋转轴。
            TurntableAxis =
                CreateAxis(
                    AxisId.Turntable,
                    plcAxisNumber: 8,
                    AxisGroupId.Turntable,
                    groupName: "转台机构",
                    displayName: "转台轴",
                    AxisType.Rotary,
                    positiveDirectionText: "顺时针",
                    negativeDirectionText: "逆时针",
                    AxisUnits.Rotary);

            // 夹紧轴。
            ClampingAxis =
                CreateAxis(
                    AxisId.Clamping,
                    plcAxisNumber: 9,
                    AxisGroupId.Turntable,
                    groupName: "转台机构",
                    displayName: "夹紧轴",
                    AxisType.Gripper,
                    positiveDirectionText: "夹紧",
                    negativeDirectionText: "松开",
                    AxisUnits.Linear);

            // 同轴度调整X轴。
            AdjustmentXAxis =
                CreateAxis(
                    AxisId.AdjustmentX,
                    plcAxisNumber: 10,
                    AxisGroupId.Adjustment,
                    groupName: "同轴度调整",
                    displayName: "X轴（前后）",
                    AxisType.Linear,
                    positiveDirectionText: "前移",
                    negativeDirectionText: "后移",
                    AxisUnits.Linear);

            // 同轴度调整Y轴。
            AdjustmentYAxis =
                CreateAxis(
                    AxisId.AdjustmentY,
                    plcAxisNumber: 11,
                    AxisGroupId.Adjustment,
                    groupName: "同轴度调整",
                    displayName: "Y轴（左右）",
                    AxisType.Linear,
                    positiveDirectionText: "左移",
                    negativeDirectionText: "右移",
                    AxisUnits.Linear);

            // 同轴度调整Z轴。
            AdjustmentZAxis =
                CreateAxis(
                    AxisId.AdjustmentZ,
                    plcAxisNumber: 12,
                    AxisGroupId.Adjustment,
                    groupName: "同轴度调整",
                    displayName: "Z轴（上下）",
                    AxisType.Linear,
                    positiveDirectionText: "上移",
                    negativeDirectionText: "下移",
                    AxisUnits.Linear);

            // 螺丝刀Y轴。
            ScrewdriverYAxis =
                CreateAxis(
                    AxisId.ScrewdriverY,
                    plcAxisNumber: 13,
                    AxisGroupId.Screwdriver,
                    groupName: "螺丝刀机构",
                    displayName: "Y轴（左右）",
                    AxisType.Linear,
                    positiveDirectionText: "左移",
                    negativeDirectionText: "右移",
                    AxisUnits.Linear);

            // 螺丝刀Z轴。
            ScrewdriverZAxis =
                CreateAxis(
                    AxisId.ScrewdriverZ,
                    plcAxisNumber: 14,
                    AxisGroupId.Screwdriver,
                    groupName: "螺丝刀机构",
                    displayName: "Z轴（上下）",
                    AxisType.Linear,
                    positiveDirectionText: "上移",
                    negativeDirectionText: "下移",
                    AxisUnits.Linear);

            #endregion

            #region 第二步：创建5个轴功能组

            /*
            轴组不会重新创建轴。

            这里放入的都是上面已经创建完成的
            AxisItemViewModel对象引用。
            */
            DamperLoadingGroup =
                new AxisGroupViewModel(
                    AxisGroupId.DamperLoading,
                    "阻尼器上下料",
                    new[]
                    {
                        DamperXAxis,
                        DamperYAxis,
                        DamperZAxis,
                        DamperRotationAxis,
                        DamperGripperAxis
                    });

            TrayLoadingGroup =
                new AxisGroupViewModel(
                    AxisGroupId.TrayLoading,
                    "料盘上下料",
                    new[]
                    {
                        TrayXAxis,
                        TrayZAxis
                    });

            TurntableGroup =
                new AxisGroupViewModel(
                    AxisGroupId.Turntable,
                    "转台机构",
                    new[]
                    {
                        TurntableAxis,
                        ClampingAxis
                    });

            AdjustmentGroup =
                new AxisGroupViewModel(
                    AxisGroupId.Adjustment,
                    "同轴度调整",
                    new[]
                    {
                        AdjustmentXAxis,
                        AdjustmentYAxis,
                        AdjustmentZAxis
                    });

            ScrewdriverGroup =
                new AxisGroupViewModel(
                    AxisGroupId.Screwdriver,
                    "螺丝刀机构",
                    new[]
                    {
                        ScrewdriverYAxis,
                        ScrewdriverZAxis
                    });

            #endregion

            #region 第三步：汇总轴组和所有轴

            Groups =
                new ReadOnlyCollection<AxisGroupViewModel>(
                    new List<AxisGroupViewModel>
                    {
                        DamperLoadingGroup,
                        TrayLoadingGroup,
                        TurntableGroup,
                        AdjustmentGroup,
                        ScrewdriverGroup
                    });

            /*
            从5个轴组中汇总14根轴。

            SelectMany只会汇总已有对象引用，
            不会重新创建AxisItemViewModel。
            */
            List<AxisItemViewModel> allAxes =
                Groups
                    .SelectMany(
                        group => group.Axes)
                    .ToList();

            // 检查轴数量是否正确。
            if (allAxes.Count != 14)
            {
                throw new InvalidOperationException(
                    $"轴数量错误：当前为{allAxes.Count}根，" +
                    "预期为14根。");
            }

            // 检查AxisId是否重复。
            bool hasDuplicateAxisId =
                allAxes
                    .GroupBy(
                        axis =>
                            axis.Definition.AxisId)
                    .Any(
                        group =>
                            group.Count() > 1);

            if (hasDuplicateAxisId)
            {
                throw new InvalidOperationException(
                    "存在重复的AxisId，请检查轴定义。");
            }

            // 检查PLC轴号是否重复。
            bool hasDuplicatePlcAxisNumber =
                allAxes
                    .GroupBy(
                        axis =>
                            axis.Definition.PlcAxisNumber)
                    .Any(
                        group =>
                            group.Count() > 1);

            if (hasDuplicatePlcAxisNumber)
            {
                throw new InvalidOperationException(
                    "存在重复的PLC轴号，请检查轴定义。");
            }

            AllAxes =
                new ReadOnlyCollection<AxisItemViewModel>(
                    allAxes);

            /*
            建立AxisId到轴对象的映射，
            供页面和ADS通信快速查找。
            */
            _axesById =
                allAxes.ToDictionary(
                    axis =>
                        axis.Definition.AxisId);

            /*
            建立PLC轴号到轴对象的映射。
            */
            _axesByPlcNumber =
                allAxes.ToDictionary(
                    axis =>
                        axis.Definition.PlcAxisNumber);

            #endregion
        }

        #region 14根轴

        public AxisItemViewModel DamperXAxis { get; }

        public AxisItemViewModel DamperYAxis { get; }

        public AxisItemViewModel DamperZAxis { get; }

        public AxisItemViewModel DamperRotationAxis { get; }

        public AxisItemViewModel DamperGripperAxis { get; }

        public AxisItemViewModel TrayXAxis { get; }

        public AxisItemViewModel TrayZAxis { get; }

        public AxisItemViewModel TurntableAxis { get; }

        public AxisItemViewModel ClampingAxis { get; }

        public AxisItemViewModel AdjustmentXAxis { get; }

        public AxisItemViewModel AdjustmentYAxis { get; }

        public AxisItemViewModel AdjustmentZAxis { get; }

        public AxisItemViewModel ScrewdriverYAxis { get; }

        public AxisItemViewModel ScrewdriverZAxis { get; }

        #endregion

        #region 5个轴组

        public AxisGroupViewModel DamperLoadingGroup
        {
            get;
        }

        public AxisGroupViewModel TrayLoadingGroup
        {
            get;
        }

        public AxisGroupViewModel TurntableGroup
        {
            get;
        }

        public AxisGroupViewModel AdjustmentGroup
        {
            get;
        }

        public AxisGroupViewModel ScrewdriverGroup
        {
            get;
        }

        /// <summary>
        /// 整台设备所有轴组。
        /// </summary>
        public IReadOnlyList<AxisGroupViewModel> Groups
        {
            get;
        }

        #endregion

        /// <summary>
        /// 整台设备所有轴。
        ///
        /// 轴数量和轴成员固定，因此使用只读集合。
        /// WPF的ItemsSource可以直接绑定IReadOnlyList。
        /// </summary>
        public IReadOnlyList<AxisItemViewModel> AllAxes
        {
            get;
        }

        /// <summary>
        /// 根据上位机AxisId查找轴。
        /// 找不到时返回null。
        /// </summary>
        public AxisItemViewModel FindAxis(
            AxisId axisId)
        {
            _axesById.TryGetValue(
                axisId,
                out AxisItemViewModel axis);

            return axis;
        }

        /// <summary>
        /// 根据PLC轴号查找轴。
        /// 找不到时返回null。
        /// </summary>
        public AxisItemViewModel FindAxisByPlcNumber(
            int plcAxisNumber)
        {
            _axesByPlcNumber.TryGetValue(
                plcAxisNumber,
                out AxisItemViewModel axis);

            return axis;
        }

        /// <summary>
        /// 创建一根轴。
        /// 所有轴都通过该方法创建，避免重复初始化代码。
        /// </summary>
        private static AxisItemViewModel CreateAxis(
            AxisId axisId,
            int plcAxisNumber,
            AxisGroupId groupId,
            string groupName,
            string displayName,
            AxisType axisType,
            string positiveDirectionText,
            string negativeDirectionText,
            AxisUnitSet units)
        {
            return CreateAxis(
                axisId,
                plcAxisNumber,
                groupId,
                groupName,
                displayName,
                axisType,
                positiveDirectionText,
                negativeDirectionText,
                units,
                adsSymbolPrefix: null,
                adsLimitSymbolPrefix: null);
        }

        /// <summary>
        /// 创建一根轴，并可选配置其 PLC ADS 符号映射。
        /// </summary>
        private static AxisItemViewModel CreateAxis(
            AxisId axisId,
            int plcAxisNumber,
            AxisGroupId groupId,
            string groupName,
            string displayName,
            AxisType axisType,
            string positiveDirectionText,
            string negativeDirectionText,
            AxisUnitSet units,
            string adsSymbolPrefix,
            string adsLimitSymbolPrefix)
        {
            AxisDefinition definition =
                new AxisDefinition
                {
                    AxisId = axisId,

                    PlcAxisNumber =
                        plcAxisNumber,

                    GroupId =
                        groupId,

                    DisplayName =
                        displayName,

                    AxisType =
                        axisType,

                    PositiveDirectionText =
                        positiveDirectionText,

                    NegativeDirectionText =
                        negativeDirectionText,

                    Units =
                        units,

                    AdsSymbolPrefix =
                        adsSymbolPrefix,

                    AdsLimitSymbolPrefix =
                        adsLimitSymbolPrefix
                };

            return new AxisItemViewModel(
                definition);
        }
    }
}
