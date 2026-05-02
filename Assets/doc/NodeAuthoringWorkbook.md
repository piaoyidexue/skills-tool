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
3. **禁止裸 Blackboard 字符串**（必须在 `BBKey.cs` 中定义常量，或使用 `BBKeyRef` 声明式引用）
4. **动画对齐使用 `AnimationEventWaitNode`**（避免用 DelayNode 猜测帧时间）
5. **伤害走 `DamagePipeline.CalculateAndApply()`**（避免直调 `IDamageable.TakeDamage`）
6. **技能图只管行为触发+标签传递**，不管规则结算（维度5）
7. **节点间禁止直接传参**，通过黑板解耦（维度4）
8. **规则级逻辑抽到管线**，如元素反应用 `TagDamageRule` 注册（维度5）

---

## 5维度复用工作流

### 维度1：子图复用工作流

**场景**：多个技能共享相同的伤害处理流程。

**步骤**：

1. 创建公共子图资产 `Common_ImpactDamage.asset`：
   ```
   StartNode → AnimationEventWaitNode(OnHit) → ApplyEffectNode → PlayVFXNode → EndNode
   ```

2. 在具体技能图中使用 SubGraphNode：
   - 拖入 `Common_ImpactDamage` 到 `subGraph` 字段
   - 配置 `inputMappings`：父图 `DamageOverride` → 子图 `DamageOverride`
   - 配置 `outputMappings`：子图 `IsCrit` → 父图 `CustomBool1`

3. 技能图结构变为：
   ```
   PreCast → SubGraph(Common_ImpactDamage) → [特殊逻辑] → PostCast → End
   ```

### 维度2：数据绑定工作流

**场景**：同一节点模板在不同技能中需要不同的参数值。

**步骤**：

1. 节点内部使用 Binding 声明参数来源：
   ```csharp
   // DamageNode 使用 FloatBinding
   public FloatBinding damage = new() { Source = SkillConfig, SkillField = SkillFloatField.Damage };
   ```

2. 策划在 Skill.csv 中填入对应列的值：
   ```csv
   skill_id,name,damage,crit_chance,...
   1001,Fireball,50,0.2,...
   1002,IceLance,35,0.35,...
   ```

3. 运行时自动从 CSV 读取，节点无需修改。

**BoolBinding 使用场景**：

```csharp
// ConditionNode 使用 BoolBinding 判断
new BoolBinding { Source = InvertedBlackboard, BlackboardKey = BBKey.IsInterrupted }
// 等价于：!ctx.Blackboard.GetBool(BBKey.IsInterrupted)
```

### 维度3：图模板生成工作流

**场景**：需要快速创建一个标准拓扑的技能图。

**步骤**：

1. 打开 `Assets/Skill System/Generate Graph from Preset...`
2. 选择预设类型（如 `ImpactLane`）
3. 配置可选模块：
   - ☐ 条件分支（暴击/概率）
   - ☐ EQS目标查询
   - ☐ 元素标签
   - ☐ 地形涂绘
   - ☐ 共鸣节点
4. 输入资产名称，点击"生成技能图"
5. 自动生成完整连线的 SkillGraphAsset，仅需微调特殊节点

**批量生成**：从 SkillRecipe.csv 读取配方，一键生成多个技能图。

### 维度4：黑板解耦工作流

**场景**：不同技能的目标获取方式不同，但伤害逻辑一样。

**步骤**：

1. 使用 BBKeyRef 声明式引用黑板键：
   ```csharp
   // TargetQueryNode 中
   public BBKeyRef countKeyRef = new(BBKeyRefMode.Predefined, BBKey.TargetCount);
   public BBKeyRef listKeyRef = new(BBKeyRefMode.Predefined, BBKey.TargetList);
   ```

2. 写入端（EQSNode）和读取端（DamageNode）完全解耦：
   ```
   EQSNode 写入 TargetList → 黑板中转 → DamageNode 读取 TargetList
   ```

3. 使用 Custom 数据流键传递节点间临时数据：
   - `CustomBool1/2` — 布尔标记（如分支结果）
   - `CustomFloat1/2` — 浮点数值（如距离、倍率）
   - `CustomString1/2` — 字符串数据（如事件名、路径）

4. 调试时开启写入追踪：
   ```csharp
   ctx.Blackboard.EnableWriteTracing = true;
   var trace = ctx.Blackboard.GetWriteTrace(); // 查看每个键的写入来源
   ```

### 维度5：标签驱动规则工作流

**场景**：火系打冰冻目标需要伤害翻倍（元素反应）。

**步骤**：

1. 在 ApplyEffectNode 中声明标签：
   ```
   extraTags = "element.fire"
   ```

2. 在游戏初始化时注册规则（数据驱动）：
   ```csharp
   DamagePipeline.RegisterTagRule(new TagDamageRule
   {
       RuleName = "MeltReaction",
       RequiredSourceTag = "element.fire",
       RequiredTargetTag = "status.chill",
       DamageMultiplier = 2.0f
   });
   ```

3. 技能图不需要任何特殊节点，DamagePipeline 自动处理。

**完整传递链**：

```text
ApplyEffectNode.extraTags = "element.fire"
  → EffectContext.AddSourceTag("element.fire")
    → DamagePipeline.ApplyTagRules()
      → 匹配 RequiredSourceTag="element.fire" + RequiredTargetTag="status.chill"
        → value *= 2.0f（融化反应）
```

---

## 5维度速查表

| 我想... | 用什么 | 在哪 |
|---------|--------|------|
| 复用一段连线逻辑 | `SubGraphNode` + `BBMapping` | 节点图 / Flow 目录 |
| 让同一节点读不同值 | `FloatBinding` / `BoolBinding` | 节点 Inspector / Core/Binding |
| 一键生成技能图 | `GraphPreset` + 生成器窗口 | Assets/Skill System/ 菜单 |
| 解耦节点间传参 | `BBKeyRef` + `Custom*` 键 | 节点 Inspector / Runtime/BBKeyRef |
| 添加元素反应规则 | `TagDamageRule` + `extraTags` | DamagePipeline / ApplyEffectNode |
