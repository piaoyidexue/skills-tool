# 《元素阵线》技能总表

本文档用于把《元素阵线》的元素塔防设计，映射到当前工程的 `CSV + CanvasCore + GE + EQS` 战斗框架中。

---

## 〇、技能释放全生命周期

每个技能从触发到完成，经历以下**权威释放管线**（Tick 驱动）：

```
玩家触发 TryCast()
  │
  ├─ 资源消耗（resource_cost）
  ├─ 前摇阶段（cast_time）── 可被打断 ── 播放 PreCast VFX
  ├─ 执行阶段 ── 运行技能图（PreCastNode → 核心节点链 → PostCastNode）
  │   ├─ ChannelNode（仅射线/雷链配方，逐 tick 施加伤害）
  │   └─ 可被打断（除非 CSV 中 interruptible=false）
  ├─ 后摇阶段（post_cast_time）── 不可打断 ── 播放 PostCast VFX
  └─ 冷却状态（cooldown）

被打断时：IsInterrupted=true → 图内节点退出 → 清理 VFX → 进入冷却
```

**CSV 控制参数**：

| 参数 | 含义 | 默认值 |
|------|------|--------|
| `cast_time` | 前摇时长（s） | 0.12~0.28 |
| `channel_duration` | 引导持续时长（s，0=无引导） | 0 |
| `post_cast_time` | 后摇时长（s） | 0.08~0.20 |
| `interruptible` | 是否可被打断 | true |

---

## 一、基础规则

- 第 1 槽：核心元素（FI/GL/FU/AC），由本局起源决定
- 第 2 槽：决定攻击形态
- 第 3 槽：决定协同逻辑
- 第 4 槽：决定规则质变或奥义重写

### 攻击形态 → 图预设 + 节点选择

| 攻击形态 | 推荐 node_preset_id | 是否插入 ChannelNode |
|----------|-------------------|---------------------|
| 直线穿透 / 高速投射 | `Preset_ImpactLane` | ❌ |
| 持续光束 / 引导射线 | `Preset_BeamLane` | ✅ |
| 命中弹跳 / 长距离雷链 | `Preset_ConductiveChain` | ✅ |
| 重型炮弹 / 处决爆发 | `Preset_CritBranch` | ❌ |
| 地刺陷阱 / 地雷 | `Preset_TrapExecute` | ❌ |
| 分叉射线 / 扩散 | `Preset_RowResonance` | ❌ |
| 状态增幅 / 反应爆发 | `Preset_StatusAmplify` / `Preset_ReactionBurst` | ❌ |
| 4 槽奥义 | `Preset_ElementCollapse` / `Preset_ExecuteUltimate` / `Preset_ChainUltimate` / `Preset_TerrainUltimate` | ❌ |

### 推荐制作流程

1. 在 `Resources/Config/SkillRecipe.csv` 中选择一条 `RecipeId`
2. 运行 `Tools/Skills/Templates/根据配方生成运行时技能配置` → 自动填充 Skill.csv
3. 运行 `Tools/Skills/Templates/根据配置生成元素阵线技能图` → 自动生成技能图
4. 在 CanvasCore 编辑器中仅微调绑定和参数

---

## 二、元素→节点映射速查

| 元素 | 核心节点 | VFX 风格 | 地表格子 | GE Tag 前缀 |
|------|---------|---------|----------|------------|
| 🔥 火 (FI) | Damage × 1.2~1.85, PlayVFX(Impact)+CritBranch | HitSpark, ExplosionWave | scorch（焦土） | `tag.status.burn` |
| ❄️ 冰 (GL) | ChannelNode(Beam), Damage × 0.9~1.65, ApplyStatus(chill) | FrostBurst, LightningBeam | ice（冰面） | `tag.status.chill` |
| ⚡ 雷 (FU) | ChannelNode(Chain), Damage × 0.9~1.85, ReactionNode | ArcBeam, PrismBeam | metal（金属） | `tag.status.conductive` |
| 🪙 金 (AC) | CritBranch+Execute, Damage × 1.0~1.85, PaintTerrain | HitSpark, BulwarkBeam | metal（金属） | `tag.status.mark` |

---

## 三、状态系统（GE 驱动）

元素状态不再由节点硬编码处理，而是通过 **GameplayEffect 系统**统一管理：

| 状态 | GE Modifier 效果 | Duration |
|------|-----------------|----------|
| `tag.status.burn` | DamagePerTick Add + Period=1s（持续伤害） | 3s |
| `tag.status.chill` | MoveSpeed Multiply（减速 25%） | 2.5s |
| `tag.status.freeze` | MoveSpeed + AttackSpeed Override=0（硬控） | 1.2s |
| `tag.status.conductive` | DamageTakenMultiplier Multiply（易伤） | 3s |
| `tag.status.mark` | DamageTakenMultiplier Multiply（易伤 15%） | 2s |
| `tag.status.stun` | MoveSpeed + AttackSpeed Override=0 | 0.45s |

### 元素反应（DamagePipeline 事件拦截）

```
目标有 tag.status.chill + 施放火系技能（tags="fire"）
  → DamagePipeline.OnPreCalculate 检测 host.HasTag("tag.status.chill") && tags 含 "fire"
    → ctx.Value *= 1.5f（融化反应 1.5x）
```

---

## 四、推荐图模板

| 预设 | 用途 | 核心节点链 | 配方数 |
|------|------|-----------|--------|
| `Preset_ImpactLane` | 标准单路命中 | PreCast → CastVFX → Delay → ImpactVFX → Damage → PostCast | 7 |
| `Preset_BeamLane` | 持续射线/引导 | PreCast → Channel → CastVFX → Delay → BeamVFX → Damage → PostCast | 3 |
| `Preset_CritBranch` | 暴击/处决分支 | PreCast → CastVFX → Delay → RollChance → Condition → PostCast | 8 |
| `Preset_ConductiveChain` | 连锁传导 | PreCast → Channel → CastVFX → Delay → BeamVFX → Damage → Reaction → PostCast | 6 |
| `Preset_StatusAmplify` | 状态增幅 | PreCast → CastVFX → Condition → ModifyFloat → Damage(useOverride) → PostCast | 3 |
| `Preset_ReactionBurst` | 反应爆发 | PreCast → CastVFX → Condition → Reaction → Damage(useOverride) → PostCast | 5 |
| `Preset_RowResonance` | 行/跨行共鸣 | PreCast → CastVFX → Delay → Resonance → SubGraph → Damage → PostCast | 8 |
| `Preset_TerrainUltimate` | 地表奥义 | PreCast → CastVFX → Delay → ImpactVFX → Damage → Terrain → FinisherStaged → PostCast | 4 |
| `Preset_ChainUltimate` | 连锁奥义 | PreCast → CastVFX → Delay → Parallel → FinisherStaged → PostCast | 5 |
| `Preset_ElementCollapse` | 元素坍缩/清场 | PreCast → CastVFX → Delay → Parallel → FinisherStaged → PostCast | 4 |
| `Preset_ExecuteUltimate` | 处决奥义 | PreCast → CastVFX → Condition → Reaction → FinisherStaged → Damage → PostCast | 4 |
| `Preset_TrapExecute` | 陷阱处决 | PreCast → CastVFX → Delay → ImpactVFX → Condition → Damage → PostCast | 3 |

---

## 五、内容规模

| 类别 | 数量 | 状态 |
|------|------|------|
| 二槽攻击形态 | 16 种 | ✅ |
| 三槽协同逻辑 | 20 种 | ✅ |
| 四槽规则质变 | 16 种 | ✅ |
| 图预设模板 | 12 种 | ✅ |
| 地表形态 | 8 种 | ✅ |
| 同位素 | 30+ 种 | ✅ |

---

## 六、相关文档

| 文档 | 用途 |
|------|------|
| [readme.md](readme.md) | 项目概述、架构、技术栈 |
| [SkillDesignGuide.md](SkillDesignGuide.md) | 完整设计说明 — 节点目录、BBKey、GE/EQS/动画系统、CSV 字段 |
| [NodeAuthoringWorkbook.md](NodeAuthoringWorkbook.md) | 节点搭建手册 — 预设→图流程、绑定规范、动画同步流程 |
| [AIModuleDocumentation.md](AIModuleDocumentation.md) | AI 行为树 — CSV 驱动生成、Action/Condition、感知系统 |
| `Resources/Config/SkillRecipe.csv` | 52 条配方原始数据 |
| `Resources/Config/Skill.csv` | 运行时技能数值（单一数据源） |
| `Resources/Config/Buff.csv` | GE/Buff 配置 |
