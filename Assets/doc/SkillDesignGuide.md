# 技能系统设计说明书

## 一、架构概览

本项目是一个**商业级战斗系统框架**，支撑策划填表（CSV）、设计师连线（自建技能图框架）、程序守护（Runtime / GE / EQS / AI）的规模化协作。

技能系统使用自建图框架（SkillGraphAsset + SkillNodeBase + SkillEdge），完全独立于 NodeCanvas。AI 系统继续使用 NodeCanvas BehaviourTree，两者无耦合。

### 六层架构

```
┌──────────────────────────────────────────────────┐
│  第一层 — CSV 数据层（策划维护）                    │
│  Skill.csv / SkillRecipe.csv / Buff.csv / AITree.csv
│  → 单一数据源，所有数值参数的唯一出处               │
├──────────────────────────────────────────────────┤
│  第二层 — 自建技能图层（无第三方依赖）              │
│  SkillGraphAsset + SkillNodeBase + SkillEdge      │
│  → 技能图容器、节点基类、有向边，完全自建           │
├──────────────────────────────────────────────────┤
│  第三层 — NodeCanvas AI 层（行为树）               │
│  AIGraph（BehaviourTree）+ AIController            │
│  → AI 行为树使用 NodeCanvas，独立于技能系统        │
├──────────────────────────────────────────────────┤
│  第四层 — GameplayEffect 层（状态驱动）             │
│  GEConfig → GEInstance → GEHost + GameplayTag     │
│  → Modifier 队列 + Tag 验证 + 事件拦截             │
├──────────────────────────────────────────────────┤
│  第五层 — Tick 运行时引擎                          │
│  SkillCaster / SkillTickManager / SkillRunner     │
│  TimelineSkillRunner（混合架构时间轴执行器）        │
│  → 状态机驱动释放管线，Blackboard 解耦节点间通信     │
├──────────────────────────────────────────────────┤
│  第六层 — 表现 & AI 感知层                         │
│  VFXManager / Shader / SpatialHashGrid / Burst Job│
│  → 对象池管理特效，空间哈希 O(1) 感知               │
└──────────────────────────────────────────────────┘
```

### 核心设计原则

| # | 原则 | 说明 |
|---|------|------|
| 1 | **单一数据源** | CSV 是唯一数值来源，节点通过 `ConfigLoader.GetSkillConfig(id)` 获取参数 |
| 2 | **Tick 驱动，0 GC** | 所有节点返回 `NodeTickResult`，由 `SkillTickManager` 统一调度，禁止 `IEnumerator` |
| 3 | **事件拦截，非硬编码** | 伤害通过 `DamagePipeline.CalculateAndApply()`，GE 通过事件拦截修改 |
| 4 | **Tag 驱动逻辑** | 状态判定使用 `GEHost.HasTag()`，不在节点中写业务判断 |
| 5 | **可插拔 Filter/Sorter** | 目标筛选使用 `ITargetFilter` + `ITargetSorter` 管道 |
| 6 | **动画替代延时** | 使用 `AnimationEventWaitNode` 等待动画事件，不使用 `DelayNode` 对齐帧 |
| 7 | **对象池强制** | 所有 VFX 通过 `VFXObjectPool.Get/Release`，禁止 `Instantiate/Destroy` |

---

## 二、技能释放管线

技能从触发到完成的完整生命周期由 `SkillCaster` 统一管理（Tick 驱动）。

### CastStage 状态机

```
TryCast()
  ├─ [校验: 冷却、资源、射程、忙状态]
  ├─ 扣除资源
  ▼
前摇阶段 ─── 可被打断 ───→ 打断清理
  │
  ▼
执行阶段 ─── 可被打断 ───→ 打断清理
  （运行技能图，Tick 逐帧推进）
  │
  ▼
后摇阶段（不可打断）
  │
  ▼
空闲状态（设置冷却）
```

### 各阶段详情

| 阶段 | 驱动方式 | 可打断？ | 写入的 BBKey |
|------|---------|---------|-------------|
| **前摇** | `SkillCaster.TickPipeline()` | ✅ 是 | `IsPreCasting`、`PreCastProgress` |
| **执行** | `SkillTickManager` 逐帧推进节点 | ✅ 是 | `IsChanneling`、`ChannelProgress` 等 |
| **后摇** | `SkillCaster.TickPipeline()` | ❌ 否 | `IsPostCasting`、`PostCastProgress` |
| **打断** | — | — | `IsInterrupted`、`InterruptReason` |

---

## 三、GE / Buff 系统（GameplayEffect）

### 核心类型

| 类型 | 职责 |
|------|------|
| `GEConfig` | CSV 行映射：GEId、Duration、Period、MaxStacks、StackPolicy、Modifiers、Tags |
| `GameplayEffectInstance` | 运行时实例：剩余时间、层数、绑定的目标 |
| `GEHost` | MonoBehaviour 组件：管理活跃 GE 列表、Tag 查询、事件触发 |
| `GEModifier` | 单个属性修改器：Attribute + Operation（Add/Multiply/Override）+ Magnitude |
| `GEModOp` | 操作枚举：Add、Multiply、Override |
| `GEAttribute` | 属性枚举：DamageTakenMultiplier、MoveSpeed、AttackSpeed、DamagePerTick 等 |
| `GEDurationPolicy` | 持续策略：Instant（瞬间）、HasDuration（有持续时间）、Infinite（永久） |
| `GEStackPolicy` | 叠加策略：Refresh（刷新）、Add（叠加层数）、Ignore（忽略） |
| `GameplayTag` | 分层级标签：`"tag.status.burn"` 被 `"tag.status"` 层级匹配 |
| `GameplayTagContainer` | 标签集合：`HasTag()` / `AddTag()` / `RemoveTag()` |
| `AttributeSet` | 基础属性容器：攻击、防御、生命、移速、攻速、暴击 |

### 事件拦截（DamagePipeline）

```
原始伤害
  → OnPreCalculate 事件（全局拦截）
    → host.RaiseGameplayEvent(ctx)（目标 GE 拦截）
      → instigatorHost.RaiseGameplayEvent(ctx)（施法者 GE 拦截）
        → Modifier 队列计算（DamageTakenMultiplier、DamageDealtMultiplier 等）
          → Tag 驱动元素反应加成
            → PostCalculate 事件
              → 施加伤害
```

**示例：Chill 状态下火系伤害翻倍**
```csharp
DamagePipeline.OnPreCalculate += (ctx) =>
{
    var host = ctx.Target.GetComponent<GEHost>();
    if (host != null && host.HasTag("tag.status.chill"))
        ctx.Value *= 1.5f;  // 火系对冰冻 1.5x
};
```

### GE 与 ApplyStatusNode 的协同

`ApplyStatusNode` 将技能系统的状态语义（burn、chill、conductive 等）映射为 GE Modifier：

| 状态 | GE 效果 |
|------|---------|
| burn / poison | `DamagePerTick` Add + Period = 1s（持续伤害） |
| chill / slow | `MoveSpeed` Multiply（减速） |
| freeze / stun / root | `MoveSpeed` / `AttackSpeed` Override = 0（硬控） |
| mark / conductive | `DamageTakenMultiplier` Multiply（易伤） |

---

## 四、EQS 目标查询系统

### 核心接口

```csharp
public interface ITargetFilter
{
    bool Pass(Transform candidate, TargetQueryContext ctx);
}

public interface ITargetSorter
{
    float Score(Transform candidate, TargetQueryContext ctx);
    SortOrder SortDirection { get; }
}
```

### 内置实现

| Filter | 说明 |
|--------|------|
| `DistanceFilter` | 距离范围筛选 |
| `TagFilter` | GE Tag 筛选（可设 RequireAll） |
| `HPThresholdFilter` | 生命百分比阈值（MinHP / MaxHP） |
| `TeamFilter` | 队伍 ID 筛选 |

| Sorter | 说明 |
|--------|------|
| `ByDistanceSorter` | 按距离排序 |
| `ByHPSorter` | 按生命百分比排序 |
| `ByThreatSorter` | 按 AI AlertLevel 排序 |

### 使用示例

```csharp
// 选出范围内血量最低且带灼烧的前3个敌人
var config = TargetQueryConfig.CreateTemplate(range: 15f, maxResults: 3);
config.Filters.Add(new TagFilter { RequiredTags = new[] { "tag.status.burn" } });
config.Filters.Add(new HPThresholdFilter { ExcludeDead = true });
config.Sorters.Add(new ByHPSorter { SortDirection = SortOrder.Ascending });
var targets = config.Execute(center, self, ctx);
```

---

## 五、动画同步系统

### 通信机制

```
AnimationClip Event ( "Skill:OnHit" )
    → AnimationEventReceiver.OnSkillAnimationEvent("Skill:OnHit")
        → SkillAnimatorController.OnAnimationEvent("Skill:OnHit")
            → ctx.Blackboard.SetValue(BBKey.AnimEvent, "OnHit")
                → AnimationEventWaitNode 检测到 "OnHit" → 通过 → 后续节点
```

### 核心类

| 类 | 职责 |
|----|------|
| `SkillAnimatorController` | 播放动画、绑定 SkillContext、攻速同步 |
| `AnimationEventReceiver` | Unity AnimationEvent 入口 |
| `AnimationEventWaitNode` | 替代 DelayNode，等待动画事件推进 |

### AnimationEventWaitNode 参数

| 参数 | 说明 |
|------|------|
| `waitEvent` | 等待的事件名（如 "OnHit"、"OnCastEnd"） |
| `timeout` | 超时（秒），0 = 永不超时 |
| `matchMode` | Exact / Contains |

### 技能图示例（动画驱动）

```
PlayAnimNode  →  AnimationEventWaitNode(waitEvent="OnHit")  →  DamageNode
     │                          │
     │ 播放攻击动画              │ OnHit 动画帧触发，节点通过
     │ 攻速自动对齐              │ 执行伤害计算
```

---

## 六、节点目录（共 24 种）

### 生命周期与流程控制

| 节点 | 说明 | 关键参数 |
|------|------|---------|
| **StartNode** | 图入口点 | — |
| **EndNode** | 图终点 | — |
| **DelayNode** | 等待 `delaySeconds`（不推荐用于动画同步） | `FloatBinding delaySeconds` |
| **AnimationEventWaitNode** | 等待动画事件推进（推荐替代 DelayNode） | `waitEvent`、`timeout`、`matchMode` |
| **ParallelNode** | 并行分支，等待全部完成 | `SkillNode[] branches` |
| **ConditionNode** | if-else 分支 | `ConditionMode`、`threshold` |
| **SubGraphNode** | 执行引用子图 | `SkillGraph subGraph` |

### 释放管线节点

| 节点 | 说明 |
|------|------|
| **PreCastNode** | 播放前摇特效 |
| **ChannelNode** | 逐 tick 引导伤害 |
| **PostCastNode** | 播放后摇特效 |

### 伤害与状态

| 节点 | 说明 |
|------|------|
| **DamageNode** | 通过 `DamagePipeline.CalculateAndApply()` 结算伤害，支持 `extraTags` |
| **ApplyStatusNode** | 通过 `GEHost.ApplyEffect()` 施加 GE 状态 |
| **ReactionNode** | 跨元素反应 |
| **ResonanceNode** | 行/列共鸣 |

### 特效与视觉

| 节点 | 说明 |
|------|------|
| **PlayVFXNode** | 对象池播放 VFX |
| **FinisherStagedNode** | 二段终结技 |
| **ProjectileNode** | 发射投射物 |
| **PaintTerrainNode** | 地形标记施加 |

### 工具类

| 节点 | 说明 |
|------|------|
| **LogNode** | 控制台日志 |
| **ModifyFloatNode** | 浮点运算写入 BB |
| **RollChanceNode** | 概率判定 |
| **SetValueNode** | 字符串写入 BB |

---

## 七、Blackboard 键名参考

### 释放管线

| 键名 | 类型 | 说明 |
|------|------|------|
| `IsPreCasting` / `IsPostCasting` | bool | 前摇/后摇激活中 |
| `IsChanneling` | bool | 引导中 |
| `IsInterrupted` / `InterruptReason` | bool / string | 打断标记与原因 |
| `PreCastProgress` / `PostCastProgress` | float | 进度 0→1 |

### 动画同步

| 键名 | 类型 | 说明 |
|------|------|------|
| `AnimEvent` | string | 当前动画事件名（"OnHit" / "OnCastEnd"） |
| `AnimOnHit` | bool | 命中帧标记（单帧有效） |
| `AnimOnCastEnd` | bool | 动画结束标记 |
| `AnimIsPlaying` | bool | 动画播放中 |
| `AnimNormalizedTime` | float | 标准化时间 0→1 |
| `AnimLastEventTime` | float | 上次事件时间 |

### 伤害与状态

| 键名 | 类型 | 说明 |
|------|------|------|
| `DamageOverride` | float | 覆盖伤害值 |
| `LastDamage` | float | 上次伤害 |
| `IsCrit` | bool | 暴击标记 |
| `StatusTags` | string | 状态标记（竖线分隔） |

---

## 八、CSV 配置参考

### Skill.csv（20 列）

| 字段 | 类型 | 说明 |
|------|------|------|
| `skill_id` | int | 技能唯一标识 |
| `name` | string | 显示名称 |
| `graph_path` | string | 技能图路径 |
| `damage` / `damage_rate` | float | 伤害 / 倍率 |
| `cooldown` / `cast_range` | float | 冷却 / 射程 |
| `crit_chance` | float | 暴击概率 |
| `cast_time` / `channel_duration` / `post_cast_time` | float | 前摇 / 引导 / 后摇 |
| `interruptible` | bool | 可打断 |
| `resource_cost` | float | 资源消耗 |

### Buff.csv

`GEConfig` 的行映射，定义 Buff/Debuff 的 Modifier 和 Tag。

---

## 九、依赖的架构模式

| 模式 | 说明 |
|------|------|
| **Tick 驱动** | 所有节点返回 `NodeTickResult`，无 IEnumerator 协程 |
| **事件拦截** | `DamagePipeline.OnPreCalculate` 事件链，GE 可在结算前修改伤害 |
| **Tag 驱动** | 状态判定使用 `GEHost.HasTag()`，不在节点中硬编码条件 |
| **可插拔 EQS** | `ITargetFilter` + `ITargetSorter` 接口，新增策略不修改核心 |
| **动画驱动** | `AnimationEventWaitNode` + `SkillAnimatorController`，不依赖时间对齐 |

---

## 十、文件地图

按 **架构层次 → 模块功能 → 生命周期** 三级划分后的目录：

```
Assets/
├── Scripts/
│   ├── Core/                  ← 基础设施（Data / Binding / Tags）
│   ├── SkillSystem/           ← 自建技能图框架
│   │   ├── Graph/             ← SkillGraphAsset + SkillNodeBase + SkillEdge
│   │   ├── Runtime/           ← TickManager, Runner, Execution, Blackboard
│   │   ├── Pipeline/          ← SkillCaster, SkillContext, SkillTimeline
│   │   ├── Nodes/             ← 24 种节点（按 Flow→Casting→Animation→Combat→GAS→VFX→Utility 分组）
│   │   └── Editor/            ← 技能图编辑器工具
│   ├── GAS/                   ← GameplayEffect 系统
│   │   ├── Core/              ← GEHost, GameplayEffectSystem, EffectSystem
│   │   ├── Pipeline/          ← DamagePipeline, ReactionEngine
│   │   ├── Attribute/         ← IDamageable, HealthComponent
│   │   ├── Status/            ← StatusType, StatusRuntime
│   │   ├── Event/             ← TagEventBus
│   │   └── Terrain/           ← TerrainEffectSystem
│   ├── EQS/                   ← 目标查询系统
│   │   └── Core/              ← TargetQueryConfig, TargetFilter
│   ├── AI/                    ← AI 行为树（NodeCanvas）
│   │   ├── Core/              ← AIController, MinionBrain
│   │   ├── Perception/        ← SpatialHashGrid, AISensorJobSystem
│   │   ├── Actions/           ← 行为节点
│   │   ├── Conditions/        ← 条件节点
│   │   ├── Composites/        ← 组合节点
│   │   ├── Decorators/        ← 装饰器节点
│   │   ├── Data/              ← AITreeConfig
│   │   ├── Graph/             ← AIGraph
│   │   └── Editor/            ← AI 编辑器工具
│   ├── Presentation/          ← 表现层
│   │   ├── VFX/               ← VFXManager, ObjectPool, 特效实现
│   │   ├── Animation/         ← SkillAnimatorController
│   │   └── Projectiles/       ← Projectile
│   ├── Entity/                ← SkillOwner, TargetDummy
│   ├── QA/                    ← QA 测试模块
│   └── Editor/                ← 全局编辑器工具
│       ├── Generators/        ← 代码/配置生成器
│       ├── Validators/        ← 数据校验器
│       └── Tools/             ← Dashboard 面板
├── Resources/Config/          ← Skill.csv、Buff.csv、AITree.csv 等
├── Resources/SkillData/       ← SkillBuilder 编译产物（SkillData ScriptableObject）
└── doc/                       ← 本文档及其他说明文档
```

---

## 十一、混合编译架构（SkillBuilder → TimelineSkillRunner）

### 11.1 设计目标

在保留现有 **Tick 驱动节点图**（零 GC、高灵活性）的基础上，为**线性技能路径**提供可选的**预编译时间轴**执行路径：

- **编辑期**：`SkillGraphAsset` 作为技能的唯一描述源（策划/设计师维护）
- **编译期**：`SkillBuilder` 遍历图，将叶子节点编译为 `SkillEffectData`，按时间轴排序生成 `SkillStep[]`
- **运行期**：`TimelineSkillRunner` 按时间轴推进，遇到动态节点时回退到现有 Tick 解释器

### 11.2 核心数据结构

#### SkillEffectData（纯数据效果描述）

```csharp
public struct SkillEffectData
{
    public SkillEffectType EffectType;   // Damage / Heal / ApplyBuff / PlayVFX / SetBlackboard / ...
    public SkillEffectTargetMode TargetMode; // Caster / PrimaryTarget / EQSResults
    public float BaseValue;              // 基础值（伤害/治疗/强度）
    public float ValueMultiplier;        // 倍率
    public string BuffKey;               // GE 配置 Key
    public float Duration;               // 持续时间（Buff 或 VFX）
    public int AbilityLevel;             // 技能等级（GE 数值缩放）
    public string VFXKey;                // 特效资源 Key
    public Vector3 VFXDirection;         // 特效方向
    public float VFXScale;               // 特效缩放
    public bool AttachToTarget;          // 是否附着目标
    public string BlackboardKey;         // 黑板 Key
    public string BlackboardValue;       // 黑板 Value（字符串形式）
    // ... 其他扁平化字段
}
```

- **零 GC**：不使用 `Dictionary`，所有参数为扁平字段，可直接序列化到 ScriptableObject
- **工厂方法**：`CreateDamage()`、`CreateVFX()`、`CreateApplyBuff()`、`CreateSetBlackboard()`

#### SkillStep（时间轴执行点）

```csharp
public class SkillStep
{
    public float TriggerTime;            // 触发时间（秒，相对于技能开始）
    public List<SkillEffectData> Effects;// 同一时间点的并行效果
    public bool IsDynamic;               // 是否为运行时动态节点（条件分支等）
    public string SourceNodeGuid;        // 关联的原始图节点 GUID
}
```

#### SkillData（编译产物）

```csharp
public class SkillData : ScriptableObject
{
    public int SkillId;
    public string SkillName;
    public SkillGraphAsset SourceGraph;  // 源图引用（用于变更检测）
    public string SourceGraphHash;       // 图哈希（编译时校验）
    public SkillCompileMode CompileMode; // FullTimeline / Hybrid / FallbackOnly
    public bool HasDynamicNodes;
    public List<SkillStep> Steps;        // 按 TriggerTime 排序的时间轴
    public float TotalDuration;
    public float PreCastTime;
    public float PostCastTime;
}
```

### 11.3 编译流程（SkillBuilder）

```text
SkillGraphAsset
    ↓ GetStartNode()
StartNode → TraverseAndCompile(node, state, result, compileCtx)
    ↓ ResolveNextNode("output")
    ├─ DelayNode        → state.CurrentTime += delayNode.GetTimelineDuration()
    ├─ PreCastNode      → state.PreCastTime = preCast.GetTimelineDuration()
    ├─ PostCastNode     → state.PostCastTime = postCast.GetTimelineDuration()
    ├─ DamageNode       → node.Compile(ctx) → SkillEffectData.CreateDamage(...) → PendingStep
    ├─ PlayVFXNode      → node.Compile(ctx) → SkillEffectData.CreateVFX(...) → PendingStep
    ├─ ApplyEffectNode  → node.Compile(ctx) → SkillEffectData.CreateApplyBuff(...) → PendingStep
    ├─ SetValueNode     → node.Compile(ctx) → SkillEffectData.CreateSetBlackboard(...) → PendingStep
    ├─ ConditionNode    → 多输出分支 → 标记 IsDynamic + 继续遍历各分支（分析用）
    └─ ...
```

**编译模式判定**：
- `DynamicNodeCount == 0 && CompiledNodeCount > 0` → `FullTimeline`
- `DynamicNodeCount > 0 && CompiledNodeCount > 0` → `Hybrid`
- `CompiledNodeCount == 0` → `FallbackOnly`

**编辑器入口**：`Assets/Skill System/Compile Selected Graph`（右键 SkillGraphAsset）

### 11.4 执行流程（TimelineSkillRunner）

```csharp
public class TimelineSkillRunner
{
    public void Start(SkillData skillData, SkillContext context);
    public void Tick(float deltaTime);   // 0 GC，纯数据比较
    public void ResumeAfterDynamic();    // 动态节点处理完成后恢复
}
```

**状态机**：`Idle → Running → [Paused / WaitingForDynamic] → Completed / Interrupted`

**Tick 推进逻辑**：
1. `_elapsedTime += deltaTime`
2. `while (true)` 查找下一个未执行的 `SkillStep`
3. 若 `step.IsDynamic` → 触发 `OnDynamicStep` 事件 → 进入 `WaitingForDynamic`
4. 否则 `ExecuteStep(step)` → 遍历 `Effects` → 按 `EffectType` 分发给各 Dispatcher

### 11.5 Effect 分发器

| Dispatcher | 处理类型 | 映射目标 |
|------------|---------|---------|
| `EffectSystemDispatcher` | Damage / Heal / ApplyBuff / RemoveBuff / ModifyAttribute | `DamagePipeline.CalculateAndApply()` / `EffectSystem.ApplyEffect()` |
| `PresentationDispatcher` | PlayVFX / PlaySFX / SpawnProjectile / ShakeCamera | `VFXManager.Play()` / Projectile 系统 |
| `AnimationDispatcher` | PlayAnimation | `Animator.SetTrigger()` / `CrossFade()` |
| `EQSQueryDispatcher` | EQSQuery | EQS 查询系统 |
| `TerrainDispatcher` | PaintTerrain | TerrainEffectSystem |
| `CustomEffectDispatcher` | Custom | 外部扩展事件 |

### 11.6 动态回退机制

当时间轴遇到**不可编译节点**（ConditionNode、RollChanceNode、AnimationEventWaitNode 等）时：

1. `TimelineSkillRunner` 暂停时间轴，触发 `OnDynamicStep(step, resumeTime)`
2. 外部处理器（如现有 `SkillTickManager`）接管，使用原始 `SkillGraphAsset` 从该节点继续 Tick 执行
3. 动态段执行完毕后，调用 `ResumeAfterDynamic()`
4. `TimelineSkillRunner` 从 `resumeTime` 继续推进后续时间轴

**优势**：
- 线性路径（80% 常见技能）享受 **0 GC 时间轴执行**
- 复杂分支路径（20% 特殊技能）自动回退到 **Tick 解释器**，无需重写逻辑

### 11.7 节点编译接口

所有叶子节点已实现 `SkillNodeBase` 新增的编译虚方法：

```csharp
public abstract class SkillNodeBase : ScriptableObject
{
    public virtual bool CanCompile => false;                    // 是否可被编译
    public virtual List<SkillEffectData> Compile(SkillContext ctx = null) { return null; }
    public virtual float GetTimelineDuration() { return 0f; }   // 对时间轴的时间贡献
}
```

**已支持编译的节点**：

| 节点 | CanCompile | Compile 产物 | GetTimelineDuration |
|------|-----------|-------------|---------------------|
| **DamageNode** | `true` | `SkillEffectData.CreateDamage(rawDamage)` + `TagsToApply` | `0` |
| **PlayVFXNode** | `true` | `SkillEffectData.CreateVFX(...)` 含方向/长度/宽度/持续时间 | `0` |
| **ApplyEffectNode** | `true` | `SkillEffectData.CreateApplyBuff(...)` 含 AbilityLevel + Tags | `0` |
| **SetValueNode** | `true` | `SkillEffectData.CreateSetBlackboard(key, value)` | `0` |
| **DelayNode** | `false`（流程节点） | — | `delaySeconds.Resolve(null)` |
| **PreCastNode** | `false`（流程节点） | — | `castTime.Resolve(null)` |
| **PostCastNode** | `false`（流程节点） | — | `postCastTime.Resolve(null)` |

### 11.8 使用示例

**编译技能图**：

```csharp
// 编辑器中选中 SkillGraphAsset 右键编译
// 或代码中调用：
var result = SkillBuilder.Build(graph, skillId: 1001, skillName: "Fireball");
if (result.Success)
{
    SkillBuilder.BuildAndSave(graph, "Assets/Resources/SkillData/Fireball_Data.asset", 1001);
}
```

**运行时时间轴执行**：

```csharp
var skillData = Resources.Load<SkillData>("SkillData/Fireball_Data");
var runner = new TimelineSkillRunner();
runner.OnDynamicStep += (step, resumeTime) =>
{
    // 使用原始 Tick 解释器处理动态段
    tickManager.ExecuteDynamicStep(step.SourceNodeGuid, context);
    runner.ResumeAfterDynamic();
};
runner.Start(skillData, context);

// 每帧调用
runner.Tick(Time.deltaTime);
```

### 11.9 新增/修改文件清单

| 文件 | 类型 | 说明 |
|------|------|------|
| `SkillSystem/Runtime/SkillEffectData.cs` | 新增 | 纯数据效果描述结构 |
| `SkillSystem/Runtime/SkillStep.cs` | 新增 | 时间轴执行点 |
| `SkillSystem/Runtime/SkillData.cs` | 新增 | 编译产物 ScriptableObject |
| `SkillSystem/Editor/SkillBuilder.cs` | 新增 | Graph → SkillData 编译器 |
| `SkillSystem/Runtime/TimelineSkillRunner.cs` | 新增 | 时间轴执行器 + Effect 分发器 |
| `SkillSystem/Graph/SkillNodeBase.cs` | 修改 | 新增 `CanCompile`、`Compile()`、`GetTimelineDuration()` |
| `SkillSystem/Nodes/Combat/DamageNode.cs` | 修改 | 新增 `CanCompile = true` + `Compile()` |
| `SkillSystem/Nodes/VFX/PlayVFXNode.cs` | 修改 | 新增 `CanCompile = true` + `Compile()` |
| `SkillSystem/Nodes/GAS/ApplyEffectNode.cs` | 修改 | 新增 `CanCompile = true` + `Compile()` |
| `SkillSystem/Nodes/Utility/SetValueNode.cs` | 修改 | 新增 `CanCompile = true` + `Compile()` |
| `SkillSystem/Nodes/Flow/DelayNode.cs` | 修改 | 新增 `GetTimelineDuration()` |
| `SkillSystem/Nodes/Casting/PreCastNode.cs` | 修改 | 新增 `GetTimelineDuration()` |
| `SkillSystem/Nodes/Casting/PostCastNode.cs` | 修改 | 新增 `GetTimelineDuration()` |
| `SkillSystem/Nodes/VFX/PlaySFXNode.cs` | 新增 | 音效播放节点，`CanCompile = true`，`Compile()` 输出 `SkillEffectType.PlaySFX` |
| `Presentation/Audio/AudioDispatcher.cs` | 新增 | 技能时间轴音频分发器，与 PresentationDispatcher 并列 |
| `Presentation/Audio/AudioManager.cs` | 新增 | 全局音频管理器，对象池 + BGM 淡入淡出 + 同频剔除 |
| `Presentation/Audio/AudioConfig.cs` | 新增 | 音频配置数据类 + AudioCategory 枚举 |
| `Resources/Config/Audio.csv` | 新增 | 22 条音频配置（3 BGM + 11 SFX + 7 UI） |
| `Core/Data/ConfigLoader.cs` | 修改 | 新增 `LoadAudioFromCsv()` + `GetAudioConfig()` + `GetAllAudioConfigs()` |

---

## 十二、数据驱动 UI 框架

### 12.1 设计目标

传统 UI 开发常在 Update 里轮询数据或深度耦合业务逻辑。新的 UI 框架全面转向"数据驱动（Data-Binding）"与"栈式管理"：

- **UI 组件绝对不碰业务逻辑**，只负责监听数据变化并刷新表现
- **数据变更自动传播**，通过 `BindableProperty<T>` 的委托链机制
- **面板管理统一**，通过 UIManager 的栈式路由控制器

### 12.2 BindableProperty<T>

泛型响应式属性包装类，核心机制：

```csharp
// 声明绑定属性
public readonly BindableFloat Health = new(100f);

// UI 侧注册回调
Health.OnChanged += (newValue, oldValue) => UpdateHealthBar(newValue);

// 业务侧赋值（自动触发回调）
Health.Value = 80f; // → UpdateHealthBar(80f) 被自动调用
```

关键 API：

| API | 说明 |
|-----|------|
| `Value` | 赋值时自动比对新旧值，不同则触发 OnChanged |
| `OnChanged` | `event Action<T, T>` 委托链，提供新旧值 |
| `Notify()` | 强制触发回调（不改变值），用于 UI 初始化 |
| `SetValueWithoutNotify()` | 静默赋值（不触发回调），用于反序列化恢复 |
| `ClearListeners()` | 清除所有回调，在 OnDestroy 中调用 |

快捷别名：`BindableFloat` / `BindableInt` / `BindableBool`，扩展方法 `AddDelta()` / `SetClamped()`。

### 12.3 AttributeSet 数据绑定重构

`AttributeSet` 的 `_currentHealth` 已替换为 `BindableFloat`：

```csharp
public readonly BindableFloat BindableHealth = new();
public readonly BindableFloat BindableMaxHealth = new();

public float CurrentHealth
{
    get => BindableHealth.GetValue();
    set => BindableHealth.SetClamped(value, 0f, MaxHealth);
}
```

写入 `CurrentHealth` 时自动触发 `BindableHealth.OnChanged`，UI 组件无需轮询。

### 12.4 UIManager 与栈式面板管理

**5 层级枚举**：

| 层级 | sortingOrder | 说明 |
|------|-------------|------|
| `HUD` | 0 | 底层 HUD（血条、小地图等常驻元素） |
| `Panel` | 100 | 常驻面板（背包、角色面板等） |
| `Popup` | 200 | 弹窗（确认框、提示框等模态对话框） |
| `Alert` | 300 | 顶层警告（网络断开、强制更新等） |
| `LoadingCurtain` | 400 | 加载幕布（场景切换遮罩） |

**UIWindowBase 生命周期**：

```text
Init()（仅首次）
  ↓
OnOpen(openArgs) → OnRefresh()
  ↓              ↑（数据变化时重复调用）
OnClose()
  ↓
Destroy 或 CacheOnClose 隐藏
```

**栈式路由**：打开面板入栈、关闭出栈，自动恢复上一层面板交互。模态面板显示半透明遮罩阻止低层级交互。

**QAFloatingText 归口**：浮动文字统一通过 `UIManager.ShowFloatingText()` 在 HUD 层创建。

### 12.5 新增/修改文件清单

| 文件 | 类型 | 说明 |
|------|------|------|
| `Core/Binding/BindableProperty.cs` | 新增 | 泛型响应式属性包装类 |
| `Core/Binding/BindablePropertyExtensions.cs` | 新增 | 快捷别名 + AddDelta/SetClamped 扩展 |
| `Presentation/UI/UIManager.cs` | 新增 | 栈式面板管理器 + 5 层级 + 模态遮罩 |
| `Presentation/UI/HealthBarUI.cs` | 新增 | 数据驱动血条示例组件 |
| `GAS/Core/GameplayEffectSystem.cs` | 修改 | AttributeSet 重构为 BindableFloat 驱动 |

---

## 十三、全局音频管理器

### 13.1 设计目标

参考现有的 VFXManager，音频系统遵循"CSV 配置驱动 + 对象池复用 + 资源异步加载"原则。引入并发数限制（Voice Stealing/Culling）防止混战中同类音效过多导致的"爆音"或性能劣化。

### 13.2 Audio.csv 配置

| 列名 | 类型 | 说明 |
|------|------|------|
| `audio_id` | int | 音频唯一 ID |
| `resource_path` | string | Resources 下相对路径 |
| `category` | enum | BGM / SFX / UI / Voice |
| `volume_weight` | float | 音量权重（0~1，与分类主音量相乘） |
| `is_3d` | bool | 是否 3D 空间音效 |
| `max_distance` | float | 最大衰减距离 |
| `loop` | bool | 是否循环 |
| `max_concurrent` | int | 同 ID 并发上限（0=使用全局默认） |
| `priority` | int | 优先级（0=最高） |
| `fade_in` | float | BGM 淡入时间（秒） |
| `fade_out` | float | BGM 淡出时间（秒） |

### 13.3 AudioManager 核心 API

| API | 说明 |
|-----|------|
| `PlayBGM(audioId)` | 播放 BGM（支持淡入淡出切换） |
| `StopBGM(fadeOut)` | 停止 BGM（带淡出） |
| `PlaySFX(audioId, position)` | 播放 3D 空间音效 |
| `PlaySFX(audioId)` | 播放 2D 音效 |
| `PlayUI(audioId)` | 播放 UI 音效（不受并发限制） |
| `SetCategoryVolume(category, vol)` | 设置分类主音量 |
| `StopAllSFX()` | 停止所有 SFX |

### 13.4 同频剔除（Voice Stealing）

每帧统计同 ID SFX 的播放数量，超限时的处理策略：

1. 尝试 `TryStealOldest(audioId)` —— 停止活跃列表中同 ID 最早的条目，回收其 AudioSource
2. 若无活跃条目可顶替，则拒绝新请求

配置示例：`Audio.csv` 中爆炸声 `max_concurrent=3`，同帧最多 3 个爆炸声，第 4 个将顶替最早的。

### 13.5 技能图集成

```text
PlaySFXNode（技能图节点）
  ↓ Tick() 或 Compile()
SkillEffectData { EffectType = PlaySFX, SFXKey = "2001" }
  ↓ PresentationDispatcher
AudioDispatcher.Apply(effect, ctx)
  ↓ ResolvePosition + QueryConfig
AudioManager.PlaySFX(audioId, position)
```

**PlaySFXNode**：`CanCompile = true`，`Compile()` 输出 `SkillEffectType.PlaySFX` 的 `SkillEffectData`。
**AudioDispatcher**：与 `PresentationDispatcher` 并列的静态分发器，在 `TimelineSkillRunner` 的 `PlaySFX` 分支中被调用。

### 13.6 新增/修改文件清单

| 文件 | 类型 | 说明 |
|------|------|------|
| `Presentation/Audio/AudioConfig.cs` | 新增 | 音频配置数据类 + AudioCategory 枚举 |
| `Presentation/Audio/AudioManager.cs` | 新增 | 全局音频管理器（对象池 + BGM + 同频剔除） |
| `Presentation/Audio/AudioDispatcher.cs` | 新增 | 技能时间轴音频分发器 |
| `SkillSystem/Nodes/VFX/PlaySFXNode.cs` | 新增 | 音效播放节点 |
| `Resources/Config/Audio.csv` | 新增 | 22 条音频配置 |
| `Core/Data/ConfigLoader.cs` | 修改 | 新增 LoadAudioFromCsv + GetAudioConfig |
| `SkillSystem/Runtime/TimelineSkillRunner.cs` | 修改 | PresentationDispatcher 新增 PlaySFX 分支 |

---

## 十四、状态快照与存档系统

### 14.1 设计目标

由于现有架构已高度数据化且剥离了表现层，存档系统的核心就是"把运行时的核心组件数据抽成快照，落盘存储"。采用"接口搜集 + 全局字典序列化"策略，确保新模块以极低成本接入。

### 14.2 ISaveable 契约接口

```csharp
public interface ISaveable
{
    string SaveKey { get; }                              // 唯一标识
    Dictionary<string, object> CaptureSnapshot();       // 生成快照
    void RestoreSnapshot(Dictionary<string, object> snapshot); // 恢复状态
}
```

**Key 命名规范**：`"{模块类型}.{实例标识}"`，如 `"Inventory.Player"`、`"AttributeSet.Boss_01"`。

### 14.3 SaveManager 完整流程

**保存流程**：

```text
FindAllSaveables()           // 搜集场景中所有 ISaveable
  ↓
CaptureSnapshot()           // 每个模块生成 Dictionary<string, object>
  ↓
JsonUtility.ToJson()        // 序列化为 JSON
  ↓
AES-128 Encrypt()           // 可选加密（防篡改）
  ↓
SafeWrite()                 // 临时文件 → 验证 → 替换原文件
```

**加载流程**：

```text
ReadAllText()                // 读取存档文件
  ↓
AES-128 Decrypt()           // 解密
  ↓
JsonUtility.FromJson()      // 反序列化为 SaveData
  ↓
foreach module              // 按 Key 分发给对应 ISaveable
  ↓
RestoreSnapshot()           // 使用 SetValueWithoutNotify 恢复
  ↓
Notify()                    // 统一通知 UI 刷新
```

### 14.4 安全写入与备份机制

| 机制 | 说明 |
|------|------|
| **临时文件** | 先写入 `.tmp` 文件，验证完整性后再替换原 `.sav` 文件 |
| **备份文件** | 保存前将当前存档复制为 `.bak` 备份 |
| **断电恢复** | 加载失败时自动尝试从 `.bak` 备份恢复 |
| **AES 加密** | CBC 模式 + PKCS7 填充，防止玩家轻易修改本地数据 |

### 14.5 业务适配器

| 适配器 | 序列化内容 | 恢复方式 |
|--------|-----------|----------|
| `SaveableInventory` | `slot_i: "itemId,count"` 格式 | `SetSlotDirect()` 静默写入，不触发事件 |
| `SaveableAttributeSet` | `current_health` / `max_health` 等 | `SetValueWithoutNotify()` 静默恢复，恢复后统一 `Notify()` |
| `SaveableQuestSystem` | `completed_quest_ids: "id1,id2,..."` | `MarkQuestCompleted()` 静默写入，不触发事件 |

**新模块接入**：实现 `ISaveable` + 挂载适配器组件到对应 GameObject，SaveManager 自动搜集，零改动。

### 14.6 新增/修改文件清单

| 文件 | 类型 | 说明 |
|------|------|------|
| `Core/Save/ISaveable.cs` | 新增 | 存档契约接口 |
| `Core/Save/SaveManager.cs` | 新增 | 统一存档管理器（JSON/AES/安全写入/备份） |
| `Inventory/SaveableInventory.cs` | 新增 | 背包容档适配器 |
| `GAS/Core/SaveableAttributeSet.cs` | 新增 | 属性存档适配器 |
| `Quest/SaveableQuestSystem.cs` | 新增 | 任务存档适配器 |
| `Inventory/InventoryComponent.cs` | 修改 | 新增 `SetSlotDirect()` 方法 |
| `Quest/QuestRunner.cs` | 修改 | 新增 `MarkQuestCompleted()` 静态方法 |
