# 技能系统 & AI 行为树 — 商业级战斗框架

**CSV 数据驱动 + 自建技能图框架 + NodeCanvas AI 行为树 + Tick 驱动执行 + GAS 状态系统 + 高性能表现**

---

## 0. 项目目标

构建一套完整的 **ARPG / 塔防 战斗框架**，覆盖以下能力域：

| 域 | 能力 | 状态 |
|----|------|------|
| **技能图** | 自建 SkillGraphAsset 框架，SkillNodeBase + SkillEdge，CSV 数值驱动，子图复用 | ✅ 52 技能配方 |
| **GAS 效果系统** | GameplayEffectData + EffectSystem + Modifier 管线 + ReactionEngine | ✅ 52 效果配置 |
| **EQS** | 可插拔 Filter/Sorter 目标查询，类 Unreal EQS | ✅ |
| **动画同步** | 动画事件驱动技能逻辑，替代 DelayNode，攻速自动对齐 | ✅ |
| **AI 行为树** | NodeCanvas BT + CSV 配置自动生成，优先级选择 + 并行 | ✅ 8 Action / 3 Tier |
| **AI 感知** | SpatialHashGrid + Burst Job 并行 FOV 检测，0 主线程开销 | ✅ |
| **VFX** | Shader + 对象池 + 投射物，中端机 10 次连续触发无卡顿 | ✅ |

---

## 1. 总体架构（六层分离）

```text
┌──────────────────────┐
│  CSV 配置层          │ ← 策划维护：Skill.csv、Buff.csv、AITree.csv、SkillRecipe.csv
├──────────────────────┤
│  自建技能图层        │ ← SkillGraphAsset + SkillNodeBase + SkillEdge（技能图，无第三方依赖）
├──────────────────────┤
│  NodeCanvas AI 层    │ ← AI 行为树（NodeCanvas BehaviourTree，独立于技能系统）
├──────────────────────┤
│  GAS 效果层          │ ← GameplayEffectData + EffectSystem + Modifier Pipeline + ReactionEngine
├──────────────────────┤
│  Tick 运行时引擎     │ ← SkillTickManager + SkillCaster + SkillRunner + TimelineSkillRunner（0 GC 驱动）
├──────────────────────┤
│  表现层 & AI 感知    │ ← VFXManager (对象池) + SpatialHashGrid + Burst Jobs
└──────────────────────┘
```

**核心原则：**
- **CSV 管数值，自建框架管技能图，NodeCanvas 管 AI 行为树，Runtime 管执行，GAS 管效果，表现层只做展示**
- **技能系统完全独立于 NodeCanvas**：SkillNodeBase（自建）替代 NodeCanvas Node，SkillEdge（自建）替代 Connection，SkillGraphAsset（自建）替代 Graph
- **AI 系统继续使用 NodeCanvas BehaviourTree**，与技能系统无耦合
- **技能节点必须使用 Tick 驱动（NodeTickResult），禁止 IEnumerator 协程**
- 目标筛选走 **EQS Filter/Sorter 管道**，不硬编码查找逻辑
- 伤害结算走 **EffectSystem → DamagePipeline 事件拦截**，不在节点中直调 IDamageable
- AI 感知走 **SpatialHashGrid + Burst Job**，不使用 Physics.OverlapSphere
- 战斗效果走 **ApplyEffectNode → ConfigLoader → GameplayEffectData → EffectSystem**，禁止直接写黑板伤害/暴击值

---

## 2. 项目目录结构

按 **架构层次 → 模块功能 → 生命周期** 三级划分，各模块内部保持统一的子目录模板：

```text
Assets/
├── Scripts/
│   ├── Core/                        # 基础设施层（跨模块共享，0 业务依赖）
│   │   ├── Data/                    # ConfigLoader + 所有 CSV 数据映射类
│   │   ├── Binding/                 # FloatBinding / StringBinding
│   │   └── Tags/                    # GameplayTag + GameplayTagContainer
│   │
│   ├── SkillSystem/                 # 技能系统（自建技能图框架，无第三方依赖）
│   │   ├── Graph/                   # SkillGraphAsset + SkillNodeBase + SkillEdge
│   │   ├── Runtime/                 # 执行引擎：TickManager, Runner, Execution, Blackboard
│   │   ├── Pipeline/                # 释放管线：SkillCaster, SkillContext, SkillTimeline
│   │   ├── Nodes/                   # 技能节点（按生命周期/功能分组）
│   │   │   ├── Flow/                # 流程控制：Start/End/Delay/Condition/Parallel/SubGraph
│   │   │   ├── Casting/             # 释放管线：PreCast/Channel/PostCast
│   │   │   ├── Animation/           # 动画同步：AnimationEventWaitNode
│   │   │   ├── Combat/              # 战斗结算：Damage/ApplyStatus/Reaction/Resonance
│   │   │   ├── GAS/                 # 效果应用：ApplyEffectNode
│   │   │   ├── VFX/                 # 视觉表现：PlayVFX/Projectile/PaintTerrain
│   │   │   └── Utility/             # 工具节点：Log/SetValue/ModifyFloat/RollChance...
│   │   └── Editor/                  # 技能图编辑器：DebugWindow, Validator, Breakpoint
│   │
│   ├── GAS/                         # GameplayEffect 系统（状态驱动）
│   │   ├── Core/                    # GEHost, GameplayEffectSystem, EffectSystem, EffectSpec
│   │   ├── Pipeline/                # DamagePipeline, ReactionEngine
│   │   ├── Attribute/               # IDamageable, HealthComponent
│   │   ├── Status/                  # StatusType, StatusRuntime, IStatusReceiver
│   │   ├── Event/                   # TagEventBus
│   │   └── Terrain/                 # TerrainEffectSystem, TerrainRuntime
│   │
│   ├── EQS/                         # 环境查询系统（可插拔 Filter/Sorter）
│   │   └── Core/                    # TargetQueryConfig, TargetFilter
│   │
│   ├── AI/                          # AI 行为树（NodeCanvas）
│   │   ├── Core/                    # AIController, MinionBrain, 节点基类
│   │   ├── Perception/              # SpatialHashGrid, AISensorJobSystem
│   │   ├── Actions/                 # 8 个行为节点
│   │   ├── Conditions/              # 7 个条件节点
│   │   ├── Composites/              # 组合节点
│   │   ├── Decorators/              # 装饰器节点
│   │   ├── Data/                    # AITreeConfig
│   │   ├── Graph/                   # AIGraph 资产定义
│   │   └── Editor/                  # AIGraphMenu, AITreeGenerator
│   │
│   ├── Presentation/                # 表现层（只做展示，无业务逻辑）
│   │   ├── VFX/
│   │   │   ├── Core/                # VFXManager, ObjectPool, Base, Request
│   │   │   └── Effects/             # 各特效实现（ArcBeam/Beam/Bulwark...）
│   │   ├── Animation/               # SkillAnimatorController
│   │   └── Projectiles/             # Projectile（飞行/命中/回收）
│   │
│   ├── Entity/                      # 实体组件
│   │   ├── SkillOwner.cs
│   │   └── TargetDummy.cs
│   │
│   ├── QA/                          # QA 测试模块（零侵入）
│   │   ├── QAGalleryTestController.cs
│   │   ├── QAReactionMatrix.cs
│   │   ├── QATargetDummy.cs
│   │   ├── QAPerformanceMonitor.cs
│   │   ├── QADeadlockDetector.cs
│   │   ├── QAEQSDebugger.cs
│   │   ├── QAAITacticsSandbox.cs
│   │   └── QAFloatingText.cs
│   │
│   └── Editor/                      # 全局编辑器工具
│       ├── Generators/              # 图/配置/VFX 生成器 + Postprocessor
│       ├── Validators/              # 数据校验器
│       └── Tools/                   # Dashboard 等综合面板
│
├── Resources/
│   ├── Config/                      # CSV 配置（单一数据源）
│   │   ├── Skill.csv
│   │   ├── SkillRecipe.csv          # 含 effect_id 映射列
│   │   ├── GameplayEffect.csv       # ★ GAS 效果配置（52 条）
│   │   ├── NodePreset.csv
│   │   ├── Buff.csv
│   │   ├── Effect.csv               # VFX 效果配置
│   │   └── AITree.csv
│   ├── SkillGraphs/                 # 技能图资产（SkillGraphAsset）
│   │   ├── ElementLine/Recipes/
│   │   └── ElementLine/Common/
│   ├── AITrees/                     # AI 行为树资产（自动生成）
│   ├── AIGraphs/                    # AI 行为树模板
│   └── VFX/                         # 特效预制体
│
├── Shaders/                         # ElementLine 特效 Shader
└── doc/                             # 文档
    ├── readme.md                    ← 本文档
    ├── SkillDesignGuide.md          # 系统设计说明书
    ├── NodeAuthoringWorkbook.md     # 节点搭建工作手册
    ├── ElementLineSkillCatalog.md   # 技能总表
    ├── AIModuleDocumentation.md     # AI 模块说明
    └── QAModuleDocumentation.md     # QA 测试模块说明
```

---

## 3. 核心系统速览

### 3.1 技能图（SkillGraphAsset）

- 基于**自建图框架**（SkillGraphAsset + SkillNodeBase + SkillEdge），无第三方依赖
- **Tick 驱动**：`ISkillNodeLogic.Tick(SkillContext ctx, float deltaTime)` — 0 GC，0 框架依赖，执行引擎仅依赖接口
- **执行权分离**：`SkillExecution` 通过 `ISkillNodeLogic` 驱动逻辑，通过 `SkillNodeBase` 驱动图导航
- 释放管线：`Idle → PreCast → Executing → PostCast → Idle`（可打断）
- 子图复用：`SubGraphNode` 引用共享 `Common_*` 资产
- **与 NodeCanvas 无耦合**：技能系统不依赖任何 NodeCanvas 类型

### 3.2 GE / Buff 系统

- GAS 风格：`GEConfig`（CSV 行）→ `GameplayEffectInstance`（运行时实例）→ `GEHost`（组件）
- **Modifier 队列**：`Add → Override → Multiply` 顺序计算属性最终值
- **GameplayTag**：分层级匹配（`"tag.status.burn"` 被 `"tag.status"` 命中）
- **事件拦截**：`DamagePipeline.OnPreCalculate += (ctx) => { ctx.Value *= 1.5f; }`
- `AttributeSet`：基础属性容器，配合 `GEHost.EvaluateAttribute()` 动态计算

### 3.3 EQS 目标查询

- 可插拔 `ITargetFilter` + `ITargetSorter` 接口
- 内置实现：`DistanceFilter`、`TagFilter`、`HPThresholdFilter`、`TeamFilter`
- 内置排序：`ByDistanceSorter`、`ByHPSorter`、`ByThreatSorter`
- `TargetQueryConfig.Execute()` — 空间哈希范围查询 → 过滤链 → 排序 → Top-N

### 3.4 动画同步

- `SkillAnimatorController`：播放动画 + 绑定 `SkillContext`，攻速自动同步
- `AnimationEventReceiver`：AnimationClip Event 入口
- `AnimationEventWaitNode`：等待动画事件推进（替代 `DelayNode`）
- 黑板通信：`BBKey.AnimEvent` / `AnimOnHit` / `AnimOnCastEnd`

### 3.5 AI 行为树

- 基于 NodeCanvas `BehaviourTree`，8 个 Action、7 个 Condition
- **CSV 驱动生成**：`AITree.csv` → `AITreeGenerator` → `AIGraph` 资产
- 三层 Tier 策略：
  - **Minion**：轻量级 FSM（`MinionBrain`，0 序列化开销）
  - **Elite / Boss**：完整 NodeCanvas BT（`AIController`）
  - **Tower**：专用 BT（包含站位、索敌、技能循环）
- **AI 系统使用 NodeCanvas，与技能系统完全独立**

### 3.6 AI 感知（高性能）

- **SpatialHashGrid**：O(1) 范围查询，替代 Physics.OverlapSphere
- **Burst Job 并行**：`AISensorJobSystem` + `FOVDistanceJob`，FOV + 距离检测跑在多线程
- 数据流：`SpatialHashGrid` 导出 `NativeArray<AIEntityNativeData>` → Job 并行计算 → 主线程回读

### 3.7 混合编译架构（SkillBuilder → TimelineSkillRunner）

为兼顾**配置灵活性**与**运行时性能**，技能系统引入可选的**编译时间轴**机制：

```text
SkillGraphAsset（编辑期描述）
    ↓ SkillBuilder.Compile()
SkillData（ScriptableObject，含时间轴 SkillStep[]）
    ↓ TimelineSkillRunner.Start()
按时间轴触发 SkillStep → SkillEffectData → EffectSystem / VFX / Animation
```

| 特性 | 说明 |
|------|------|
| **编译模式** | `FullTimeline`（全静态）、`Hybrid`（静动混合）、`FallbackOnly`（纯 Tick 回退） |
| **可编译节点** | DamageNode、PlayVFXNode、ApplyEffectNode、SetValueNode → 生成 `SkillEffectData`；DelayNode / PreCastNode / PostCastNode → 贡献时间轴位移 |
| **动态回退** | ConditionNode、RollChanceNode、AnimationEventWaitNode 标记为 `IsDynamic`，触发 `OnDynamicStep` 事件，由外部 Tick 解释器处理后回调 `ResumeAfterDynamic()` |
| **0 GC 执行** | TimelineSkillRunner 纯数据驱动，无 IEnumerator，无反射，时间轴推进仅比较浮点数 |
| **统一入口** | `EffectSystemDispatcher.Apply()` 将 `SkillEffectData` 映射到 `DamagePipeline` / `EffectSystem.ApplyEffect()` / `VFXManager.Play()` |

**使用方式**：在 Project 窗口选中 `SkillGraphAsset` → 右键 `Skill System/Compile Selected Graph` → 生成 `Assets/Resources/SkillData/XXX_Data.asset`，运行时可通过 `TimelineSkillRunner` 直接加载执行。

---

## 4. 关键设计决策

| 原则 | 说明 |
|------|------|
| **技能系统独立** | 技能图使用自建框架（SkillNodeBase + SkillEdge + SkillGraphAsset），不依赖 NodeCanvas |
| **执行权分离** | 逻辑执行走 `ISkillNodeLogic`（0 框架依赖），图导航走 `SkillNodeBase`（自建框架），两者解耦 |
| **Tick 驱动，0 GC** | 所有节点返回 `NodeTickResult`，由 `SkillTickManager` 统一调度，禁止 `IEnumerator` |
| **事件拦截，非硬编码** | 伤害通过 `DamagePipeline.CalculateAndApply()`，GE 通过 `OnPreCalculate` 事件拦截修改 |
| **Tag 驱动逻辑** | 状态判定使用 `GEHost.HasTag("tag.status.burn")`，不在节点中写 switch-case |
| **动画替代延时** | 使用 `AnimationEventWaitNode`，不依赖 `DelayNode` 对齐动画帧 |
| **空间哈希替代物理** | AI 感知使用 `SpatialHashGrid.QueryRange()`，不调用 `Physics.OverlapSphere` |
| **对象池强制** | 所有 VFX 通过 `VFXObjectPool.Get/Release`，禁用 `Instantiate/Destroy` |
| **Blackboard 通信** | 节点间不直接传参，全部通过 `Blackboard.SetValue/GetValue` |

---

## 5. 开发规范

| 规范 | 内容 |
|------|------|
| **CSV 单一数据源** | 所有运行时数值从 CSV 获取，节点内不硬编码 |
| **节点命名** | `XxxNode`；子图命名 `Skill_Xxx`；BBKey 为 PascalCase 常量 |
| **协程禁止** | 全部节点必须实现 `ISkillNodeLogic.Tick()` 返回 `NodeTickResult`，不得使用 `IEnumerator` |
| **新增 Node** | 必须继承 `SkillNodeBase`，重写 `Tick()` 返回 `NodeTickResult` |
| **新增 BBKey** | 必须使用 `BBKey.cs` 中的 `const string`，禁止裸字符串 |
| **VFX 必须对象池** | 通过 `VFXManager.Play(key, ...)` 调用 |
| **伤害必须管道** | 通过 `DamagePipeline.CalculateAndApply()` 结算，不直调 `IDamageable.TakeDamage()` |

---

## 6. 相关文档索引

| 文档 | 内容 |
|------|------|
| [SkillDesignGuide.md](SkillDesignGuide.md) | 全面设计说明书 — 节点目录、BBKey 参考、CSV 字段、释放管线 |
| [NodeAuthoringWorkbook.md](NodeAuthoringWorkbook.md) | 节点搭建手册 — 预设→图流程、绑定规范、通信模式 |
| [ElementLineSkillCatalog.md](ElementLineSkillCatalog.md) | 《元素阵线》技能总表 — 52 配方 × 4 元素 × 12 预设 |
| [AIModuleDocumentation.md](AIModuleDocumentation.md) | AI 行为树模块说明 — CSV 驱动生成、Action/Condition 节点、感知系统 |
| [QAModuleDocumentation.md](QAModuleDocumentation.md) | QA 测试模块说明 — 画廊/压力测试、反应矩阵、性能监控、死锁检测、EQS 调试、AI 沙盒 |

---

> **核心宗旨**：这是一条 **战斗系统生产流水线** —— 策划填表（CSV），设计师连线（自建技能图框架），程序守护管道（Runtime / GE / EQS / AI）。四者各司其职，系统才能规模化和可维护。
