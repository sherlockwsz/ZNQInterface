using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ZNQInterface.Models.Axes;

namespace ZNQInterface.ViewModels.Components.Axes
{
    /// <summary>
    /// 整台设备14根轴的唯一界面对象集合。
    /// 轴编号、名称、方向和ADS路径只从AxisCatalog生成，
    /// 页面与通信服务共享同一批AxisItemViewModel实例。
    /// </summary>
    public class AxisStatusViewModel : BindableBase
    {
        private readonly IReadOnlyDictionary<AxisId, AxisItemViewModel>
            _axesById;
        private readonly IReadOnlyDictionary<int, AxisItemViewModel>
            _axesByPlcNumber;

        public AxisStatusViewModel()
        {
            ValidateCatalog();

            Dictionary<AxisId, AxisItemViewModel> axes =
                AxisCatalog.Definitions.ToDictionary(
                    definition => definition.AxisId,
                    definition => new AxisItemViewModel(definition));

            TrayXAxis = axes[AxisId.TrayX];
            TrayZAxis = axes[AxisId.TrayZ];
            DamperXAxis = axes[AxisId.DamperX];
            DamperYAxis = axes[AxisId.DamperY];
            DamperZAxis = axes[AxisId.DamperZ];
            DamperRotationAxis = axes[AxisId.DamperRotation];
            DamperGripperAxis = axes[AxisId.DamperGripper];
            TurntableAxis = axes[AxisId.Turntable];
            ClampingAxis = axes[AxisId.Clamping];
            AdjustmentXAxis = axes[AxisId.AdjustmentX];
            AdjustmentYAxis = axes[AxisId.AdjustmentY];
            AdjustmentZAxis = axes[AxisId.AdjustmentZ];
            ScrewdriverYAxis = axes[AxisId.ScrewdriverY];
            ScrewdriverZAxis = axes[AxisId.ScrewdriverZ];

            // 保留现有页面的功能组及显示顺序。
            DamperLoadingGroup = new AxisGroupViewModel(
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

            TrayLoadingGroup = new AxisGroupViewModel(
                AxisGroupId.TrayLoading,
                "料盘上下料",
                new[] { TrayXAxis, TrayZAxis });

            TurntableGroup = new AxisGroupViewModel(
                AxisGroupId.Turntable,
                "转台机构",
                new[] { TurntableAxis, ClampingAxis });

            AdjustmentGroup = new AxisGroupViewModel(
                AxisGroupId.Adjustment,
                "同轴度调整",
                new[]
                {
                    AdjustmentXAxis,
                    AdjustmentYAxis,
                    AdjustmentZAxis
                });

            ScrewdriverGroup = new AxisGroupViewModel(
                AxisGroupId.Screwdriver,
                "螺丝刀机构",
                new[] { ScrewdriverYAxis, ScrewdriverZAxis });

            Groups = new ReadOnlyCollection<AxisGroupViewModel>(
                new List<AxisGroupViewModel>
                {
                    DamperLoadingGroup,
                    TrayLoadingGroup,
                    TurntableGroup,
                    AdjustmentGroup,
                    ScrewdriverGroup
                });

            // 通信批量读取始终按PLC数组下标1..14排列。
            List<AxisItemViewModel> allAxes = axes.Values
                .OrderBy(axis => axis.Definition.PlcAxisNumber)
                .ToList();

            AllAxes = new ReadOnlyCollection<AxisItemViewModel>(allAxes);
            _axesById = axes;
            _axesByPlcNumber = allAxes.ToDictionary(
                axis => axis.Definition.PlcAxisNumber);
        }

        public AxisItemViewModel TrayXAxis { get; }
        public AxisItemViewModel TrayZAxis { get; }
        public AxisItemViewModel DamperXAxis { get; }
        public AxisItemViewModel DamperYAxis { get; }
        public AxisItemViewModel DamperZAxis { get; }
        public AxisItemViewModel DamperRotationAxis { get; }
        public AxisItemViewModel DamperGripperAxis { get; }
        public AxisItemViewModel TurntableAxis { get; }
        public AxisItemViewModel ClampingAxis { get; }
        public AxisItemViewModel AdjustmentXAxis { get; }
        public AxisItemViewModel AdjustmentYAxis { get; }
        public AxisItemViewModel AdjustmentZAxis { get; }
        public AxisItemViewModel ScrewdriverYAxis { get; }
        public AxisItemViewModel ScrewdriverZAxis { get; }

        public AxisGroupViewModel DamperLoadingGroup { get; }
        public AxisGroupViewModel TrayLoadingGroup { get; }
        public AxisGroupViewModel TurntableGroup { get; }
        public AxisGroupViewModel AdjustmentGroup { get; }
        public AxisGroupViewModel ScrewdriverGroup { get; }
        public IReadOnlyList<AxisGroupViewModel> Groups { get; }
        public IReadOnlyList<AxisItemViewModel> AllAxes { get; }

        public AxisItemViewModel FindAxis(AxisId axisId)
        {
            _axesById.TryGetValue(axisId, out AxisItemViewModel axis);
            return axis;
        }

        public AxisItemViewModel FindAxisByPlcNumber(int plcAxisNumber)
        {
            _axesByPlcNumber.TryGetValue(
                plcAxisNumber,
                out AxisItemViewModel axis);
            return axis;
        }

        private static void ValidateCatalog()
        {
            IReadOnlyList<AxisDefinition> definitions =
                AxisCatalog.Definitions;

            if (definitions.Count != AxisCatalog.AxisCount)
            {
                throw new InvalidOperationException(
                    $"轴映射数量错误：当前{definitions.Count}根，" +
                    $"预期{AxisCatalog.AxisCount}根。");
            }

            if (definitions.GroupBy(item => item.AxisId)
                .Any(group => group.Count() > 1))
            {
                throw new InvalidOperationException(
                    "轴映射存在重复AxisId。");
            }

            if (definitions.GroupBy(item => item.PlcAxisNumber)
                .Any(group => group.Count() > 1))
            {
                throw new InvalidOperationException(
                    "轴映射存在重复PLC数组下标。");
            }

            foreach (AxisDefinition definition in definitions)
            {
                if (definition.PlcAxisNumber < 1 ||
                    definition.PlcAxisNumber > AxisCatalog.AxisCount ||
                    definition.PlcAxisNumber != (int)definition.AxisId)
                {
                    throw new InvalidOperationException(
                        $"轴{definition.AxisId}的PLC下标" +
                        $"{definition.PlcAxisNumber}无效。");
                }
            }
        }
    }
}
