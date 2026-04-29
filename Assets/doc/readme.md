# 技能系统 & AI 行为树 — 商业级战斗框架

**CSV 数据驱动 + CanvasCore 可视化逻辑 + Tick 驱动执行 + GE 状态系统 + AI 行为树 + 高性能表现**

---

## 0. 项目目标

构建一套完整的 **ARPG / 塔防 战斗框架**，覆盖以下能力域：

| 域 | 能力 | 状态 |
|----|------|------|
| **技能图** | CanvasCore 可视化连线，CSV 数值驱动，子图复用 | ✅ 52 技能配方 |
| **GE/Buff** | GAS 风格 Gameplay Effect，Tag 驱动 Modifier 队列，事件拦截 | ✅ |
| **EQS** | 可插拔 Filter/Sorter 目标查询，类 Unreal EQS | ✅ |
| **动画同步** | 动画事件驱动技能逻辑，替代 DelayNode，攻速自动对齐 | ✅ |
| **AI 行为树** | NodeCanvas BT + CSV 配置自动生成，优先级选择 + 并行 | ✅ 8 Action / 3 Tier |
| **AI 感知** | SpatialHashGrid + Burst Job 并行 FOV 检测，0 主线程开销 | ✅ |
| **VFX** | Shader + 对象池 + 投射物，中端机 10 次连续触发无卡顿 | ✅ |

---

## 1. 总体架构（五层分离）

```text
┌──────────────────────┐
│  CSV 配置层          │ ← 策划维护：Skill.csv、Buff.csv、AITree.csv、SkillRecipe.csv
├──────────────────────┤
│  NodeCanvas 逻辑层   │ ← 可视化连线：SkillGraph（技能图）+ AIGraph（行为树）
├──────────────────────┤
│  GameplayEffect 层   │ ← GE/Buff + GameplayTag + DamagePipeline 事件拦截
├──────────────────────┤
│  Tick 运行时引擎     │ ← SkillTickManager + SkillCaster + SkillRunner（0 GC 驱动）
├──────────────────────┤
│  表现层 & AI 感知    │ ← VFXManager (对象池) + SpatialHashGrid + Burst Jobs
└──────────────────────┘
```

**核心原则：**
- **CSV 管数值，CanvasCore 管逻辑，Runtime 管执行，GE 管状态，表现层只做展示**
- **技能节点必须使用 Tick 驱动（NodeTickResult），禁止 IEnumerator 协程**
- 目标筛选走 **EQS Filter/Sorter 管道**，不硬编码查找逻辑
- 伤害结算走 **DamagePipeline 事件拦截**，不在节点中直调 IDamageable
- AI 感知走 **SpatialHashGrid + Burst Job**，不使用 Physics.OverlapSphere

---

## 2. 项目目录结构

```text
Assets/
├── Scripts/
│   ├── AI/                          # AI 行为树 + 感知系统
│   │   ├── Core/                    # AIController、AISensor、MinionBrain、SpatialHashGrid、AISensorJobSystem
│   │   ├── Actions/                 # 8 个行为节点（Attack、Move、Flee、Idle…）
│   │   ├── Conditions/              # 7 个条件节点
│   │   ├── Composites/              # PrioritizedSelector、ParallelAll
│   │   ├── Decorators/              # Cooldown、TargetObserver
│   │   ├── Data/                    # AITreeConfig（CSV 行）
│   │   ├── Editor/                  # AIGraphMenu、AITreeGenerator、AITreeSyncPostprocessor
│   │   └── Graph/                   # AIGraph 资产定义
│   │
│   ├── Data/                        # SkillConfig、ConfigLoader、FloatBinding、StringBinding
│   │
│   ├── Graph/                       # SkillGraph（技能图定义）、SkillConnection
│   │
│   ├── Nodes/                       # 24 种技能节点
│   │   ├── SkillNode.cs             # 基类（Tick 驱动，NodeTickResult）
│   │   ├── StartNode / EndNode / DelayNode / LogNode / ParallelNode
│   │   ├── ConditionNode / SubGraphNode / SetValueNode
│   │   ├── DamageNode / ApplyStatusNode / ReactionNode / ResonanceNode
│   │   ├── PlayVFXNode / ProjectileNode / PaintTerrainNode
│   │   ├── PreCastNode / ChannelNode / PostCastNode
│   │   ├── FinisherStagedNode / ModifyFloatNode / RollChanceNode
│   │   └── AnimationEventWaitNode   # 动画事件等待（替代 DelayNode）
│   │
│   ├── Runtime/                     # 运行时核心
│   │   ├── SkillRunner.cs           # 图执行器（Tick 驱动）
│   │   ├── SkillCaster.cs           # 释放管线（CastStage 状态机）
│   │   ├── SkillTickManager.cs      # 全局 Tick 调度
│   │   ├── SkillContext.cs          # 单次释放上下文
│   │   ├── SkillExecution.cs        # 执行实例（暂停/单步/继续）
│   │   ├── Blackboard.cs / BBKey.cs # 黑板系统
│   │   ├── DebugRecorder.cs         # 执行记录与回放
│   │   │
│   │   ├── GameplayEffectSystem.cs  # GE/Buff 系统 + AttributeSet
│   │   ├── GameplayTag.cs           # 分层级 GameplayTag + GameplayTagContainer
│   │   ├── DamagePipeline.cs        # 事件驱动伤害管道
│   │   │
│   │   ├── TargetQueryConfig.cs     # EQS 查询配置（可插拔 Filter/Sorter）
│   │   ├── TargetFilter.cs          # ITargetFilter / ITargetSorter 接口 + 内置实现
│   │   │
│   │   ├── SkillAnimatorController.cs # 动画→技能图桥接层
│   │   ├── IDamageable.cs           # 伤害接口
│   │   └── Projectile.cs            # 投射物
│   │
│   ├── Editor/                      # 编辑器扩展
│   │   ├── ElementLineGraphGenerator.cs
│   │   ├── ElementLineSkillConfigGenerator.cs
│   │   ├── SkillDebugWindow.cs
│   │   ├── SkillGraphValidatorWindow.cs
│   │   └── BreakpointDatabase.cs
│   │
│   ├── Entity/                      # 实体组件
│   │   ├── SkillOwner.cs
│   │   └── TargetDummy.cs
│   │
│   └── VFX/                         # 特效模块
│       ├── VFXManager.cs
│       ├── VFXObjectPool.cs
│       └── ElementLineVFXProfile.cs
│
├── Resources/
│   ├── Config/                      # CSV 配置（单一数据源）
│   │   ├── Skill.csv
│   │   ├── SkillRecipe.csv
│   │   ├── NodePreset.csv
│   │   ├── Buff.csv
│   │   ├── Effect.csv
│   │   └── AITree.csv
│   ├── SkillGraphs/                 # 技能图资产
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
    └── AIModuleDocumentation.md     # AI 模块说明
```

---

## 3. 核心系统速览

### 3.1 技能图（SkillGraph）

- 基于 **CanvasCore Graph**，22+ 节点类型
- **Tick 驱动**：`public abstract NodeTickResult Tick(SkillContext ctx, float deltaTime)` — 0 GC，无 IEnumerator 装箱
- 释放管线：`Idle → PreCast → Executing → PostCast → Idle`（可打断）
- 子图复用：`SubGraphNode` 引用共享 `Common_*` 资产

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

### 3.6 AI 感知（高性能）

- **SpatialHashGrid**：O(1) 范围查询，替代 Physics.OverlapSphere
- **Burst Job 并行**：`AISensorJobSystem` + `FOVDistanceJob`，FOV + 距离检测跑在多线程
- 数据流：`SpatialHashGrid` 导出 `NativeArray<AIEntityNativeData>` → Job 并行计算 → 主线程回读

---

## 4. 关键设计决策

| 原则 | 说明 |
|------|------|
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
| **协程禁止** | 全部节点必须实现 `Tick()` 返回 `NodeTickResult`，不得使用 `IEnumerator` |
| **新增 Node** | 必须同时在 `SkillNode.cs` 的 switch 中添加类别/颜色注册 |
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

---

> **核心宗旨**：这是一条 **战斗系统生产流水线** —— 策划填表（CSV），设计师连线（CanvasCore），程序守护管道（Runtime / GE / EQS / AI）。四者各司其职，系统才能规模化和可维护。
