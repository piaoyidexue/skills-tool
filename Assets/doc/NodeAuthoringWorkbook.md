# 节点搭建工作手册

## 目标

使用当前工程中的 `自建技能图框架 + CSV + GE + EQS` 运行时，快速搭建技能图。

## 最短制作路径

1. 在 `Resources/Config/SkillRecipe.csv` 中挑选一条技能配方
2. 查看其对应的 `node_preset_id`
3. 自动生成器已预先创建了图，路径为 `Resources/SkillGraphs/ElementLine/Recipes/{RecipeId}_{Recipe}.asset`
4. 在 Inspector / 自定义编辑器中打开，所有节点已预连线
5. 仅在行为确实需要变更时，才调整绑定覆盖和 Blackboard 键名
6. 运行 `Tools/Skills/Validate Graph` 校验
7. 进入运行模式，打开 `Tools/Skills/Debug Window`

> 两个生成器（`ElementLineGraphGenerator` + `ElementLineSkillConfigGenerator`）会自动产出 99% 的图和 CSV 内容。手动搭建是例外。

---

## 通用管线（所有预设共享）

```
StartNode → LogNode(摘要) → PreCastNode(前摇) → [ChannelNode] → [预设核心] → PostCastNode(后摇) → EndNode
```

---

## 新增：动画驱动技能流程

如果技能涉及近战攻击、需要动画帧精确对齐，推荐使用下面的流程替代传统的 `DelayNode` 链：

```
PlayAnimNode → AnimationEventWaitNode(waitEvent="OnHit") → DamageNode
                      │
                      ├─ 等待动画 OnHit 事件（黑板 AnimEvent）
                      ├─ 攻速变化时自动适配（动画速度自动调整）
                      └─ 超时保护：timeout=2s
```

**AnimationEventWaitNode 配置**:

| 参数 | 推荐值 | 说明 |
|------|--------|------|
| `waitEvent` | `"OnHit"` | 等待的动画事件名 |
| `timeout` | `2` | 超时保护（秒） |
| `matchMode` | `Contains` | 匹配模式 |

**Animator 配置步骤**:

1. 角色 GameObject 挂载 `SkillAnimatorController` + `AnimationEventReceiver`
2. 在 AnimationClip 中需要触发的帧上添加 Event
3. Event 的 Function 选择 `AnimationEventReceiver.OnSkillAnimationEvent`
4. StringParameter 设为 `"Skill:OnHit"`

---

## 新增：EQS 目标查询

需要复杂索敌逻辑（如"范围内血量最低的3个带灼烧敌人"）时：

```csharp
// 在 TargetQueryNode 中调用
var config = TargetQueryConfig.CreateTemplate(range: 15f, maxResults: 3);
config.Filters.Add(new TagFilter { RequiredTags = new[] { "tag.status.burn" } });
config.Filters.Add(new HPThresholdFilter { ExcludeDead = true });
config.Sorters.Add(new ByHPSorter { SortDirection = SortOrder.Ascending });
var targets = config.Execute(center, self, ctx);
```

可用 Filter：
- `DistanceFilter` — 距离范围
- `TagFilter` — GE Tag 筛选
- `HPThresholdFilter` — 血量阈值
- `TeamFilter` — 队伍筛选

可用 Sorter：
- `ByDistanceSorter` — 按距离
- `ByHPSorter` — 按血量
- `ByThreatSorter` — 按威胁值

---

## 预设 → 图流程参考

### Preset_ImpactLane — 单目标直接命中

```
PreCast → CastVFX → Delay → ImpactVFX → Damage → Status → PostCast → End
```

### Preset_BeamLane — 持续光束 / 引导攻击

```
PreCast → Channel(tick伤害) → CastVFX → Delay → BeamVFX → Damage → Status → PostCast → End
```

### Preset_CritBranch — 暴击 / 处决分支

```
PreCast → CastVFX → Delay → RollChance → Condition
  ├─ 命中:  ModifyFloat → FinisherVFX → Damage(useOverride) ─┐
  └─ 未命中: SubGraph(Common_ImpactDamage) ──────────────────┤
                                                              ▼
                                                    Status → PostCast → End
```

### Preset_ConductiveChain — 多目标连锁

```
PreCast → Channel(tick) → CastVFX → Delay → BeamVFX → Damage → Status → Reaction → PostCast → End
```

其他预设（StatusAmplify、ReactionBurst、RowResonance、TerrainUltimate、ChainUltimate、ElementCollapse、ExecuteUltimate、TrapExecute）详见[技能总表](ElementLineSkillCatalog.md)。

---

## Blackboard 通信模式

### 伤害覆盖
```
ModifyFloatNode(outputKey=DamageOverride) → DamageNode(useOverride)
```

### 随机分支
```
RollChanceNode(outputKey=IsCrit) → ConditionNode(mode=BlackboardBool, bbKey=IsCrit)
```

### 动画事件
```
SkillAnimatorController → BBKey.AnimEvent = "OnHit"
  → AnimationEventWaitNode(waitEvent="OnHit") 检测 → 通过
```

### 投射物命中
```
ProjectileNode → BBKey.ProjectileHitPosition / ProjectileHitTarget
```

---

## 绑定参考

### FloatBinding 三种来源
```csharp
// CSV 字段
new FloatBinding { Source = SkillConfig, SkillField = SkillFloatField.Damage }

// Blackboard
new FloatBinding { Source = Blackboard, BlackboardKey = BBKey.DamageOverride }

// 字面值（仅限设计常量倍率）
new FloatBinding { Source = Literal, LiteralValue = 2.5f }
```

---

## 设计规则

1. **优先使用 `FloatBinding`，而非字面值**（字面值仅用于倍率常量）
2. **优先使用 `SubGraphNode`**（共享逻辑提取为 `Common_*`）
3. **禁止裸 Blackboard 字符串**（必须在 `BBKey.cs` 中定义常量）
4. **动画对齐使用 `AnimationEventWaitNode`**（避免用 DelayNode 猜测帧时间）
5. **伤害走 `DamagePipeline.CalculateAndApply()`**（避免直调 `IDamageable.TakeDamage`）
