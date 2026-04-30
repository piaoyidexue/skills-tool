# QA 测试模块说明文档

## 一、模块概述

QA 模块是技能系统（Skills-Tool）的集成测试与调试工具集，覆盖战斗框架的**功能验证**、**性能监控**、**逻辑正确性**和**可视化调试**四大维度。全部工具设计为即插即用，无需修改业务代码即可接入。

### 核心能力

| 能力 | 说明 |
|------|------|
| 画廊模式 | 按顺序释放 Skill.csv 中每个技能，慢速回放，肉眼观察同步性 |
| 压力测试 | 批量生成靶子 + 高频施法，监控 FPS/GC/对象池 |
| 元素反应穷举 | 5×5 木桩矩阵 + 预设状态注入 + 自动化反应验算 |
| 性能监控 | 实时 FPS/内存/对象池折线图 + 泄露报警 + 自定义计数器 |
| 死锁检测 | 编辑器后台无头运行所有技能图，检测节点超时死循环 |
| EQS 可视化 | 扇形/准星/连线绘制 + 查询耗时统计 |
| AI 战术沙盒 | SpatialHashGrid 网格可视化 + 刷怪/刷墙笔刷 + EQS 集成 |
| 浮动跳字 | 伤害/暴击/治疗/Buff/元素反应多类型浮动文字 |

### 设计原则

- **零侵入**：QA 组件独立于业务逻辑，不修改 SkillCaster / GEHost / SkillRunner 等核心类
- **GAS 驱动**：QATargetDummy 通过 `GEHost` 受理伤害修正，与正式战斗流程一致
- **即时可用**：大多数工具只需挂载 MonoBehaviour 或打开 EditorWindow 即可运行
- **运行时 GUI**：核心工具提供 OnGUI 面板，无需额外 UI 包依赖

---

## 二、目录结构

```
Assets/Scripts/QA/
├── QAGalleryTestController.cs   # 画廊 + 压力测试控制器
├── QAReactionMatrix.cs          # 元素反应矩阵穷举测试
├── QATargetDummy.cs             # QA 增强靶子（GEHost 驱动）
├── QAPerformanceMonitor.cs      # 实时性能监控面板
├── QADeadlockDetector.cs        # 死循环防呆检测器（EditorWindow）
├── QAEQSDebugger.cs             # EQS 查询可视化器
├── QAAITacticsSandbox.cs        # AI 战术沙盘
└── QAFloatingText.cs            # 浮动跳字组件
```

---

## 三、架构设计

### 3.1 模块依赖关系

```
┌─────────────────────────────────────────────────────────────┐
│                     QA 测试模块                              │
│                                                             │
│  QAGalleryTestController ◄──── QAPerformanceMonitor         │
│       │                           ▲                         │
│       ▼                           │                         │
│  QATargetDummy ◄──── QAReactionMatrix                      │
│       ▲                           ▲                         │
│       │                           │                         │
│  QAFloatingText ─────────────────┘                         │
│                                                             │
│  QADeadlockDetector (EditorWindow, 独立)                    │
│  QAEQSDebugger (MonoBehaviour, 独立)                        │
│  QAAITacticsSandbox ◄──── QAEQSDebugger                    │
└─────────────────────────────────────────────────────────────┘
          │                │                │
          ▼                ▼                ▼
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│  SkillCaster  │  │    GEHost     │  │SpatialHashGrid│
│  SkillRunner  │  │ AttributeSet  │  │AISensorJobSys │
│  SkillTickMgr │  │EffectSystem   │  │  ConfigLoader │
└──────────────┘  └──────────────┘  └──────────────┘
```

### 3.2 数据流 — 画廊/压力测试

```
QAGalleryTestController
    │
    ├── Gallery Mode:
    │     ConfigLoader.GetAllSkillConfigs()
    │       → 每 2s 切换一个 SkillID
    │       → SkillCaster.TryCast(skillId, target)
    │       → QATargetDummy.TakeDamage()
    │       → QAFloatingText.ShowDamage()
    │
    ├── Stress Mode:
    │     每 0.05s 施法一次 → 100 靶子阵型
    │     → QAPerformanceMonitor 实时记录 FPS/GC/Pool
    │     → 每 200 帧动态添加靶子
    │
    └── 打断测试:
          CastSkill → 随机 30%~80% 时刻 → SkillCaster.Interrupt()
```

### 3.3 数据流 — 反应矩阵测试

```
QAReactionMatrix
    │
    ├── BuildMatrix() → 5×5 QATargetDummy 方阵
    │
    ├── ApplyRowPreset(row, StatusType)
    │     → GEHost.ApplyEffect(GEConfig) 施加状态标签
    │     → 每行不同元素：冰/火/雷/混合/无
    │     → GEConfig.Modifiers 添加 DamageTakenMultiplier
    │
    ├── ExecuteReactionTest()
    │     → 重置 → 注入预设 → 施放触发技能
    │     → OnTargetDamaged() 回调
    │       → DetectReaction(): 检查 GE Tags 是否满足反应条件
    │       → ComputeExpectedDamage(): 计算 GE 修正 × 反应倍率
    │       → ReactionVerification: 实际值 vs 期望值
    │       → QAFloatingText: ✅/❌ 反馈
    │
    └── SummarizeTest()
          → 统计通过/失败数量
          → 输出失败明细
```

---

## 四、核心组件详解

### 4.1 QAGalleryTestController — 画廊与压力测试控制器

**文件**: `QA/QAGalleryTestController.cs`

集画廊模式和压力测试于一体的运行时测试控制器。通过 Inspector 配置后挂载到场景即可使用。

**模式枚举**:

| 模式 | 说明 |
|------|------|
| `Idle` | 空闲，不执行测试 |
| `Gallery` | 画廊模式：按顺序释放每个技能，支持慢速回放 |
| `Stress` | 压力模式：高频施法 + 大量靶子，监控性能 |

**Inspector 可配置参数**:

| 分组 | 参数 | 类型 | 默认值 | 说明 |
|------|------|------|--------|------|
| 靶子配置 | `_targetPrefab` | `GameObject` | 自动创建 | 测试用靶子预制体（推荐 QATargetDummy） |
| | `_targetSpacing` | `float` | 3 | 靶子生成阵型间隔 |
| | `_galleryTargetCount` | `int` | 5 | 画廊模式靶子数量 |
| Gallery 配置 | `_galleryInterval` | `float` | 2s | 技能切换间隔 |
| | `_gallerySpeed` | `GallerySpeed` | SlowMotion01 | 画廊速度（Normal/0.1x/0.05x） |
| Stress 配置 | `_stressTargetCount` | `int` | 100 | 压力测试靶子数量 |
| | `_stressCastInterval` | `float` | 0.05s | 每次施法间隔 |
| | `_stressSkillId` | `int` | 11002 | 压力测试技能 ID（默认链燃） |
| 性能监控 | `_autoStartMonitor` | `bool` | true | 自动启动 QAPerformanceMonitor |

**Gallery 速度控制**:

| 枚举值 | TimeScale | 用途 |
|--------|-----------|------|
| `Normal` | 1.0 | 正常速度，快速遍历所有技能 |
| `SlowMotion01` | 0.1 | 10 倍慢速，仔细观察动画同步 |
| `SlowMotion02` | 0.05 | 20 倍慢速，逐帧检查特效细节 |

**关键 API**:

| 方法 | 说明 |
|------|------|
| `StartGallery()` | 启动画廊模式 |
| `StopGallery()` | 停止画廊模式 |
| `StartStress()` | 启动压力测试 |
| `StopStress()` | 停止压力测试并生成报告 |
| `StartRandomInterruptTest()` | 执行随机打断测试 |

**压力测试报告输出**:

```
══════════════════════════════════════
💥 Stress 测试报告
══════════════════════════════════════
持续时间: 12.50s
技能施法次数: 250
靶子峰值数量: 100
FPS 最低值: 45.2
GC 峰值: 32.1 MB
泄露报警次数: 0
总伤害输出: 125,000
平均 DPS: 10000
══════════════════════════════════════
```

**运行时 GUI 面板**:
- 右上角 300px 宽面板
- 模式切换按钮（Gallery / Stress / 停止）
- 画廊速度控制（Normal / 0.1x / 0.05x）
- 压力测试配置显示
- 随机打断测试按钮
- 实时统计：FPS、Active Executions、Targets、Total Damage
- 滚动日志窗口（最近 200 条）

---

### 4.2 QAReactionMatrix — 元素反应矩阵测试沙盒

**文件**: `QA/QAReactionMatrix.cs`

在场景中生成 5×5 木桩方阵，按行注入不同元素状态，施放触发技能后自动验算反应伤害是否与理论值一致。

**Inspector 可配置参数**:

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `_targetPrefab` | `GameObject` | 自动创建 | 木桩预制体 |
| `_rows` / `_cols` | `int` | 5 / 5 | 矩阵行列数 |
| `_spacing` | `float` | 2.5 | 木桩间距 |
| `_origin` | `Vector3` | (0,0,5) | 矩阵左下角起点 |
| `_triggerSkillId` | `int` | 11001 | 触发技能 ID（默认链燃） |
| `_autoVerify` | `bool` | true | 自动验算 |

**行预设状态映射**:

| 行号 | StatusType | 伤害修正 | 说明 |
|------|-----------|---------|------|
| 0 | `Chill`（冰） | +10% 受伤 | 冰元素状态 |
| 1 | `Burn`（火） | +20% 受伤 | 火元素状态 |
| 2 | `Conductive`（雷） | +15% 受伤 | 雷元素状态 |
| 3 | `Chill`（混合） | +30% 受伤 | 混合元素状态 |
| 4 | `None` | 0% | 无状态基准组 |

**验算逻辑**:

```
1. OnTargetDamaged() 触发
2. DetectReaction(): 遍历 GEHost.ActiveEffects 收集 Tags → 匹配 Reaction.csv 条件
3. ComputeExpectedDamage(): 遍历 GE 修正 (DamageTakenMultiplier × Magnitude) × 反应倍率
4. ReactionVerification: |actualDamage - expectedDamage| < 0.5 → ✅ 通过 / ❌ 失败
5. QAFloatingText 显示验算结果
```

**ReactionVerification 数据结构**:

| 字段 | 类型 | 说明 |
|------|------|------|
| `TargetIndex` | `int` | 木桩在矩阵中的索引 |
| `InitialStatus` | `string` | 受击前挂载的状态名 |
| `ReactionName` | `string` | 触发的反应名（含倍率），null=无反应 |
| `ExpectedDamage` | `float` | 期望伤害值 |
| `ActualDamage` | `float` | 实际伤害值 |
| `Passed` | `bool` | 验算是否通过（误差 < 0.5） |
| `Notes` | `string` | 备注信息 |

**关键 API**:

| 方法 | 说明 |
|------|------|
| `BuildMatrix()` | 生成 5×5 木桩方阵 |
| `ClearMatrix()` | 清空矩阵 |
| `ApplyRowPreset(row, status, duration)` | 给指定行注入状态 |
| `ExecuteReactionTest()` | 执行自动化反应穷举测试 |
| `ResetAll()` | 重置所有木桩到满血+清空 GE |

**运行时 GUI 面板**:
- 右上角 340px 宽面板
- 木桩控制：生成矩阵 / 重置
- 元素注入按钮：冰 / 火 / 雷 / 混合 / 清除
- 触发技能 ID 显示 + 执行穷举测试按钮
- 矩阵可视化：5×5 按钮网格（行=预设状态，列=靶子）
  - 绿色 = 验算通过，红色 = 验算失败，蓝色 = 有 GE，灰色 = 死亡
  - 点击选中查看详情
- 选中木桩详情：HP、存活、GE Tags、战斗状态、验算结果

---

### 4.3 QATargetDummy — QA 增强靶子

**文件**: `QA/QATargetDummy.cs`

取代基础 `TargetDummy` 的 QA 增强版靶子，用于所有测试场景。实现 `IDamageable` 接口，通过 `GEHost` 受理伤害修正。

**RequireComponent**: `GEHost`, `HealthComponent`

**Inspector 可配置参数**:

| 分组 | 参数 | 类型 | 默认值 | 说明 |
|------|------|------|--------|------|
| 基础配置 | `_maxHealth` | `float` | 1000 | 初始生命值 |
| 显示配置 | `_uiOffset` | `Vector3` | (0,2.2,0) | 头顶 UI 偏移 |
| | `_floatingTextPrefab` | `QAFloatingText` | null | 浮动文字预制体 |
| | `_hitColorDuration` | `float` | 0.3s | 受击变色持续时间 |
| 状态颜色 | `_normalColor` | `Color` | White | 正常颜色 |
| | `_hitColor` | `Color` | Red | 受击颜色 |
| | `_deadColor` | `Color` | Gray | 死亡颜色 |

**运行时属性**:

| 属性 | 类型 | 说明 |
|------|------|------|
| `CurrentHealth` | `float` | 当前生命值 |
| `HealthRatio` | `float` | 生命值比例（0~1） |
| `IsDead` | `bool` | 是否死亡 |
| `DamageHistory` | `IReadOnlyList<QADamageRecord>` | 伤害历史记录 |
| `GEHostComponent` | `GEHost` | GE 宿主组件引用 |

**事件**:

| 事件 | 签名 | 说明 |
|------|------|------|
| `OnDamaged` | `Action<float, Transform, float>` | 受击回调（finalDamage, source, rawDamage） |
| `OnDied` | `Action` | 死亡回调 |
| `OnReset` | `Action` | 重置回调 |

**伤害结算流程**:

```
TakeDamage(amount, instigator)
    │
    ├── if IsDead or amount ≤ 0: return
    │
    ├── GEHost.EvaluateAttribute(DamageTakenMultiplier, amount)  ← GE 修正
    │
    ├── AttributeSet.TakeDamage() ← 若存在 AttributeSet，由其内部处理
    │
    ├── 扣减 HP: _currentHealth -= finalAmount
    │
    ├── 记录伤害: QADamageRecord 加入 _damageHistory
    │
    ├── QA 反馈:
    │     TriggerHitColor() → 受击变色 0.3s
    │     ShowFloatingText() → 浮动跳字
    │     OnDamaged?.Invoke() → 外部回调
    │
    └── 死亡判定: _currentHealth ≤ 0 → IsDead = true → OnDied?.Invoke()
```

**关键 API**:

| 方法 | 说明 |
|------|------|
| `TakeDamage(amount, instigator)` | IDamageable 接口实现，受击入口 |
| `ResetQA()` | 重置满血 + 清空历史 + 清除所有 GE + 恢复颜色 |
| `GetSnapshot()` | 获取当前状态快照（用于 Odin Inspector 面板） |

**辅助数据结构**:

`QADamageRecord` — 单条伤害记录：

| 字段 | 类型 | 说明 |
|------|------|------|
| `Timestamp` | `float` | 受击时间（Time.time） |
| `RawDamage` | `float` | 原始伤害 |
| `FinalDamage` | `float` | GE 修正后最终伤害 |
| `SourceName` | `string` | 伤害来源名称 |
| `RemainingHealth` | `float` | 剩余生命值 |

`QAStatusSnapshot` — 当前状态快照：

| 字段 | 类型 | 说明 |
|------|------|------|
| `Health` / `HealthRatio` / `IsDead` / `Time` | 基础属性 | 生命值与存活状态 |
| `GETags` | `List<string>` | 当前 GE 名称列表 |
| `GERemainingTimes` | `List<float>` | GE 剩余时间 |
| `GEStacks` | `List<int>` | GE 叠加层数 |
| `StatusTypes` | `List<StatusType>` | 从 GE Tags 映射的状态类型 |
| `StatusValues` / `StatusRemaining` | `List<float>` | 状态值/剩余时间 |

---

### 4.4 QAPerformanceMonitor — 实时性能监控面板

**文件**: `QA/QAPerformanceMonitor.cs`

运行时性能监控 Singleton，通过 OnGUI 绘制 FPS/内存/对象池实时折线图，并检测对象池泄露。

**Singleton**: `QAPerformanceMonitor.Instance`

**Inspector 可配置参数**:

| 分组 | 参数 | 类型 | 默认值 | 说明 |
|------|------|------|--------|------|
| 显示开关 | `_showFps` / `_showMemory` / `_showPool` | `bool` | true | 各指标显示开关 |
| | `_showSkillExecutions` / `_showVFXPool` | `bool` | true | 额外指标开关 |
| | `_enableChart` | `bool` | true | 折线图开关 |
| | `_showAlertOnLeak` | `bool` | true | 泄露报警显示 |
| 面板外观 | `_panelBg` | `Color` | 黑色半透明 | 面板背景色 |
| | `_normalColor` / `_warnColor` / `_alertColor` / `_goodColor` | `Color` | — | 指标等级颜色 |
| 图表参数 | `_chartHistory` | `int` | 120 | 记录帧数 |
| | `_chartHeight` / `_chartWidth` | `float` | 60 / 200 | 图表尺寸 |

**FPS 颜色等级**:

| FPS 范围 | 颜色 | 含义 |
|----------|------|------|
| ≥ 55 | 绿色 (`_goodColor`) | 性能良好 |
| 30~55 | 黄色 (`_warnColor`) | 性能一般 |
| < 30 | 红色 (`_alertColor`) | 性能瓶颈 |

**内存等级**:

| GC Memory | 颜色 |
|-----------|------|
| < 200 MB | `_normalColor` |
| ≥ 200 MB | `_warnColor` |

**泄露检测逻辑**:

```
每 0.25s 采样一次对象池活跃数
连续 20 个采样点，计算趋势（总增量）
趋势 > 50 且连续 5 次 → _isLeakAlert = true
```

**自定义计数器 API**:

| 方法 | 说明 |
|------|------|
| `RegisterCounter(name, color)` | 注册自定义计数器 |
| `SetCounterValue(name, value)` | 更新计数值 |
| `IncrementCounter(name, delta)` | 递增计数器 |
| `ClearCounter(name)` | 移除计数器 |
| `ClearAllCounters()` | 清空所有计数器 |

**公开属性**:

| 属性 | 类型 | 说明 |
|------|------|------|
| `CurrentFPS` | `float` | 当前帧率 |
| `IsLeakAlert` | `bool` | 是否触发泄露报警 |

**GUI 布局**:
- 左下角 280px 宽面板
- FPS 行 + 折线图（120 帧）
- GC Memory 行 + 折线图
- VFX Pool 行 + 折线图
- Active Executions 行
- 自定义计数器行
- 泄露报警行（红色闪烁）

> **子类扩展**: 覆写 `GetVFXPoolCount()` / `GetPoolInactive()` 虚方法，接入真实对象池实现。

---

### 4.5 QADeadlockDetector — 技能图死循环防呆检测器

**文件**: `QA/QADeadlockDetector.cs`（`#if UNITY_EDITOR`）

编辑器工具窗口，后台无头运行所有技能图，检测哪个节点执行超时。通过手动驱动 `SkillExecution.Tick()` 模拟真实运行时行为。

**打开方式**: `Tools/Skills/QA/Deadlock Detector`

**可配置参数**:

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `_timeoutSeconds` | `float` | 5s | 超时阈值（1~30s） |
| `_timeoutFrames` | `int` | 300 | 对应帧数（60fps × 秒数） |
| `_autoPingOnError` | `bool` | true | 检测到问题时自动 Ping 资产 |

**检测流程**:

```
1. AssetDatabase.FindAssets("t:SkillGraph") → 获取所有技能图
2. 遍历每个图:
   a. 创建 DummySkillCaster + SkillContext
   b. SkillExecution.Initialize(graph, ctx)
   c. while (execution.IsRunning && ticks < _timeoutFrames)
        execution.Tick(1/60f)
   d. ticks >= maxTicks → 死循环！记录节点名和堆栈
3. 输出结果列表
```

**DeadlockResult 数据结构**:

| 字段 | 类型 | 说明 |
|------|------|------|
| `GraphName` | `string` | 技能图名称 |
| `GraphPath` | `string` | 资产路径 |
| `FailedNodeName` | `string` | 超时节点名 |
| `ElapsedSeconds` | `float` | 实际执行耗时 |
| `ExecutedTicks` | `int` | 执行的 Tick 数 |
| `StackTrace` | `string` | 堆栈追踪信息 |
| `HasDeadlock` | `bool` | 是否检测到死循环 |

**编辑器 GUI**:
- 超时阈值滑块 + 对应帧数显示
- 运行/清空按钮
- 进度条
- 结果列表：✅/❌ 图标 + 图名 + 执行 tick 数 + 耗时
- 失败项：卡住节点名 + 堆栈文本
- Ping 按钮：在 Project 窗口高亮有问题的资产
- 导出报告：保存为 txt 文件

---

### 4.6 QAEQSDebugger — EQS 查询可视化器

**文件**: `QA/QAEQSDebugger.cs`

挂载到场景物体上的 MonoBehaviour，可视化 EQS（环境查询系统）的查询过程和结果。

**AddComponentMenu**: `Skills QA/EQS Debugger`

**Inspector 可配置参数**:

| 分组 | 参数 | 类型 | 默认值 | 说明 |
|------|------|------|--------|------|
| 查询配置 | `_detectionRange` | `float` | 10m | 检测范围 |
| | `_attackRange` | `float` | 2m | 攻击范围 |
| | `_fieldOfView` | `float` | 60° | 视野角度 |
| | `_targetTeamId` | `int` | 2 | 目标队伍 ID |
| 可视化配置 | `_fanColor` | `Color` | 绿色半透明 | 扇形填充颜色 |
| | `_hitColor` / `_missColor` | `Color` | 蓝/红 | 命中/未命中颜色 |
| | `_lineWidth` | `float` | 0.05 | 连线宽度 |
| 性能统计 | `_enablePerformanceMetrics` | `bool` | true | 启用耗时统计 |

**关键 API**:

| 方法 | 说明 |
|------|------|
| `ExecuteQuery(origin, forward, fov, range, teamId)` | 执行 EQS 查询并可视化 |
| `ExecuteQuery()` | 使用当前 Inspector 配置执行查询 |

**可视化元素**:

| 元素 | 说明 |
|------|------|
| 扇形填充 | 检测范围的扇形区域，`_fanColor` 半透明 |
| 攻击范围圈 | 内圈虚线，`_attackRange` 半径 |
| 外圈 | 检测范围圈，`_detectionRange` 半径 |
| 蓝色准星 | 命中目标：十字准星 + 中心圆 + 指向线条 |
| 红色叉 | 未命中目标：X 形标记 |
| 编号标签 | 命中目标头顶显示 #1, #2... |
| 性能信息 | 场景内 Handles.Label 显示实体数和耗时 |

**查询结果 — SensorQueryResult**:

| 字段 | 说明 |
|------|------|
| `EnemyCount` | 发现的敌人数量 |
| `AllyCount` | 友军数量 |
| `NearestEnemyEntityId` | 最近敌人 EntityId |
| `NearestEnemyDistance` | 最近敌人距离 |
| `HasTargetInAttackRange` | 是否有目标在攻击范围内 |

**Gizmo 显示**:
- `OnDrawGizmosSelected` 时绘制
- 查询后 3 秒自动隐藏
- 包含扇形、圈、准星、叉、连线

**运行时 GUI**:
- 左上角面板：显示 Targets Found、Nearest Distance、In Attack Range、Query Time、Entities Scanned

---

### 4.7 QAAITacticsSandbox — AI 战术沙盘

**文件**: `QA/QAAITacticsSandbox.cs`

场景级 AI 战术调试工具，提供 SpatialHashGrid 网格可视化、刷怪/刷墙笔刷和 EQS 集成。

**Singleton**: `QAAITacticsSandbox.Instance`

**AddComponentMenu**: `Skills QA/AI Tactics Sandbox`

**Inspector 可配置参数**:

| 分组 | 参数 | 类型 | 默认值 | 说明 |
|------|------|------|--------|------|
| 场景配置 | `_gridCellSize` | `int` | 10 | 网格单元大小 |
| | `_gridHeight` | `float` | 0.05 | 网格绘制高度 |
| 刷怪配置 | `_spawnableMinionPrefab` | `GameObject` | — | 怪物预制体 |
| | `_spawnableObstaclePrefab` | `GameObject` | — | 障碍物预制体 |
| | `_spawnableTeamId` | `int` | 1 | 生成实体的队伍 ID |
| 可视化配置 | `_showGrid` / `_showSpawnPoints` | `bool` | true | 显示开关 |
| | `_gridColor` / `_spawnPointColor` / `_obstacleColor` | `Color` | — | 各类元素颜色 |
| 调试 | `_showEntityIds` | `bool` | false | 显示实体 ID |
| | `_showTeamColors` | `bool` | true | 显示队伍颜色 |

**关键 API**:

| 方法 | 说明 |
|------|------|
| `AddSpawnPoint(position)` | 添加刷怪点 |
| `ClearSpawnPoints()` | 清除所有刷怪点 |
| `SpawnAllMinions()` | 在所有刷怪点生成怪物 |
| `SpawnObstacle(position, size)` | 生成障碍物 |
| `ClearEntities()` | 清空所有已生成实体 |
| `ClearAll()` | 清空实体 + 刷怪点 |
| `RunEQSQuery(origin, forward, fov, range)` | 运行 EQS 查询 |
| `GetEntityCount()` | 获取 SpatialHashGrid 注册实体数 |
| `GetGridStats()` | 获取网格统计（实体数/格子数/脏实体数） |

**Gizmo 绘制**:

| 元素 | 说明 |
|------|------|
| SpatialHashGrid 网格 | 已激活格子的 WireCube 绘制 |
| 刷怪点 | 线框球 + 顶部标记（线 + 小球） |
| 选中对象最近刷怪点 | 连线 |
| 障碍物 | WireCube 标记 |

**Editor 笔刷工具**:

| 菜单 | 说明 |
|------|------|
| `Tools/Skills/QA/AI Brush Mode` | 切换笔刷模式：左键=刷怪点，Shift+左键=障碍物 |
| `Tools/Skills/QA/Clear AI Sandbox` | 清空沙盒 |

---

### 4.8 QAFloatingText — 战斗浮动文字

**文件**: `QA/QAFloatingText.cs`

世界空间浮动文字组件，支持伤害、暴击、治疗、Buff、元素反应等多种类型显示。

**Inspector 可配置参数**:

| 分组 | 参数 | 类型 | 默认值 | 说明 |
|------|------|------|--------|------|
| 显示参数 | `_liftSpeed` | `float` | 1.5 | 上升速度 |
| | `_lifetime` | `float` | 1.2s | 总生存时间 |
| | `_fadeStartRatio` | `float` | 0.6 | 开始淡出的时间比例 |
| | `_randomOffsetX` | `float` | 0.5 | X 轴随机偏移 |
| 文字样式 | `_fontSize` | `int` | 24 | 基础字号 |
| | `_font` | `Font` | LegacyRuntime | 字体 |
| | `_useOutLine` | `bool` | true | 是否使用描边 |

**显示类型**:

| 方法 | 颜色 | 字号 | 格式 |
|------|------|------|------|
| `ShowDamage(damage, isCritical, isHeal)` | 红/橙/绿 | 24/30 | `-100` / `暴!-150` / `+50` |
| `ShowBuff(buffName, isApplied)` | 蓝色 | 20 | `[+Burn]` / `[-Burn]` |
| `ShowReaction(reactionName, bonusDamage)` | 紫色 | 26 | `Melt\n+200` |
| `ShowCustom(text, color, fontSize)` | 自定义 | 自定义 | 任意文本 |

**动画效果**:
- 上升：沿 Y 轴匀速上升
- 面向相机：实时旋转朝向主相机
- 淡出：超过 `_fadeStartRatio` 后逐渐透明
- 缩放弹跳：`1 + sin(t * 8) * 0.05 * (1 - fadeRatio)` 微弱弹跳
- 随机偏移：X 轴 ±0.5 随机抖动

**内部实现**:
- 自动创建 `Canvas`（WorldSpace, sortingOrder=100）+ `CanvasScaler`
- `UnityEngine.UI.Text` + `Outline`（黑色描边，偏移 1.5）
- `Update` 中驱动动画，超时自动 `Destroy`

---

## 五、使用指南

### 5.1 快速开始 — 画廊模式

1. 场景中创建空 GameObject，添加 `QAGalleryTestController` 组件
2. 确保场景中有 `SkillTickManager` 和 `SpatialHashGrid`（或勾选自动创建）
3. 运行场景，点击右上角面板的 **Gallery** 按钮
4. 技能将按顺序自动释放，可通过速度按钮切换 Normal / 0.1x / 0.05x
5. 观察 Scene 视图中技能释放的同步性和特效表现

### 5.2 快速开始 — 压力测试

1. 同上配置 `QAGalleryTestController`
2. 运行场景，点击 **Stress** 按钮
3. 系统自动生成 100 个靶子，每 0.05s 施法一次
4. 左下角 `QAPerformanceMonitor` 实时显示 FPS/内存/对象池
5. 点击 **停止测试** 查看完整报告

### 5.3 快速开始 — 元素反应矩阵

1. 场景中创建空 GameObject，添加 `QAReactionMatrix` 组件
2. 运行场景，点击 **生成矩阵** 按钮
3. 使用 **冰/火/雷/混合** 按钮注入状态
4. 点击 **执行穷举测试** — 系统自动注入预设并施放技能
5. 查看矩阵视图中每个木桩的 ✅/❌ 验算结果

### 5.4 快速开始 — 死锁检测

1. 菜单 `Tools/Skills/QA/Deadlock Detector` 打开窗口
2. 调整超时阈值（默认 5s）
3. 点击 **运行死循环检测**
4. 等待进度条完成
5. 查看结果列表，红色 ❌ 为有问题的技能图
6. 点击 **Ping** 在 Project 窗口定位问题资产
7. 可点击 **导出报告** 保存 txt 文件

### 5.5 快速开始 — EQS 调试

1. 在需要调试的 AI 实体上添加 `QAEQSDebugger` 组件
2. 配置 `_detectionRange`、`_fieldOfView`、`_targetTeamId`
3. 选中该物体，Scene 视图中可见扇形范围
4. 运行时调用 `ExecuteQuery()` 或通过脚本触发
5. Scene 视图中显示：扇形区域、蓝色准星（命中）、红色叉（未命中）
6. 左上角面板显示查询统计

### 5.6 快速开始 — AI 战术沙盒

1. 场景中创建空 GameObject，添加 `QAAITacticsSandbox` 组件
2. 配置怪物和障碍物预制体
3. 菜单 `Tools/Skills/QA/AI Brush Mode` 开启笔刷
4. Scene 视图中左键点击放置刷怪点，Shift+左键放置障碍物
5. 点击 **生成怪物** 批量生成
6. 调用 `RunEQSQuery()` 测试 AI 感知
7. 菜单 `Tools/Skills/QA/Clear AI Sandbox` 清空

---

## 六、QATargetDummy 与 GEHost 的交互

QATargetDummy 是整个 QA 模块的基石组件，其伤害结算完全通过 GAS 系统驱动：

```
SkillCaster.TryCast()
    → ApplyEffectNode → ConfigLoader → GameplayEffectData → EffectSystem
        → DamagePipeline.CalculateAndApply()
            → GEHost.EvaluateAttribute(DamageTakenMultiplier)
                → Modifier Pipeline: Add → Override → Multiply
            → QATargetDummy.TakeDamage(finalAmount, instigator)
                → 记录 QADamageRecord
                → QAFloatingText.ShowDamage()
                → OnDamaged 事件回调
```

**GE 状态注入**（反应矩阵使用）:

```csharp
var cfg = new GEConfig
{
    GEId = tag.GetHashCode(),
    Name = tag,
    DurationPolicy = GEDurationPolicy.HasDuration,
    Duration = 10f,
    MaxStacks = 10,
    StackPolicy = GEStackPolicy.Add
};
cfg.GrantedTags.Add(tag);           // 授予标签（用于反应检测）
cfg.Modifiers.Add(new GEModifier    // 伤害修正
{
    Attribute = GEAttribute.DamageTakenMultiplier,
    Operation = GEModOp.Multiply,
    Magnitude = 1.2f               // +20% 受伤
});
geHost.ApplyEffect(cfg, null);
```

**状态快照读取**:

```csharp
var snap = targetDummy.GetSnapshot();
// snap.GETags — 当前所有 GE 名称
// snap.GERemainingTimes — GE 剩余时间
// snap.GEStacks — GE 叠加层数
// snap.StatusTypes — 从 GE Tags 映射的 StatusType
```

---

## 七、QAPerformanceMonitor 自定义计数器

QAPerformanceMonitor 支持注册自定义计数器，用于监控业务特定的指标：

```csharp
// 注册计数器
QAPerformanceMonitor.Instance.RegisterCounter("SkillStage", Color.cyan);
QAPerformanceMonitor.Instance.RegisterCounter("ActiveGEs", Color.magenta);

// 更新数值（通常在业务代码中调用）
QAPerformanceMonitor.Instance.SetCounterValue("SkillStage", (int)currentStage);
QAPerformanceMonitor.Instance.SetCounterValue("ActiveGEs", geHost.ActiveEffects.Count);

// 递增
QAPerformanceMonitor.Instance.IncrementCounter("TotalCasts");

// 清理
QAPerformanceMonitor.Instance.ClearCounter("SkillStage");
```

> **QAGalleryTestController 集成示例**：画廊控制器在 `CreateCaster()` 中注册了 `"SkillStage"` 计数器，并在 `OnStageChanged` 回调中更新数值。

---

## 八、StatusType 枚举参考

QA 模块使用的 `StatusType` 枚举（定义于 `Runtime/StatusType.cs`）：

| 值 | 名称 | 说明 |
|----|------|------|
| 0 | `None` | 无状态 |
| 1 | `Burn` | 燃烧 |
| 2 | `Chill` | 冰冻减速 |
| 3 | `Conductive` | 导电 |
| 4 | `Mark` | 标记 |
| 5 | `Freeze` | 冰封 |
| 6 | `Slow` | 减速 |
| 7 | `Stun` | 眩晕 |
| 8 | `Poison` | 中毒 |
| 9 | `Root` | 定身 |

反应矩阵测试中，`ApplyGERowPreset()` 将 `StatusType` 转换为小写字符串作为 GameplayTag 注入 GE，同时通过 `GEConfig.Modifiers` 添加对应的 `DamageTakenMultiplier` 修正。

---

## 九、注意事项

1. **GEHost 依赖**：`QATargetDummy` 必须配合 `GEHost` 组件使用（`[RequireComponent]` 自动添加）。伤害修正确保通过 GE 管道，与正式战斗流程一致。

2. **HealthComponent 与 AttributeSet**：当 QATargetDummy 上同时存在 `AttributeSet` 时，`TakeDamage()` 会委托给 `AttributeSet.TakeDamage()` 处理，避免双重扣血。

3. **SpatialHashGrid 初始化**：画廊和反应矩阵测试依赖 `SpatialHashGrid` 进行目标查询。`QAGalleryTestController.EnsureManagers()` 会自动创建 Singleton 实例。

4. **ConfigLoader 初始化**：所有 QA 组件在 `Start()` 中调用 `ConfigLoader.Initialize()`，确保 CSV 数据已加载。

5. **画廊模式 TimeScale**：画廊模式会修改 `Time.timeScale`（0.1 或 0.05），但内部使用 `Time.unscaledDeltaTime` 计时，不受慢速影响。停止测试后自动恢复 `timeScale = 1`。

6. **QADeadlockDetector 仅编辑器**：`#if UNITY_EDITOR` 保护，不会打包到正式构建中。

7. **QAPerformanceMonitor 对象池检测**：默认 `GetVFXPoolCount()` 返回 0，需子类覆写或手动对接真实 `VFXObjectPool`。泄露检测基于趋势分析，非精确计数。

8. **QAFloatingText 生命周期**：浮动文字在 `_lifetime` 后自动 `Destroy(gameObject)`，无需手动回收。如需对象池化，需自行改造。

9. **反应矩阵验算精度**：通过 `Mathf.Abs(actual - expected) < 0.5` 判断，容忍浮点误差 0.5。如需更严格/宽松的判定，修改 `QAReactionMatrix.OnTargetDamaged()` 中的阈值。

10. **QA 组件不属于正式构建**：建议所有 QA 组件放在 `QA/` 目录，打包时通过 Assembly Definition 或脚本符号排除。

11. **打断测试的协程**：`QAGalleryTestController.StartRandomInterruptTest()` 内部使用 `StartCoroutine(Invoke(...))` 延迟触发打断。这是 QA 工具中唯一使用协程的地方，业务节点仍遵循 Tick 驱动。

12. **EQS Gizmo 显示条件**：`QAEQSDebugger` 的 Gizmo 仅在物体被选中（`OnDrawGizmosSelected`）时绘制，查询结果 3 秒后自动隐藏。
