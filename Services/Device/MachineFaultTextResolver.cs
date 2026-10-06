using System.Linq;
using ZNQInterface.Models.Axes;
using ZNQInterface.Models.Device;

namespace ZNQInterface.Services.Device
{
    /// <summary>纯文本映射；不访问ADS，不判断故障是否存在或是否可复位。</summary>
    public static class MachineFaultTextResolver
    {
        public static string Resolve(MachineFaultSource source, ushort axisIndex, uint errorCode)
        {
            string sourceText = source switch
            {
                MachineFaultSource.HmiWatchdog => "HMI看门狗",
                MachineFaultSource.AutoStart => "自动启动",
                MachineFaultSource.MachinePreparation => "整机准备",
                MachineFaultSource.AxisHoming => "单轴回零",
                MachineFaultSource.Axis => "轴",
                MachineFaultSource.DamperGripper => "COM3阻尼器夹爪",
                MachineFaultSource.AdjustGripper => "COM4调整夹爪",
                MachineFaultSource.TurntableProcess => "转台/夹紧流程",
                MachineFaultSource.ScrewdriverProcess => "螺丝刀流程",
                MachineFaultSource.TrayProcess => "料盘流程",
                MachineFaultSource.DamperProcess => "阻尼器流程",
                MachineFaultSource.AutoStop => "全轴停止",
                _ => $"未知故障源({(short)source})"
            };
            // 错误码域由Source选择；同一个数值不可跨域解释。
            string detail = (source, errorCode) switch
            {
                (MachineFaultSource.HmiWatchdog, 0xA001) => "HMI心跳超时",
                (MachineFaultSource.AutoStart, 1) => "HMI离线",
                (MachineFaultSource.AutoStart, 2) => "控制许可无效",
                (MachineFaultSource.AutoStart, 3) => "夹爪未就绪",
                (MachineFaultSource.AutoStart, 4) => "轴未配置",
                (MachineFaultSource.AutoStart, 5) => "启动条件丢失",
                (MachineFaultSource.AutoStart, 6) => "启动阶段轴报警",
                (MachineFaultSource.AutoStart, 7) => "轴就绪超时",
                (MachineFaultSource.AutoStart, 8) => "启动步骤无效",
                (MachineFaultSource.AutoStart, 0xA107) => "自动启动超时",
                (MachineFaultSource.MachinePreparation, 0xC101) => "准备轴未配置",
                (MachineFaultSource.MachinePreparation, 0xC102) => "准备轴就绪超时",
                (MachineFaultSource.MachinePreparation, 0xC103) => "准备定位超时",
                (MachineFaultSource.MachinePreparation, 0xC104) => "COM3夹爪张开失败",
                (MachineFaultSource.MachinePreparation, 0xC105) => "COM4夹爪夹紧失败",
                (MachineFaultSource.MachinePreparation, 0xC1FF) => "准备步骤无效",
                (MachineFaultSource.MachinePreparation, >= 0xC201 and <= 0xC20E) => "准备轴报警",
                (MachineFaultSource.MachinePreparation, >= 0xC301 and <= 0xC30E) => "准备轴命令失败",
                (MachineFaultSource.AxisHoming, 0xC100) => "回零故障（PLC未提供详细码）",
                (MachineFaultSource.AxisHoming, 0xC101) => "回零轴号超出范围",
                (MachineFaultSource.AxisHoming, 0xC102) => "回零未启用",
                (MachineFaultSource.AxisHoming, 0xC103) => "回零轴映射无效",
                (MachineFaultSource.AxisHoming, 0xC104) => "回零控制许可无效",
                (MachineFaultSource.AxisHoming, 0xC105) => "外部回零传感器未实现",
                (MachineFaultSource.AxisHoming, 0xC106) => "回零参考未标定",
                (MachineFaultSource.AxisHoming, 0xC107) => "回零超时",
                (MachineFaultSource.AxisHoming, 0xC108) => "回零使能阶段轴报警",
                (MachineFaultSource.AxisHoming, 0xC109) => "回零过程中轴报警",
                (MachineFaultSource.AxisHoming, 0xC10A) => "回零命令被拒绝",
                (MachineFaultSource.AxisHoming, 0xC10B) => "回零命令被中止",
                (MachineFaultSource.AxisHoming, 0xC10C) => "回零最终定位时轴报警",
                (MachineFaultSource.AxisHoming, 0xC10D) => "回零最终定位命令被拒绝",
                (MachineFaultSource.AxisHoming, 0xC10E) => "回零最终定位命令被中止",
                (MachineFaultSource.AxisHoming, 0xC10F) => "其他运动仍在执行",
                (MachineFaultSource.AxisHoming, 0xC110) => "回零选中轴存在报警",
                (MachineFaultSource.AxisHoming, 0xC1FF) => "回零步骤无效",
                (MachineFaultSource.Axis, 0xA201) => "轴故障（PLC未提供详细码）",
                (MachineFaultSource.TurntableProcess, 0x7101) => "转台位置状态无效",
                (MachineFaultSource.TurntableProcess, 0x7102) => "转台绝对定位超时",
                (MachineFaultSource.TurntableProcess, 0x71FF) => "转台步骤无效",
                (MachineFaultSource.TurntableProcess, 0x7201) => "夹紧轴转矩命令被拒绝",
                (MachineFaultSource.TurntableProcess, 0x7202) => "夹紧轴进入转矩模式超时",
                (MachineFaultSource.TurntableProcess, 0x7203) => "夹紧轴建立转矩时轴报警",
                (MachineFaultSource.TurntableProcess, 0x7204) => "夹紧轴建立转矩超时",
                (MachineFaultSource.TurntableProcess, 0x7205) => "夹紧步骤无效",
                (MachineFaultSource.TurntableProcess, 0x7206) => "夹紧轴位置命令未释放",
                (MachineFaultSource.TurntableProcess, 0x7207) => "夹紧轴恢复位置模式时轴报警",
                (MachineFaultSource.TurntableProcess, 0x7208) => "夹紧轴恢复位置模式超时",
                (MachineFaultSource.TurntableProcess, 0x7209) => "夹紧轴保持位置命令失败",
                (MachineFaultSource.TurntableProcess, 0x720A) => "夹紧轴定位超时",
                (MachineFaultSource.TurntableProcess, 0xA401) => "转台故障（PLC未提供详细码）",
                (MachineFaultSource.TrayProcess, 0x6211) => "料盘运动命令被拒绝",
                (MachineFaultSource.TrayProcess, 0x6212) => "料盘运动被中止",
                (MachineFaultSource.TrayProcess, 0x6213) => "料盘运动超时",
                (MachineFaultSource.TrayProcess, 0x6214) => "计划层取盘前检测缺盘",
                (MachineFaultSource.DamperProcess, 0x6111) => "螺钉相机离线",
                (MachineFaultSource.DamperProcess, 0x6112) => "螺钉视觉超时",
                (MachineFaultSource.DamperProcess, 0x6113) => "螺钉视觉结果无效",
                (MachineFaultSource.DamperProcess, 0x6114) => "螺钉视觉服务故障",
                (MachineFaultSource.DamperProcess, 0x6115) => "未检测到螺钉目标",
                (MachineFaultSource.DamperProcess, 0x6121) => "阻尼器运动命令被拒绝",
                (MachineFaultSource.DamperProcess, 0x6122) => "阻尼器运动被中止",
                (MachineFaultSource.DamperProcess, 0x6123) => "阻尼器运动超时",
                (MachineFaultSource.ScrewdriverProcess, 0xB600) => "螺丝刀故障（PLC未提供详细码）",
                (MachineFaultSource.ScrewdriverProcess, 0xB601) => "螺丝刀定位超时",
                (MachineFaultSource.ScrewdriverProcess, 0xB602) => "螺丝刀轴命令失败",
                (MachineFaultSource.ScrewdriverProcess, 0xB603) => "调整轴命令失败",
                (MachineFaultSource.ScrewdriverProcess, 0xB604) => "调整夹爪失败",
                (MachineFaultSource.ScrewdriverProcess, 0xB605) => "螺丝刀1动作失败",
                (MachineFaultSource.ScrewdriverProcess, 0xB606) => "螺丝刀2动作失败",
                (MachineFaultSource.ScrewdriverProcess, 0xB607) => "两个螺丝刀动作失败",
                (MachineFaultSource.ScrewdriverProcess, 0xB608) => "螺丝刀模式无效",
                (MachineFaultSource.AutoStop, 0xA501) => "整机停止超时",
                (MachineFaultSource.AutoStop, 0xA502) => "整机停止失败",
                (MachineFaultSource.AutoStop, 0xE501) => "轴在停止确认前掉使能",
                (MachineFaultSource.AutoStop, 0xE502) => "受控停止超时",
                (MachineFaultSource.AutoStop, 0xE503) => "释放Stop超时",
                (MachineFaultSource.AutoStop, 0xE504) => "撤销使能超时",
                (MachineFaultSource.AutoStop, 0xE505) => "外部设备停止失败",
                (MachineFaultSource.AutoStop, 0xE506) => "外部设备停止超时",
                _ => null
            };
            if (source == MachineFaultSource.DamperGripper || source == MachineFaultSource.AdjustGripper)
                detail = ResolveGripper(errorCode);
            string axisText = "";
            if (axisIndex != 0)
            {
                var axis = AxisCatalog.Definitions.FirstOrDefault(x => x.PlcAxisNumber == axisIndex);
                axisText = axis == null ? $"，轴{axisIndex}" : $"，轴{axisIndex}（{axis.DisplayName}）";
            }
            return $"0x{errorCode:X8}，{sourceText}：{detail ?? "未知故障"}{axisText}";
        }

        private static string ResolveGripper(uint code) => code switch
        {
            1 => "参数无效", 2 => "未就绪", 3 => "忙碌", 4 => "命令无效",
            5 => "夹紧电流无效", 6 => "设备正在停止",
            0x2001 => "动作超时", 0x2002 => "设备故障或复位超时",
            0x2003 => "通信不可用", 0x2004 => "被整机停止中断",
            0x3001 => "动作命令无效", 0x3002 => "动作接受超时",
            0x3003 => "动作结果超时", 0x3005 => "动作已取消", 0x3006 => "动作结果无效",
            0x4001 => "自动模式禁止手动命令", 0x4002 => "当前模式禁止自动命令",
            0x4003 => "故障模式禁止此命令", 0x4004 => "当前模式禁止控制",
            0xA301 => "夹爪动作故障（PLC未提供详细码）",
            0xA302 => "夹爪健康状态异常", 0xFFFF => "通信状态未知",
            _ => null
        };
    }
}
