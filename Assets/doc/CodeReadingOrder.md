# 代码阅读顺序指南

**CSV 数据驱动 + 自建技能图框架 + NodeCanvas AI 行为树 + Tick 驱动执行 + GAS 状态系统 + 数据驱动 UI + 全局音频 + 存档持久化 + 高性能表现**

---

## 阅读原则

| 原则 | 说明 |
|------|------|
| **先接口后实现** | 每个模块先看接口/基类（`ISkillNodeLogic`、`IDamageable`、`ISaveable`、`IStatusReceiver`），再看具体实现 |
| **跟随数据流** | 核心数据流：`CSV → ConfigLoader → SkillNode → EffectSystem/DamagePipeline → AttributeSet → BindableProperty → UI` |
| **理解两层独立** | 技能图框架（自建）与 AI 行为树（NodeCanvas）完全独立，不混淆 |
| **理解执行权分离** | `ISkillNodeLogic.Tick()`（纯逻辑，0框架依赖）与 `SkillNodeBase`（图导航，自建框架）是两条路径 |
| **牢记三大准则** | 零GC（struct/池化/Tick驱动）、数据驱动（CSV单一数据源）、模块解耦（事件总线/接口契约/黑板通信） |

---

## 第一阶段：基础设施层（Core）

> 理解项目的三大设计准则：零GC、数据驱动、模块解耦

### 1. 数据绑定（Binding）

| 文件 | 理解目标 |
|------|----------|
| `Core/Binding/BindableProperty.cs` | 响应式属性核心：泛型包装，赋值时自动比对新旧值、不同则触发 `OnChanged` 委托链 |
| `Core/Binding/FloatBinding.cs` | 浮点数绑定快捷别名 |
| `Core/Binding/IntBinding.cs` | 整数绑定快捷别名 |
| `Core/Binding/BoolBinding.cs` | 布尔绑定快捷别名 |
| `Core/Binding/StringBinding.cs` | 字符串绑定快捷别名 |
| `Core/Binding/BindablePropertyExtensions.cs` | 扩展方法：`AddDelta` / `SetClamped` 等 |

**核心概念**：`BindableProperty<T>` 是整个 UI 数据驱动的基础，AttributeSet 的属性变更通过它通知 UI，UI 组件绝不轮询。

### 2. 事件总线（EventBus）

| 文件 | 理解目标 |
|------|----------|
| `Core/EventBus/GlobalEventBus.cs` | 泛型 `Subscribe<T>/Publish<T>` 模型，类型即 Key，0 GC |
| `Core/EventBus/GameEvents.cs` | 14 种业务事件定义：`EntityDeathEvent`、`ReactionTriggeredEvent`、`ItemAcquiredEvent` 等 |

**核心概念**：事件数据必须是 `struct`，按值传递无堆分配。模块间通信走事件总线，不直接依赖组件。异常隔离：单个订阅者异常不中断后续订阅者调用。

### 3. 标签系统（Tags）

| 文件 | 理解目标 |
|------|----------|
| `Core/Tags/` | GameplayTag 分层级匹配（`"tag.status.burn"` 被 `"tag.status"` 命中） |

**核心概念**：Tag 驱动逻辑，状态判定使用 `GEHost.HasTag()`，不在节点中写 switch-case。

### 4. 配置加载（Data）

| 文件 | 理解目标 |
|------|----------|
| `Core/Data/ConfigLoader.cs` | CSV 数据驱动的单一数据源入口，加载所有 CSV 并缓存 |
| `Core/Data/SkillConfig.cs` → `SkillConfigData.cs` | 技能配置映射 |
| `Core/Data/EffectConfig.cs` | 效果配置映射 |
| `Core/Data/BuffConfig.cs` | Buff 配置映射 |
| `Core/Data/ReactionConfig.cs` | 反应规则配置映射 |
| `Core/Data/TerrainConfig.cs` | 地形配置映射 |
| `Core/Data/VFXArtProfileConfig.cs` | VFX 美术配置映射 |

**核心概念**：所有运行时数值从 CSV 获取，节点内不硬编码。

### 5. 存档系统（Save）

| 文件 | 理解目标 |
|------|----------|
| `Core/Save/` | `ISaveable` 契约接口 + `SaveManager`（AES-128 加密 + 安全写入 + 备份恢复） |

**核心概念**：新模块实现 `ISaveable` + 挂载到 GameObject 即可自动接入，零改动 SaveManager。

---

## 第二阶段：GAS 效果系统层（GAS）

> 理解"状态驱动"的核心机制，这是战斗系统的数值引擎

### 6. GE 核心（GAS/Core）

| 文件 | 理解目标 |
|------|----------|
| `GAS/Core/EffectContext.cs` | 效果上下文：来源/目标/标签等元数据 |
| `GAS/Core/EffectSpec.cs` | 效果运行时实例：将静态数据实例化为可执行单元 |
| `GAS/Core/GameplayEffectData.cs` | GE 静态数据定义：Modifier 类型/幅度/标签/持续时间等 |

**核心概念**：`GEConfig`（CSV 行）→ `GameplayEffectInstance`（运行时实例）→ `GEHost`（组件）。

### 7. 效果系统（GAS/Core）

| 文件 | 理解目标 |
|------|----------|
| `GAS/Core/EffectSystem.cs` | GE 的应用/移除/叠加逻辑 |
| `GAS/Core/GameplayEffectSystem.cs` | 全局 GE 管理器，Modifier 队列：Add → Override → Multiply |
| `GAS/Core/SaveableAttributeSet.cs` | 属性集存档适配器，BindableFloat 响应式属性 |

**核心概念**：Modifier 队列按 Add→Override→Multiply 顺序计算属性最终值。

### 8. 属性与伤害接口（GAS/Attribute）

| 文件 | 理解目标 |
|------|----------|
| `GAS/Attribute/IDamageable.cs` | 伤害接收接口 |
| `GAS/Attribute/HealthComponent.cs` | 生命值组件，实现 IDamageable |

### 9. 伤害管线（GAS/Pipeline）

| 文件 | 理解目标 |
|------|----------|
| `GAS/Pipeline/DamagePipeline.cs` | 事件拦截式伤害结算，`OnPreCalculate` 修改器链 + TagDamageRule 规则注册 |

**核心概念**：伤害通过 `DamagePipeline.CalculateAndApply()` 结算，不在节点中直调 `IDamageable.TakeDamage()`。支持 `RegisterTagRule()` 注册标签伤害规则。

### 10. 反应引擎（GAS/Pipeline）

| 文件 | 理解目标 |
|------|----------|
| `GAS/Pipeline/ReactionEngine.cs` | 元素反应规则（融化/蒸发/超载/感电/脆弱），CSV 配置驱动 |

### 11. 状态/标签/地形（GAS/Status + Event + Terrain）

| 文件 | 理解目标 |
|------|----------|
| `GAS/Status/StatusType.cs` → `StatusRuntime.cs` → `IStatusReceiver.cs` | 状态类型枚举、运行时状态实例、状态接收接口 |
| `GAS/Event/TagEventBus.cs` | 标签事件广播：Tag 变更时通知订阅者 |
| `GAS/Terrain/TerrainEffectSystem.cs` → `TerrainRuntime.cs` | 地形效果系统 |

---

## 第三阶段：技能图框架层（SkillSystem/Graph + Runtime）

> 理解"自建图框架"的设计，这是技能系统的骨架

### 12. 黑板系统（SkillSystem/Runtime）

| 文件 | 理解目标 |
|------|----------|
| `SkillSystem/Runtime/Blackboard.cs` | 节点间通信枢纽：`SetValue`/`GetValue`，全类型字典 |
| `SkillSystem/Runtime/BBKey.cs` | 黑板键常量定义，禁止裸字符串 |
| `SkillSystem/Runtime/BBKeyRef.cs` | 声明式黑板键引用，5维度复用的上下文级机制 |

**核心概念**：节点间不直接传参，全部通过黑板通信。BBKey 必须使用 `const string`，BBKeyRef 实现子图映射桥接。

### 13. 图资产（SkillSystem/Graph）

| 文件 | 理解目标 |
|------|----------|
| `SkillSystem/Graph/SkillGraphAsset.cs` | 自建图框架的 ScriptableObject 资产，无第三方依赖 |
| `SkillSystem/Graph/SkillEdge.cs` | 自建边定义，替代 NodeCanvas Connection |

### 14. 节点基类（SkillSystem/Graph）

| 文件 | 理解目标 |
|------|----------|
| `SkillSystem/Graph/SkillNodeBase.cs` | 技能节点完整生命周期：Enter→Tick→Exit，图导航逻辑 |

### 15. 节点逻辑接口（SkillSystem/Runtime）

| 文件 | 理解目标 |
|------|----------|
| `SkillSystem/Runtime/ISkillNodeLogic.cs` | 执行权分离的核心接口：`Tick(SkillContext, float)` → `NodeTickResult` |
| `SkillSystem/Runtime/NodeTickResult.cs` | Tick 结果枚举：Running / Success / Failure |

**核心概念**：`ISkillNodeLogic` 是纯逻辑接口（0框架依赖），`SkillNodeBase` 负责图导航（自建框架），两者解耦。

### 16. 执行引擎（SkillSystem/Runtime）

| 文件 | 理解目标 |
|------|----------|
| `SkillSystem/Runtime/SkillExecution.cs` | 技能执行实例：通过 ISkillNodeLogic 驱动逻辑 |
| `SkillSystem/Runtime/SkillTickManager.cs` | Tick 统一调度器：0 GC 驱动 |
| `SkillSystem/Runtime/SkillRunner.cs` | 技能运行器 |

### 17. 释放管线（SkillSystem/Pipeline）

| 文件 | 理解目标 |
|------|----------|
| `SkillSystem/Pipeline/SkillCaster.cs` | 释放状态机：Idle→PreCast→Executing→PostCast→Idle（可打断） |
| `SkillSystem/Pipeline/SkillContext.cs` | 释放上下文：施法者/目标/黑板引用 |
| `SkillSystem/Pipeline/SkillTimeline.cs` | 时间轴管线 |

### 18. 编译时间轴（SkillSystem/Runtime）

| 文件 | 理解目标 |
|------|----------|
| `SkillSystem/Runtime/SkillData.cs` | 编译后数据：含时间轴 `SkillStep[]` |
| `SkillSystem/Runtime/SkillEffectData.cs` | 编译后效果数据 |
| `SkillSystem/Runtime/TimelineSkillRunner.cs` | 纯数据驱动时间轴执行器，0 GC |
| `SkillSystem/Runtime/SkillStep.cs` | 时间轴步进定义 |
| `SkillSystem/Runtime/SkillExecutionFrame.cs` | 执行帧状态 |

**核心概念**：混合编译架构 — `SkillGraphAsset`（编辑期描述）→ `SkillBuilder.Compile()` → `SkillData`（含时间轴）→ `TimelineSkillRunner` 纯数据驱动执行。编译模式分 FullTimeline / Hybrid / FallbackOnly。

### 19. 预设与配方（SkillSystem/Runtime）

| 文件 | 理解目标 |
|------|----------|
| `SkillSystem/Runtime/GraphPreset.cs` | 拓扑级复用：GraphPreset + SkillRecipe + ElementLineGraphGenerator |
| `SkillSystem/Runtime/DebugRecorder.cs` | 调试记录器 |
| `SkillSystem/Runtime/SkillSandboxController.cs` | 技能沙盒控制器 |

---

## 第四阶段：技能节点实现（SkillSystem/Nodes）

> 理解各类节点的具体实现，按功能域分组阅读

### 20. 流程控制节点（Nodes/Flow）

| 文件 | 理解目标 |
|------|----------|
| `StartNode` / `EndNode` | 图的入口与出口 |
| `DelayNode` | 延时等待（简单场景），优先使用 AnimationEventWaitNode |
| `ConditionNode` | 条件分支（标记为 IsDynamic，触发动态回退） |
| `ParallelNode` | 并行执行 |
| `SubGraphNode` | 子图复用：引用共享 `Common_*` 资产 + BBMapping 桥接 |

### 21. 释放管线节点（Nodes/Casting）

| 文件 | 理解目标 |
|------|----------|
| `PreCastNode` / `ChannelNode` / `PostCastNode` | 释放状态机的节点映射 |

### 22. 动画同步节点（Nodes/Animation）

| 文件 | 理解目标 |
|------|----------|
| `AnimationEventWaitNode` | 等待动画事件推进（替代 DelayNode），黑板通信：`BBKey.AnimEvent` / `AnimOnHit` / `AnimOnCastEnd` |

### 23. 战斗结算节点（Nodes/Combat）

| 文件 | 理解目标 |
|------|----------|
| `DamageNode` / `ApplyStatusNode` / `ReactionNode` | 伤害→DamagePipeline→GE 的完整链路 |

### 24. GAS 应用节点（Nodes/GAS）

| 文件 | 理解目标 |
|------|----------|
| `ApplyEffectNode` | 技能节点触发 GE 系统，携带标签→管线自动处理 |

### 25. 视觉表现节点（Nodes/VFX）

| 文件 | 理解目标 |
|------|----------|
| `PlayVFXNode` / `ProjectileNode` / `PaintTerrainNode` / `PlaySFXNode` | 表现层调用方式（全走对象池） |

### 26. 工具节点（Nodes/Utility）

| 文件 | 理解目标 |
|------|----------|
| `LogNode` / `SetValueNode` / `ModifyFloatNode` / `RollChanceNode` | 黑板写入、概率分支等工具 |

---

## 第五阶段：AI 行为树（AI）

> 理解 AI 与技能系统的独立解耦设计

### 27. AI 核心（AI/Core）

| 文件 | 理解目标 |
|------|----------|
| `AI/Core/AIController.cs` | 完整 NodeCanvas BT 控制器（Elite/Boss Tier） |
| `AI/Core/MinionBrain.cs` | 轻量级 FSM（Minion Tier，0 序列化开销） |

**核心概念**：三层 Tier 策略 — Minion(FSM) / Elite(BT) / Tower(专用BT)。

### 28. AI 黑板（AI/Core）

| 文件 | 理解目标 |
|------|----------|
| `AI/Core/AIBlackboard.cs` | AI 决策上下文，与技能系统黑板独立 |

### 29. AI 节点基类（AI/Core）

| 文件 | 理解目标 |
|------|----------|
| `AI/Core/AIActionNode.cs` | NodeCanvas 行为树 Action 基类适配 |
| `AI/Core/AIConditionNode.cs` | NodeCanvas 行为树 Condition 基类适配 |

### 30. AI 感知系统（AI/Perception + AI/Core）

| 文件 | 理解目标 |
|------|----------|
| `AI/Perception/` | SpatialHashGrid：O(1) 范围查询，替代 Physics.OverlapSphere |
| `AI/Core/AISensorJobSystem.cs` | Burst Job 并行 FOV + 距离检测，0 主线程开销 |

**核心概念**：`SpatialHashGrid` 导出 `NativeArray<AIEntityNativeData>` → Job 并行计算 → 主线程回读。

### 31. 行为节点（AI/Actions）

| 文件 | 理解目标 |
|------|----------|
| 8 个 Action 实现 | 具体 AI 行为（攻击/巡逻/追击/逃跑/使用技能等） |

### 32. 结构节点（AI/Conditions + Composites + Decorators）

| 文件 | 理解目标 |
|------|----------|
| `AI/Conditions/` | 条件节点 |
| `AI/Composites/` | 组合节点（选择/并行） |
| `AI/Decorators/` | 装饰器节点 |

### 33. 数据驱动生成（AI/Data + AI/Editor + AI/Graph）

| 文件 | 理解目标 |
|------|----------|
| `AI/Data/AITreeConfig.cs` | AI 行为树 CSV 配置映射 |
| `AI/Editor/` | AIGraphMenu、AITreeGenerator：CSV→自动生成→AIGraph 资产 |
| `AI/Graph/` | AIGraph 资产定义 |

---

## 第六阶段：周边系统

> 理解业务模块如何接入核心框架

### 34. EQS 环境查询系统（EQS）

| 文件 | 理解目标 |
|------|----------|
| `EQS/Core/` | 可插拔 `ITargetFilter` + `ITargetSorter`，类 Unreal EQS |

**内置实现**：DistanceFilter、TagFilter、HPThresholdFilter、TeamFilter / ByDistanceSorter、ByHPSorter、ByThreatSorter。`TargetQueryConfig.Execute()` — 空间哈希范围查询 → 过滤链 → 排序 → Top-N。

### 35. 实体组件（Entity）

| 文件 | 理解目标 |
|------|----------|
| `Entity/SkillOwner.cs` | 技能持有者组件 |
| `Entity/TargetDummy.cs` | 测试靶组件 |

### 36. 表现层（Presentation）

| 子目录 | 理解目标 |
|--------|----------|
| `Presentation/VFX/` | VFXManager + ObjectPool + 各特效实现（ArcBeam/Beam/Bulwark...） |
| `Presentation/Audio/` | AudioManager + AudioConfig + AudioDispatcher，对象池强制 |
| `Presentation/UI/` | UIManager（5层级栈式面板）+ UIWindowBase + HealthBarUI |
| `Presentation/Animation/` | SkillAnimatorController：播放动画 + 绑定 SkillContext |
| `Presentation/Projectiles/` | Projectile（飞行/命中/回收） |

**核心概念**：表现层只做展示，无业务逻辑。UI 组件只监听 BindableProperty 回调，绝不在 Update 中轮询。

### 37. 物品与装备系统（Inventory）

| 文件 | 理解目标 |
|------|----------|
| `Inventory/ItemConfig.cs` | 物品配置数据类 + ItemType/EquipmentSlot 枚举 |
| `Inventory/InventoryComponent.cs` | 背包核心组件（堆叠/交换/消耗） |
| `Inventory/EquipmentComponent.cs` | 装备组件（穿戴=ApplyEffect 永久 GE，卸下=RemoveEffect） |
| `Inventory/SaveableInventory.cs` | 背包容档适配器 |

### 38. 任务与剧情图（Quest）

| 文件 | 理解目标 |
|------|----------|
| `Quest/LogicGraphAsset.cs` | 泛化逻辑图基类 |
| `Quest/LogicNodeBase.cs` | 泛化逻辑节点基类 |
| `Quest/QuestNodeBase.cs` | 任务节点基类 + QuestNodeResult 枚举 |
| `Quest/QuestGraphAsset.cs` | 任务图资产 + QuestState 枚举 |
| `Quest/QuestRunner.cs` | 任务执行引擎（低频 Tick 0.5s 间隔） |
| `Quest/Nodes/` | 5 类专用节点：QuestStart / EventWait / ConditionCheck / Reward / Dialogue |
| `Quest/SaveableQuestSystem.cs` | 任务存档适配器 |

---

## 第七阶段：系统初始化与QA验证

> 理解全链路如何串通、如何验证

### 39. 启动引导

| 文件 | 理解目标 |
|------|----------|
| `Core/GameSystemBootstrapper.cs` | 自动初始化链：ConfigLoader(BeforeSceneLoad) → ReactionEngine(BeforeSceneLoad) → TagDamageRules(AfterSceneLoad) |

### 40. QA 测试模块

| 文件 | 理解目标 |
|------|----------|
| `QA/SkillTestSceneSetup.cs` | 技能测试场景搭建 |
| `QA/QAGalleryTestController.cs` | 画廊测试：52 技能连续释放验证 |
| `QA/QAReactionMatrix.cs` | 反应矩阵：元素组合全覆盖测试 |
| `QA/QATargetDummy.cs` | 测试靶：伤害接收与死亡事件 |
| `QA/QAPerformanceMonitor.cs` | 性能监控：帧时间/内存/GC 分配 |
| `QA/QADeadlockDetector.cs` | 死锁检测：技能执行超时诊断 |
| `QA/QAEQSDebugger.cs` | EQS 调试：目标查询可视化 |
| `QA/QAAITacticsSandbox.cs` | AI 沙盒：行为策略验证 |
| `QA/QAFloatingText.cs` | 浮动文字（归口到 UIManager HUD 层） |

---

## 第八阶段：编辑器工具与文档

### 41. 技能图编辑器（SkillSystem/Editor）

| 理解目标 |
|----------|
| DebugWindow：运行时节点状态可视化 |
| Validator：图结构校验（孤立节点/断边/类型不匹配） |
| Breakpoint：节点断点调试 |

### 42. AI 编辑器（AI/Editor）

| 理解目标 |
|----------|
| AIGraphMenu：AI 图菜单扩展 |
| AITreeGenerator：CSV → 行为树资产自动生成 |

### 43. 全局编辑器（Editor）

| 理解目标 |
|----------|
| `SkillEntityInstaller.cs`：技能实体自动安装 |
| `URPMaterialFixer.cs`：URP 材质修复工具 |

### 44. 设计文档

| 文档 | 内容 |
|------|------|
| `doc/SkillDesignGuide.md` | 全面设计说明书 — 节点目录、BBKey 参考、CSV 字段、释放管线 |
| `doc/NodeAuthoringWorkbook.md` | 节点搭建手册 — 预设→图流程、绑定规范、通信模式 |
| `doc/ElementLineSkillCatalog.md` | 《元素阵线》技能总表 — 52 配方 × 4 元素 × 12 预设 |
| `doc/AIModuleDocumentation.md` | AI 行为树模块说明 — CSV 驱动生成、Action/Condition 节点、感知系统 |
| `doc/QAModuleDocumentation.md` | QA 测试模块说明 — 画廊/压力测试、反应矩阵、性能监控、死锁检测、EQS 调试、AI 沙盒 |

---

## 附录：核心数据流全景图

```text
┌─────────────────────────────────────────────────────────────────────┐
│                        CSV 配置层                                   │
│  Skill.csv ─┐                                                      │
│  SkillRecipe.csv ─┤                                                 │
│  GameplayEffect.csv ─┤  ──→  ConfigLoader  ──→  各 Config 类        │
│  Buff.csv ─┤       │                                              │
│  AITree.csv ─┘                                                      │
└────────────────────────────────┬────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     自建技能图层                                     │
│  SkillGraphAsset ──→ SkillNodeBase ──→ ISkillNodeLogic.Tick()       │
│       │                           │                                  │
│       ▼                           ▼                                  │
│  SubGraphNode              Blackboard(BBKey/BBKeyRef)               │
│  GraphPreset               FloatBinding/IntBinding                  │
└────────────────────────────────┬────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     Tick 运行时引擎                                  │
│  SkillCaster ──→ SkillExecution ──→ SkillTickManager                │
│       │                                              │               │
│       ▼                                              ▼               │
│  SkillContext                              TimelineSkillRunner       │
│  (Idle→PreCast→Executing→PostCast)         (编译时间轴，0 GC)        │
└────────────────────────────────┬────────────────────────────────────┘
                                 │
                    ┌────────────┼────────────┐
                    ▼            ▼            ▼
┌───────────────┐ ┌──────────────┐ ┌──────────────────┐
│  GAS 效果层   │ │  伤害管线    │ │  表现层           │
│  EffectSystem │ │  DamagePipe  │ │  VFXManager(池)  │
│  GEHost       │ │  line        │ │  AudioManager(池)│
│  AttributeSet │ │  Reaction    │ │  UI(Binding驱动) │
│  TagEventBus  │ │  Engine      │ │  Projectile      │
└───────┬───────┘ └──────┬───────┘ └──────────────────┘
        │                │
        ▼                ▼
┌─────────────────────────────────────────────────────────────────────┐
│  BindableProperty ──→ UI 自动刷新    │  GlobalEventBus ──→ 模块通信  │
│  ISaveable ──→ SaveManager 持久化    │  Tag 驱动逻辑              │
└─────────────────────────────────────────────────────────────────────┘
```

---

> **核心宗旨**：这是一条 **战斗系统生产流水线** —— 策划填表（CSV），设计师连线（自建技能图框架），程序守护管道（Runtime / GE / EQS / AI）。四者各司其职，系统才能规模化和可维护。
