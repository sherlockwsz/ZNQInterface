# ZNQInterface — 14轴ADS接入与映射说明

## 1. 唯一轴映射

WPF仅在 `Models/Axes/AxisCatalog.cs` 维护轴号、名称、方向、单位和ADS路径。页面、命令服务和监控服务不得另建轴号表。

| PLC下标 | AxisId | 机构 | 当前状态 |
|---:|---|---|---|
| 1 | TrayX | 料盘X轴（前后） | 已接入 |
| 2 | TrayZ | 料盘Z轴（上下） | 已接入 |
| 3 | DamperX | 阻尼器X轴（前后） | 已接入 |
| 4 | DamperY | 阻尼器Y轴（左右） | 已接入 |
| 5 | DamperZ | 阻尼器Z轴（上下） | 已接入 |
| 6 | DamperRotation | 阻尼器旋转轴 | 已接入 |
| 7 | DamperGripper | 阻尼器夹紧轴 | **未接入，保留编号** |
| 8 | Turntable | 转台轴 | 已接入 |
| 9 | Clamping | 夹紧轴 | 已接入 |
| 10 | AdjustmentX | 同轴度调整X轴（前后） | 已接入 |
| 11 | AdjustmentY | 同轴度调整Y轴（左右） | 已接入 |
| 12 | AdjustmentZ | 同轴度调整Z轴（上下） | 已接入 |
| 13 | ScrewdriverY | 螺丝刀Y轴（左右） | 已接入 |
| 14 | ScrewdriverZ | 螺丝刀Z轴（上下） | 已接入 |

空间方向约定：X正向为前移、负向为后移；Y正向为左移、负向为右移；Z正向为上移、负向为下移。旋转轴和夹紧轴继续使用原有顺/逆时针、夹紧/松开文字。

## 2. PLC符号规则

每根轴的运行接口：

```text
GVL_AxisRuntime.Axes[i]
```

每根轴的安全参数：

```text
GVL_AxisConfig.AxisLimits[i]
```

`i` 必须等于 `AxisId` 的数值。程序启动时 `AxisStatusViewModel` 会检查14轴数量、重复编号、编号范围和 `AxisId`/PLC下标一致性，发现错误直接阻止启动。

## 3. 配置一致性保护

- WPF设计配置由 `AxisDefinition.IsExpectedConfigured` 表示。
- PLC实际配置由 `AxisLimits[i].bConfigured` 表示。
- 7号 `DamperGripper` 在界面保留并显示“未接入”，所有控制禁用。
- WPF与PLC的配置状态不一致时显示“配置不一致”，并禁止轴命令，防止错误下标导致误动作。

## 4. 监控策略

`AxisMonitoringService` 使用ADS Sum批量读取，不再对每根轴、每个变量分别发送ADS请求。

- 快速批次：按 `AdsConnectionOptions.PollInterval`（默认50 ms）读取位置、速度、运动状态、命令结果和报警。
- 慢速批次：每250 ms读取设置速度、加速度、转速、软限位状态和 `ST_AxisLimit`。
- 每批少于Beckhoff建议的500个子命令上限。
- ADS断线后全部已接入轴立即显示通信异常；连接服务保持原有自动重连和句柄缓存。

## 5. 参数安全范围

手动调试速度优先使用PLC：

```text
AxisLimits[i].fMinVelocity
AxisLimits[i].fMaxVelocity
```

当PLC最小/最大速度尚未形成有效范围（当前工程默认均为0）时，WPF继续采用原有 `(0, 100]` 兼容范围，现有操作方式不变。

当PLC启用软限位且正、负限位有效时，绝对目标以及“当前位置 + 相对距离”还会在WPF侧按以下范围预检：

```text
[fSoftwareLimitNeg + fSoftLimitMargin,
 fSoftwareLimitPos - fSoftLimitMargin]
```

PLC仍是最终安全裁决方，WPF校验不能替代PLC限位、驱动器限位或硬件安全回路。

## 6. 保留的现有行为

- 使能为保持量。
- 复位、停止、绝对定位和相对定位继续使用原有命令脉冲。
- 点动保持“按下置位、松开复位”。
- 主动退出/断开时继续释放点动、停止并撤销使能。
- 回零、力矩写入和零位写入按钮继续保持禁用，因为当前PLC `ST_AxisCmd` 未提供对应命令字段。
