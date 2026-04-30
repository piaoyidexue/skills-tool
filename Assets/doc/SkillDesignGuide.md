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

```
Assets/
├── Scripts/
│   ├── Data/          ← SkillConfig、ConfigLoader、Binding
│   ├── Nodes/         ← 24 种节点
│   ├── Runtime/       ← SkillCaster、SkillRunner、GE 系统、EQS、动画同步
│   ├── VFX/           ← VFX 模块
│   ├── AI/            ← AI 行为树 + 感知（NodeCanvas）
│   ├── Graph/         ← SkillGraphAsset + SkillNodeBase + SkillEdge（自建图框架）
│   └── Editor/        ← 生成器、校验器、调试工具
├── Resources/Config/  ← Skill.csv、Buff.csv、AITree.csv 等
└── doc/               ← 本文档及其他说明文档
```
