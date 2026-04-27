# 技能系统设计说明书

## 一、架构概览

本项目是一个**商业级技能系统流水线**，支撑策划填表（CSV）、设计师连线（NodeCanvas）、程序守护（Runtime/VFX/Debug）的规模化协作。

### 四层架构

```
┌──────────────────────────────────────────────────┐
│  第一层 — CSV 数据层（策划维护）                    │
│  Skill.csv / SkillRecipe.csv / NodePreset.csv    │
│  → 单一数据源，所有数值参数的唯一出处               │
├──────────────────────────────────────────────────┤
│  第二层 — NodeCanvas 图层（可视化逻辑）             │
│  SkillGraph 资产 + 22 种节点类型                   │
│  → 连线定义执行流程，绑定引用 CSV 字段              │
├──────────────────────────────────────────────────┤
│  第三层 — 技能运行时（协程执行引擎）                 │
│  SkillCaster / SkillRunner / SkillContext        │
│  → 状态机驱动释放管线，Blackboard 解耦节点间通信     │
├──────────────────────────────────────────────────┤
│  第四层 — 特效表现层                               │
│  VFXManager / Shader / Projectile               │
│  → 对象池管理特效生命周期，Shader 处理视觉风格       │
└──────────────────────────────────────────────────┘
```

### 核心设计原则

| # | 原则 | 说明 |
|---|------|------|
| 1 | **单一数据源** | 所有运行时数值必须从 CSV 经由 `ConfigLoader.GetSkillConfig(id)` 获取。禁止在节点中硬编码数值。 |
| 2 | **逻辑与数值分离** | CSV 存原始数值（伤害、冷却等）。NodeCanvas 存执行流程（分支、顺序）。CSV 中不写公式，节点中不写死常量。 |
| 3 | **松耦合设计** | 节点不直接引用 GameObject。伤害通过 `IDamageable.TakeDamage()` 传递。特效通过 `VFXManager.Play()` 播放。节点间仅通过 Blackboard 通信。 |
| 4 | **对象池强制** | 所有 VFX 必须经由 `VFXObjectPool.Get/Release` 管理。禁止使用 `Instantiate/Destroy` 创建运行时特效。 |
| 5 | **Blackboard 隔离** | 节点间仅通过 `Blackboard.SetValue(key)` / `GetFloat(key)` 通信。这保障了子图复用能力和运行时调试可见性。 |

---

## 二、技能释放管线

技能从触发到完成的完整生命周期由 `SkillCaster` 统一管理：

### CastStage 状态机

```
TryCast()
  ├─ [校验: 冷却、资源、射程、忙状态]
  ├─ 扣除资源
  │
  ▼
┌──────────────┐    可被打断
│  前摇阶段     │─────────────────┐
│  cast_time   │  （如果允许）     │
│  进度条显示   │                 │
└──────┬───────┘                 │
       │ (未被打断)               │
       ▼                         │
┌──────────────┐    可被打断     │
│  执行阶段     │─────────────────┤
│  运行技能图   │  （如果允许）     │
│  节点触发     │                 │
└──────┬───────┘                 │
       │ (未被打断)               │
       ▼                         ▼
┌──────────────┐          ┌──────────────┐
│  后摇阶段     │          │   打断清理    │
│  post_time   │          │   事件触发    │
│  不可打断     │          └──────────────┘
└──────┬───────┘
       ▼
┌──────────────┐
│   空闲状态    │
│  设置冷却     │
└──────────────┘
```

### 各阶段详情

| 阶段 | 谁在等待？ | 是否可打断？ | 写入的 BBKey |
|------|-----------|-------------|-------------|
| **前摇** | `CastPipeline` while 循环 | ✅ 是 | `IsPreCasting`、`PreCastTime`、`PreCastProgress` |
| **执行** | 图节点（PreCastNode → ... → PostCastNode） | ✅ 是（ChannelNode、PreCastNode 中检测） | `IsChanneling`、`ChannelProgress`、`DamageOverride` … |
| **后摇** | `CastPipeline` while 循环 | ❌ 否（恢复阶段） | `IsPostCasting`、`PostCastTime`、`PostCastProgress` |
| **打断** | 不适用 | — | `IsInterrupted`、`InterruptReason` |

### 打断条件

`SkillCaster.Interrupt(reason)` 需要同时满足两个条件：
1. `IsInterruptibleStage` → 当前阶段为前摇/执行/引导（后摇不可打断）
2. `SkillConfig.IsInterruptible == true`（来自 CSV，默认 `true`）

例外：`InterruptReason.Death`（死亡）绕过所有限制。

### 打断传播流程

```
CombatSystem.Interrupt(眩晕)
  → SkillCaster.Interrupt(眩晕)
    → 检查 IsInterruptibleStage ✓
    → 检查 config.IsInterruptible ✓
    → 设置 ctx.IsInterrupted = true
    → 设置 BBKey.IsInterrupted = true
    → StopCoroutine(_castCoroutine)
    → 设置 stage = Interrupted
    → 触发 OnInterrupted 事件
```

图内节点（`PreCastNode`、`ChannelNode`）在逐帧 while 循环中检测 `ctx.IsInterrupted`，一旦为真则 `yield break` 提前退出。`ResolveNextNode()` 在打断状态下返回 `null`。

---

## 三、节点目录（共 22 种）

### 生命周期与流程控制

| 节点 | 类型 | 说明 | 关键参数 |
|------|------|------|---------|
| **StartNode** | 入口 | 图入口点，无逻辑 | — |
| **EndNode** | 出口 | 图终点，无逻辑 | — |
| **DelayNode** | 计时 | 等待 `delaySeconds`（来自 `SkillConfig.DelaySeconds`） | `FloatBinding delaySeconds` |
| **ParallelNode** | 分支 | 通过动态端口分叉为 N 个并行分支，等待全部完成 | `SkillNode[] branches` |
| **ConditionNode** | 分支 | 基于随机/BlackboardBool/BlackboardFloat 的 if-else 分支 | `ConditionMode mode`、`threshold` |
| **SubGraphNode** | 复用 | 执行引用的 `SkillGraph` 子图 | `SkillGraph subGraph` |

### 释放管线节点

| 节点 | 类型 | 说明 | 关键参数 |
|------|------|------|---------|
| **PreCastNode** | 释放 | 播放前摇特效。时序由 `CastPipeline` 控制。 | `FloatBinding castTime`、`StringBinding preCastVfxKey`、`continueOnInterrupt` |
| **ChannelNode** | 释放 | 逐 tick 引导，每 tick 通过 `IDamageable` 施加伤害。自主管理时序。`ChannelDuration=0` 时为空操作。 | `FloatBinding channelDuration`、`tickInterval`、`tickDamageRate`、`channelVfxKey`、`channelFinishVfxKey` |
| **PostCastNode** | 释放 | 播放后摇特效。时序由 `CastPipeline` 控制。 | `FloatBinding postCastTime`、`StringBinding postCastVfxKey` |

### 伤害与状态

| 节点 | 类型 | 说明 | 关键参数 |
|------|------|------|---------|
| **DamageNode** | 核心 | 通过 `IDamageable` 对 `ctx.Target` 施加伤害。支持从 `BBKey.DamageOverride` 读取覆盖值。 | `FloatBinding damageAmount`、`damageRate`、`multiplyByDamageRate` |
| **ApplyStatusNode** | 核心 | 通过 `IStatusReceiver` 向目标施加状态标记（灼烧/寒蚀/导电/标记/…）。 | `StringBinding statusTags`、`append`、`defaultDuration` |
| **ReactionNode** | 元数据 | 触发跨元素反应。基于状态组合修改伤害倍率。 | `reactionSummary`、`damageMultiplier`、`writeDamageOverride` |
| **ResonanceNode** | 元数据 | 检测/解析行/列共鸣标记。写入 `BBKey.HasResonance`。 | `resonanceTags` |

### 特效与视觉

| 节点 | 类型 | 说明 | 关键参数 |
|------|------|------|---------|
| **PlayVFXNode** | 特效 | 从对象池播放 VFX。阶段选择器：自动/释放/命中/光束/反应/地形/终结。 | `VFXStage stage`、`StringBinding vfxKey`、`styleKey`、`DirectionMode`、`TransformBinding` |
| **FinisherStagedNode** | 特效 | 二段终结技：吸能 → 爆发。用于奥义技能。 | `absorbVfxKey`、`burstVfxKey`、`absorbDuration`、`burstDuration` |
| **ProjectileNode** | 特效 | 发射投射物，支持轨迹（直线/追踪/抛物线）和追踪模式。命中时写入 `ProjectileHitPosition`。 | `LaunchMode`、`HomingMode`、`FloatBinding projectileSpeed`、`projectilePrefab`、`impactVfxKey` |
| **PaintTerrainNode** | 地形 | 向目标区域施加地形标记（焦土/冰面/蒸汽/…）。 | `StringBinding terrainTags` |

### 工具类

| 节点 | 类型 | 说明 | 关键参数 |
|------|------|------|---------|
| **LogNode** | 调试 | 向控制台输出日志（仅编辑器模式）。 | `StringBinding message` |
| **ModifyFloatNode** | 数据 | 对浮点数应用倍率并写入 Blackboard。 | `outputKey`、`inputValue`、`multiplier` |
| **RollChanceNode** | 数据 | 掷随机概率，将 `true/false` 写入 Blackboard。 | `outputKey`（默认 `BBKey.IsCrit`） |
| **SetValueNode** | 数据 | 将字符串字面值写入 Blackboard。 | `blackboardKey`、`StringBinding value` |

---

## 四、Blackboard 键名参考

Blackboard 是节点间**唯一的通信通道**。所有键名均为 `BBKey.cs` 中的常量。

### 释放管线键名

| 键名 | 类型 | 由谁写入 | 说明 |
|------|------|---------|------|
| `IsPreCasting` | bool | `CastPipeline` | 前摇阶段激活中 |
| `IsPostCasting` | bool | `CastPipeline` | 后摇阶段激活中 |
| `IsChanneling` | bool | `ChannelNode` | 引导中 |
| `IsInterrupted` | bool | `SkillCaster.Interrupt()` | 技能已被打断 |
| `InterruptReason` | string | `SkillCaster.Interrupt()` | 打断原因（枚举转为字符串） |
| `PreCastTime` | float | `CastPipeline` | 前摇总时长（秒） |
| `PostCastTime` | float | `CastPipeline` | 后摇总时长（秒） |
| `PreCastProgress` | float | `CastPipeline` | 前摇进度 0.0 → 1.0 |
| `PostCastProgress` | float | `CastPipeline` | 后摇进度 0.0 → 1.0 |

### 引导键名

| 键名 | 类型 | 由谁写入 | 说明 |
|------|------|---------|------|
| `ChannelDuration` | float | `ChannelNode` | 引导总时长（秒） |
| `ChannelProgress` | float | `ChannelNode` | 引导进度 0.0 → 1.0 |
| `ChannelCurrentTick` | int | `ChannelNode` | 当前 tick 序号（从 1 开始） |
| `ChannelTotalTicks` | int | `ChannelNode` | 已完成的 tick 总数 |
| `ChannelTick_{n}` | bool | `ChannelNode` | 第 n 号 tick 已触发 |

### 伤害与状态键名

| 键名 | 类型 | 由谁写入 | 由谁读取 | 说明 |
|------|------|---------|---------|------|
| `DamageOverride` | float | `ModifyFloatNode`、`ChannelNode`、`ReactionNode` | `DamageNode`（useOverride 模式） | 覆盖伤害值 |
| `DelayOverride` | float | — | `DelayNode` | 覆盖延迟时间 |
| `IsCrit` | bool | `RollChanceNode` | `ConditionNode` | 已触发暴击 |
| `LastDamage` | float | `DamageNode` | 后续节点 | 上一次造成的伤害 |

### 上下文键名

| 键名 | 类型 | 由谁写入 | 说明 |
|------|------|---------|------|
| `BranchCount` | float | `ParallelNode` | 当前活跃分支数 |
| `TargetDistance` | float | — | 施法者到目标的距离 |
| `CurrentGraph` | string | `SkillContext` | 当前执行的图 ID |
| `StatusTags` | string | `ApplyStatusNode` | 已施加的状态标记（竖线分隔） |
| `TerrainTags` | string | `PaintTerrainNode` | 已施加的地形标记 |
| `ResonanceTags` | string | — | 当前共鸣配置 |
| `ReactionSummary` | string | — | 反应描述 |
| `RecipeId` | string | — | 当前配方标识 |
| `HasResonance` | bool | `ResonanceNode` | 共鸣条件已满足 |

### 投射物键名

| 键名 | 类型 | 由谁写入 | 说明 |
|------|------|---------|------|
| `ProjectileActive` | bool | `ProjectileNode` | 投射物飞行中 |
| `ProjectileHitPosition` | Vector3 | `ProjectileNode` | 碰撞位置 |
| `ProjectileHitTarget` | string | `ProjectileNode` | 被命中目标名称 |

---

## 五、CSV 配置参考

### Skill.csv — 20 列（单一数据源）

```
skill_id | name | graph_path | impact_vfx | beam_vfx | damage | damage_rate |
cooldown | cast_range | delay_seconds | crit_chance | radius | chain_count |
vfx_duration | cast_time | channel_duration | post_cast_time | interruptible |
projectile_speed | projectile_prefab | resource_cost
```

| 字段 | 类型 | 示例值 | 说明 |
|------|------|--------|------|
| `skill_id` | int | 10001 | 技能唯一标识 |
| `name` | string | 烈阳长枪 | 显示名称 |
| `graph_path` | string | SkillGraphs/ElementLine/… | NodeCanvas 图资源路径 |
| `impact_vfx` | string | HitSpark | 命中特效键名 |
| `beam_vfx` | string | ArcBeam | 光束特效键名（可为空） |
| `damage` | float | 72 | 基础伤害 |
| `damage_rate` | float | 1.0 | 伤害倍率 |
| `cooldown` | float | 1.8 | 冷却时间（秒） |
| `cast_range` | float | 8.0 | 最大释放距离 |
| `delay_seconds` | float | 0.08 | 特效/伤害触发前的延迟 |
| `crit_chance` | float | 0.18 | 暴击概率（0–1） |
| `radius` | float | 2.2 | 效果半径 |
| `chain_count` | int | 3 | 连锁/弹跳次数（0=无连锁） |
| `vfx_duration` | float | 0.35 | 特效显示时长 |
| `cast_time` | float | 0.12 | 前摇蓄力时间（秒） |
| `channel_duration` | float | 0 | 引导持续时长（0=瞬发） |
| `post_cast_time` | float | 0.08 | 后摇恢复时间（秒） |
| `interruptible` | bool | true | 前摇/引导阶段是否可被打断 |
| `projectile_speed` | float | 0 | 投射物速度（0=无投射物） |
| `projectile_prefab` | string | — | 投射物预制体键名 |
| `resource_cost` | float | 15 | 法力/能量消耗 |

### SkillRecipe.csv

配方级内容：映射元素组合到预设模板。一行 = 一个可玩技能。

### NodePreset.csv

将 `node_preset_id` 映射到可复用的图模板。共 12 种预设：`ImpactLane`、`BeamLane`、`CritBranch`、`ConductiveChain`、`StatusAmplify`、`ReactionBurst`、`RowResonance`、`TerrainUltimate`、`ChainUltimate`、`ElementCollapse`、`ExecuteUltimate`、`TrapExecute`。

---

## 六、设计工具与代码生成

### 编辑器菜单

| 菜单项 | 功能 |
|--------|------|
| `Tools/Skills/Templates/根据配置生成元素阵线技能图` | 从 `SkillRecipe.csv` 重建全部配方图 |
| `Tools/Skills/Templates/根据配方生成运行时技能配置` | 从配方重新生成 `Skill.csv` |
| `Tools/Skills/Validate Graph` | 对当前打开的图执行常见问题校验 |
| `Tools/Skills/Debug Window` | 运行时调试器：节点高亮、Blackboard 监视、执行回放 |

### 代码生成流水线

```
SkillRecipe.csv ──→ ElementLineGraphGenerator（图生成器）
                      │
                      ├─ BuildRecipeGraph()
                      │   ├─ StartNode
                      │   ├─ LogNode（配方摘要）
                      │   ├─ PreCastNode（所有预设均自动插入）
                      │   ├─ ChannelNode（BeamLane / ConductiveChain / 射线 / 雷链 自动插入）
                      │   ├─ 预设专属节点（VFX、Damage、Status、Terrain …）
                      │   ├─ PostCastNode（所有预设均自动插入）
                      │   └─ EndNode
                      │
                      └─ 输出: 52 个 SkillGraph 资产（.asset 文件）

SkillRecipe.csv ──→ ElementLineSkillConfigGenerator（配置生成器）
                      │
                      ├─ ResolveDamage()、ResolveCooldown()、ResolveRange()
                      ├─ ResolveCastTime()、ResolveChannelDuration()、ResolvePostCastTime()
                      ├─ ResolveInterruptible()、ResolveProjectileSpeed()、ResolveProjectilePrefab()
                      ├─ ResolveResourceCost()
                      └─ 输出: Skill.csv（52 行，20 列）
```

### 典型工作流

1. 策划在 `SkillRecipe.csv` 中添加新配方行
2. 运行 `Tools/Skills/Templates/根据配方生成运行时技能配置` → 生成 CSV 行
3. 运行 `Tools/Skills/Templates/根据配置生成元素阵线技能图` → 生成图资产
4. 在 NodeCanvas 编辑器中打开图，微调节点位置/绑定
5. 运行 `Tools/Skills/Validate Graph` 校验
6. 进入运行模式，打开 `Tools/Skills/Debug Window` 调试

---

## 七、运行时关键类型

### SkillCaster（生命周期管理器）
- `TryCast(skillId, target)` → 前置校验 + 启动管线
- `Interrupt(reason)` → 设置打断标记 + 停止协程
- `CurrentStage` → CastStage 枚举，供 UI/外部系统读取
- `IsBusy` → 当前是否有技能在执行
- 事件：`OnStageChanged`、`OnInterrupted`

### SkillRunner（图执行器）
- `RunSkill(graph, ctx)` → 通过 IEnumerator 逐个节点执行图
- `RunNodeChain(startNode, ctx, graph)` → 执行部分节点链
- 调试控制：`Pause()`、`Step()`、`Continue()`

### SkillContext（单次释放状态）
- `SkillID`、`Config`（SkillConfig）
- `Caster`（Transform）、`Target`（Transform）、`CasterComponent`（SkillCaster）
- `IsInterrupted` — 节点中检测
- `Blackboard` — 节点间通信
- `DebugEnabled`、`Recorder`

### 绑定系统（FloatBinding / StringBinding）
- `SourceType.SkillConfig` → 从 `SkillConfig.GetFloat(SkillFloatField)` / `GetString(fieldName)` 读取
- `SourceType.Blackboard` → 从 `Blackboard.GetFloat(key)` 读取
- `SourceType.Literal` → 使用内联字面值

### IDamageable（伤害接口）
```csharp
public interface IDamageable
{
    void TakeDamage(float amount, Transform instigator);
}
```

### IInterruptible（打断接口）
```csharp
public interface IInterruptible
{
    bool IsCasting { get; }
    void InterruptCast();
}
```

---

## 八、文件地图

```
Assets/
├── Examples/
│   ├── SkillDesignGuide.md          ← 本文档
│   ├── NodeAuthoringWorkbook.md     ← 节点搭建工作手册
│   └── ElementLineSkillCatalog.md   ← 技能总表
├── Resources/Config/
│   ├── Skill.csv                    ← 运行时技能数据（单一数据源）
│   ├── SkillRecipe.csv              ← 配方到预设的映射
│   ├── NodePreset.csv               ← 预设定义
│   ├── Effect.csv                   ← VFX 特效配置
│   ├── Reaction.csv                 ← 跨元素反应
│   ├── Terrain.csv                  ← 地形变异配置
│   ├── Buff.csv                     ← Buff 定义
│   ├── GuestElement.csv             ← 外来元素定义
│   ├── Isotope.csv                  ← Roguelike 同位素修改器
│   └── VFXArtProfile.csv            ← VFX 风格化配置
├── Resources/SkillGraphs/ElementLine/
│   ├── Common/                      ← 可复用子图
│   └── Recipes/                     ← 自动生成的配方图
├── Scripts/
│   ├── Data/                        ← 配置模型（SkillConfig 等）
│   ├── Nodes/                       ← 22 种节点实现
│   ├── Runtime/                     ← SkillCaster、SkillRunner、BBKey、Binding
│   ├── VFX/                         ← VFXManager、VFXObjectPool
│   ├── Editor/                      ← 生成器、校验器、调试工具
│   ├── Entity/                      ← SkillOwner 组件
│   └── Graph/                       ← SkillGraph、SkillNode 基类
└── Shaders/                         ← ElementLineBeam、GroundRing、Pulse
```
