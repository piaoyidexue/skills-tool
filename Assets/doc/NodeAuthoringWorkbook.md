# 节点搭建工作手册

## 目标

使用当前工程中的 `NodeCanvas + CSV` 运行时，把《元素阵线》的技能配方快速转成可维护的技能图。

## 最短制作路径

1. 在 `Resources/Config/SkillRecipe.csv` 中挑选一条技能配方
2. 查看其对应的 `node_preset_id`
3. 自动生成器已预先创建了图，路径为 `Resources/SkillGraphs/ElementLine/Recipes/{RecipeId}_{Recipe}.asset`
4. 在 NodeCanvas 编辑器中打开，所有节点已预连线
5. 仅在行为确实需要变更时，才调整绑定覆盖和 Blackboard 键名
6. 运行 `Tools/Skills/Validate Graph` 校验
7. 进入运行模式，打开 `Tools/Skills/Debug Window`

> **重要提示**：两个生成器（`ElementLineGraphGenerator` + `ElementLineSkillConfigGenerator`）会自动产出 99% 的图和 CSV 内容。手动搭建是例外情况，不是常规操作。

---

## 预设 → 图流程参考

所有自动生成的图都遵循以下通用管线：

```
StartNode
  │
  ├─ LogNode               ← 配方摘要（名称、协同、突变）
  │
  ├─ PreCastNode            ← 前摇特效、打断检测（所有预设均插入）
  │
  ├─ ChannelNode            ← 引导 tick（仅 BeamLane 和 ConductiveChain）
  │
  ├─ [预设专属核心逻辑]
  │
  ├─ PostCastNode           ← 后摇特效（所有预设均插入）
  │
  └─ EndNode
```

### 各预设核心节点链

#### Preset_ImpactLane — 单目标直接命中

```
PreCastNode → CastVFX(Cast) → Delay → ImpactVFX(Impact) → Damage → Status → TerrainVFX → Terrain → PostCast → End
```

| 节点 | 绑定项 | 数据来源 |
|------|--------|---------|
| DelayNode | `delaySeconds` | `SkillFloatField.DelaySeconds` |
| PlayVFXNode（Impact 阶段） | `vfxKey` → stage=Impact | `SkillConfig.ImpactVFXKey` |
| DamageNode | `damageAmount`、`damageRate` | `SkillFloatField.Damage`、`SkillFloatField.DamageRate` |

**适用**：直线投射物、地雷爆发、简单射线原型。火系和冰系的基础攻击。

---

#### Preset_BeamLane — 持续光束 / 引导攻击

```
PreCastNode → ChannelNode(tick伤害) → CastVFX(Cast) → Delay → BeamVFX(Beam) → Damage → Status → TerrainVFX → Terrain → PostCast → End
```

| 节点 | 绑定项 | 数据来源 |
|------|--------|---------|
| ChannelNode | `channelDuration`、`tickInterval=0.3`、`tickDamageRate=0.3` | `SkillFloatField.ChannelDuration` |
| PlayVFXNode（Beam 阶段） | `vfxKey` → stage=Beam | `SkillConfig.BeamVFXKey` |

**ChannelNode 行为说明**：当 `ChannelDuration > 0` 时，运行 while 循环，每 `tickInterval` 秒通过 `IDamageable.TakeDamage()` 施加一次 tick 伤害。之后的常规 DamageNode 处理最后一击的伤害。当 `ChannelDuration = 0` 时，ChannelNode 为空操作。

**适用**：冰系射线、雷系光束。非引导型构建中 `ChannelDuration` 默认为 0。

---

#### Preset_CritBranch — 暴击 / 处决分支

```
PreCastNode → CastVFX(Cast) → Delay → RollChance → Condition
  ├─ 命中:  ModifyFloat(DamageOverride) → FinisherVFX → CritDamage(useOverride) ─┐
  └─ 未命中: SubGraph(Common_ImpactDamage) ───────────────────────────────────────┤
                                                                                  ▼
                                                                        Status → TerrainVFX → Terrain → PostCast → End
```

| 节点 | 绑定项 | 数据来源 |
|------|--------|---------|
| RollChanceNode | `outputKey` | `BBKey.IsCrit` |
| ConditionNode | `mode=BlackboardBool`、`bbKey` | `BBKey.IsCrit` |
| ModifyFloatNode | `outputKey=DamageOverride`、`multiplier` | `2f`（四槽：`2.5f`） |
| DamageNode（命中分支） | `damageAmount` → Blackboard | `BBKey.DamageOverride` |
| SubGraphNode（未命中分支） | `subGraph` | `Common_ImpactDamage` |

**适用**：金系收割技能、火系高暴击倍率的处决技能。

---

#### Preset_ConductiveChain — 多目标连锁闪电

```
PreCastNode → ChannelNode(tick伤害) → CastVFX(Cast) → Delay → BeamVFX(Beam) → Damage → Status → ReactionVFX → Reaction → TerrainVFX → Terrain → PostCast → End
```

| 节点 | 绑定项 | 数据来源 |
|------|--------|---------|
| ChannelNode | 同 BeamLane | — |
| PlayVFXNode（Beam 阶段） | `vfxKey` → stage=Beam | `SkillConfig.BeamVFXKey` |
| ReactionNode | `damageMultiplier=1.2`、`reactionSummary` | 配方行协同逻辑 |

**适用**：雷链弹跳、火系弹跳攻击。

---

#### Preset_StatusAmplify — 状态增幅 / 条件强化

```
PreCastNode → CastVFX(Cast) → Condition(随机)
  ├─ 命中:  ModifyFloat(DamageOverride) → BurstVFX(Reaction) → BoostedDamage(useOverride) ─┐
  └─ 未命中: BaseVFX(Impact) → BaseDamage ─────────────────────────────────────────────────┤
                                                                                           ▼
                                                                                 Status → TerrainVFX → Terrain → PostCast → End
```

| 节点 | 绑定项 | 数据来源 |
|------|--------|---------|
| ConditionNode | `mode=Random`、`threshold` | `SkillFloatField.CritChance` |
| ModifyFloatNode | `multiplier=1.5` | — |

**适用**：脆化外壳增幅、灼烧增强。

---

#### Preset_ReactionBurst — 状态消耗 / 反应爆发

```
PreCastNode → CastVFX(Cast) → Condition(随机)
  ├─ 命中:  BeamVFX → Reaction → ReactionVFX → Damage(useOverride) → FinisherVFX → Status → TerrainVFX → Terrain → PostCast → End
  └─ 未命中: → End
```

| 节点 | 绑定项 | 数据来源 |
|------|--------|---------|
| ConditionNode | `mode=Random`、`threshold` | `SkillFloatField.CritChance` |
| ReactionNode | `damageMultiplier` | `2f`（四槽：`2.5f`） |

**适用**：冻结爆发、碎裂、状态触发型爆炸。

---

#### Preset_RowResonance — 行 / 跨行共鸣

```
PreCastNode → CastVFX(Cast) → Delay → Resonance → SubGraph(Common_RowPulse) → Damage → Status → TerrainVFX → Terrain → PostCast → End
```

**适用**：同行联动效果、元素墙交互。

---

#### Preset_TerrainUltimate — 地表改写奥义

```
PreCastNode → CastVFX(Cast) → Delay → ImpactVFX → Damage → Status → TerrainVFX → Terrain → FinisherStaged → PostCast → End
```

| 节点 | 绑定项 | 数据来源 |
|------|--------|---------|
| FinisherStagedNode | `absorbVfx="EnergyAbsorb"`、`burstVfx` | `SkillConfig.FinisherVFXKey` |
| FinisherStagedNode | `absorbDuration=0.55`、`burstDuration` | `SkillFloatField.VFXDuration` |

**FinisherStagedNode**：吸能（0.55s）→ 爆发（VFXDuration）。奥义技能的二段视觉表现。

**适用**：地形转变类奥义。

---

#### Preset_ChainUltimate — 连锁奥义

```
PreCastNode → CastVFX(Cast) → Delay → Parallel
  ├─ 分支A: BeamVFX → DamageA ─┐
  └─ 分支B: ImpactVFX → DamageB ┘
                                    ▼
                            Status → TerrainVFX → Terrain → FinisherStaged → PostCast → End
```

**适用**：火雷连锁奥义、多段组合终结技。

---

#### Preset_ElementCollapse — 全行 / 全屏坍缩

```
PreCastNode → CastVFX(Cast) → Delay → Parallel
  ├─ 分支A: ImpactVFX → DamageA ─┐
  └─ 分支B: BeamVFX → DamageB ───┘
                                    ▼
                            Status → TerrainVFX → Terrain → FinisherStaged → PostCast → End
```

**适用**：四槽终极技能、全行清场。

---

#### Preset_ExecuteUltimate — 阈值处决

```
PreCastNode → CastVFX(Cast) → Condition(随机)
  ├─ 命中:  Reaction → ReactionVFX → FinisherStaged → Damage(useOverride) → Status → TerrainVFX → Terrain → PostCast → End
  └─ 未命中: → End
```

| 节点 | 绑定项 | 数据来源 |
|------|--------|---------|
| ReactionNode | `damageMultiplier=2.5` | — |

**适用**：确认击杀型终结技。

---

#### Preset_TrapExecute — 陷阱触发处决（金系）

```
PreCastNode → CastVFX(Cast) → Delay → ImpactVFX → Condition(随机)
  ├─ 命中:  Damage → Status → TerrainVFX → Terrain → PostCast → End
  └─ 未命中: → End
```

**适用**：金系地刺陷阱、地雷触发。

---

## Blackboard 通信

所有节点仅通过 Blackboard 进行通信。以下是标准通信模式：

### 模式一：覆盖伤害给下游节点

```
ModifyFloatNode（outputKey=DamageOverride，multiplier=2f）
       │
       ▼  写入 BBKey.DamageOverride = 修改后的值
DamageNode（damageAmount.Source=Blackboard，BlackboardKey=DamageOverride）
```

### 模式二：随机分支

```
RollChanceNode（outputKey=IsCrit）
       │
       ▼  写入 BBKey.IsCrit = true/false
ConditionNode（mode=BlackboardBool，bbKey=IsCrit）
  ├─ truePort → （暴击路径）
  └─ falsePort → （普通路径）
```

### 模式三：引导 tick 伤害

```
ChannelNode（每 tick）
       │
       ▼  写入 BBKey.DamageOverride = tick伤害值
       ▼  同时直接调用 IDamageable.TakeDamage(tickDamage, caster)
后续 DamageNode 可读取 DamageOverride 作为最后一击的参考
```

### 模式四：投射物命中

```
ProjectileNode
       │
       ▼  投射物飞行，检测命中
       ▼  写入 BBKey.ProjectileHitPosition = 命中位置
       ▼  写入 BBKey.ProjectileHitTarget = 目标名称
```

---

## 绑定参考

### FloatBinding — 三种来源

```csharp
// 从 SkillConfig CSV 读取
new FloatBinding {
    Source = FloatBinding.SourceType.SkillConfig,
    SkillField = SkillFloatField.Damage
}

// 从 Blackboard 读取（前一节点的输出）
new FloatBinding {
    Source = FloatBinding.SourceType.Blackboard,
    BlackboardKey = BBKey.DamageOverride,
    DefaultValue = 0f
}

// 字面值（硬编码 — 谨慎使用）
new FloatBinding {
    Source = FloatBinding.SourceType.Literal,
    LiteralValue = 2.5f
}
```

### StringBinding — 三种来源

```csharp
// 从 SkillConfig CSV 字段读取
new StringBinding {
    Source = StringBinding.SourceType.SkillConfigField,
    SkillConfigFieldName = nameof(SkillConfig.ImpactVFXKey)
}

// 从 Blackboard 读取
new StringBinding {
    Source = StringBinding.SourceType.Blackboard,
    BlackboardKey = BBKey.CurrentGraph
}

// 字面值
new StringBinding {
    Source = StringBinding.SourceType.Literal,
    LiteralValue = "EnergyAbsorb"
}
```

### SkillFloatField 枚举

用于 `FloatBinding.SourceType.SkillConfig` 的可用字段：

| 枚举值 | CSV 列 | 典型范围 |
|--------|--------|---------|
| `Damage` | damage | 60–240 |
| `DamageRate` | damage_rate | 0.9–1.85 |
| `Cooldown` | cooldown | 1.8–8.0 |
| `CastRange` | cast_range | 5.5–9.0 |
| `DelaySeconds` | delay_seconds | 0.05–0.18 |
| `CritChance` | crit_chance | 0.13–0.50 |
| `Radius` | radius | 2.2–5.5 |
| `ChainCount` | chain_count | 0–5 |
| `VFXDuration` | vfx_duration | 0.35–0.85 |
| `CastTime` | cast_time | 0.12–0.28 |
| `ChannelDuration` | channel_duration | 0（非引导型） |
| `PostCastTime` | post_cast_time | 0.08–0.20 |
| `ProjectileSpeed` | projectile_speed | 0（无投射物） |
| `ResourceCost` | resource_cost | 15–40 |

---

## 建议的公共子图

- `Common_ImpactDamage` — 命中 VFX + 伤害 + 状态
- `Common_StatusPrime` — 纯状态施加
- `Common_ExecuteCheck` — 条件判断 + 处决反应逻辑
- `Common_RowPulse` — 共鸣 + 光束 VFX
- `Common_TerrainPaint` — 地形标记施加

---

## 命名规范

| 类型 | 模式 | 示例 |
|------|------|------|
| 配方图 | `{RecipeId}_{Recipe}` | `10001_FI-FI` |
| 公共子图 | `Common_{用途}` | `Common_ImpactDamage` |
| Blackboard 键名 | PascalCase 常量 | `DamageOverride`、`IsPreCasting` |
| VFX 键名 | PascalCase 语义名称 | `HitSpark`、`ArcBeam`、`EnergyAbsorb` |

---

## 设计规则

1. **优先使用 `FloatBinding`，而非字面值。** 字面值仅应用于表示游戏设计常量的倍率（如 2× 暴击），不得用于配置数据。

2. **优先使用 `SubGraphNode`，而非手动复制同一分支。** 如果两条配方共享逻辑，提取到公共子图中。

3. **保持一张图聚焦一个技能定位。** 将重复的节点链移至 `Common_*` 共享资产。

4. **禁止在未向 `BBKey.cs` 添加常量前，新增自定义 Blackboard 字符串键名。** 所有键名必须可通过代码补全发现。

5. **释放管线节点（PreCast、Channel、PostCast）由生成器自动插入。** 除非正在构建完全自定义的图，否则请勿手动增删这些节点。

6. **当 `ChannelDuration = 0` 时，ChannelNode 为空操作。** 即使非引导型技能图中存在该节点也是安全的。
