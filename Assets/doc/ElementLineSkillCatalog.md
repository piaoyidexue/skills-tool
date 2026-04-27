# 《元素阵线》技能总表

本文档用于把《元素阵线》的元素塔防设计，映射到当前工程的 `CSV + NodeCanvas` 技能系统中，方便策划、程序、美术协同制作。

---

## 〇、技能释放全生命周期

每个技能从触发到完成，经历以下**权威释放管线**（由 `SkillCaster.CastPipeline` 协程驱动）：

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

**CSV 控制参数**（每行在 `Skill.csv` 中）：

| 参数 | 含义 | 默认值 |
|------|------|--------|
| `cast_time` | 前摇时长（s） | 0.12~0.28 |
| `channel_duration` | 引导持续时长（s，0=无引导） | 0 |
| `post_cast_time` | 后摇时长（s） | 0.08~0.20 |
| `interruptible` | 是否可被打断 | true |
| `resource_cost` | 资源消耗 | 15~40 |

---

## 一、基础规则

- 第 1 槽：核心元素（FI/GL/FU/AC），由本局起源决定
- 第 2 槽：决定攻击形态
- 第 3 槽：决定协同逻辑
- 第 4 槽：决定规则质变或奥义重写

### 攻击形态 → 图预设 + 节点选择

| 攻击形态 | 推荐 node_preset_id | 是否插入 ChannelNode | 典型 CSV 参数 |
|----------|-------------------|---------------------|---------------|
| 直线穿透 / 高速投射 | `Preset_ImpactLane` | ❌ | `channel_duration=0` |
| 持续光束 / 引导射线 | `Preset_BeamLane` | ✅ | `channel_duration>0`, `tickInterval=0.3` |
| 命中弹跳 / 长距离雷链 | `Preset_ConductiveChain` | ✅ | `channel_duration>0`, `chain_count>0` |
| 重型炮弹 / 处决爆发 | `Preset_CritBranch` | ❌ | `crit_chance≥0.36` |
| 地刺陷阱 / 地雷 | `Preset_TrapExecute` | ❌ | `cast_range≤5.5` |
| 分叉射线 / 扩散 | `Preset_RowResonance` | ❌ | `radius>2.2` |
| 状态增幅 / 反应爆发 | `Preset_StatusAmplify` / `Preset_ReactionBurst` | ❌ | — |
| 4 槽奥义 | `Preset_ElementCollapse` / `Preset_ExecuteUltimate` / `Preset_ChainUltimate` / `Preset_TerrainUltimate` | ❌ | `vfx_duration=0.85`, `cooldown=8` |

### 推荐制作流程

1. 在 `Resources/Config/SkillRecipe.csv` 中选择一条 `RecipeId`
2. 按 `node_preset_id` 选择对应节点模板
3. 运行 `Tools/Skills/Templates/根据配方生成运行时技能配置` → 自动填充 Skill.csv
4. 运行 `Tools/Skills/Templates/根据配置生成元素阵线技能图` → 自动生成技能图
5. 在 NodeCanvas 编辑器中仅微调绑定和参数，不重复造轮子

---

## 二、二槽攻击形态

### 1. 火核心（FI）

| 配方 | 名称 | 攻击形态 | 预设 | 引导 | 战术定位 |
|---|---|---|---|---|---|
| FI-FI | 烈阳长枪 | 直线穿透火矛 | `Preset_ImpactLane` | ❌ | 单路稳定消耗 |
| FI-GL | 蒸汽烙印 | 高速火弹，附带灼烧与寒蚀标记 | `Preset_ImpactLane` | ❌ | 挂状态起手 |
| FI-FU | 弧焰火种 | 命中后额外弹跳一次 | `Preset_ConductiveChain` | ✅ | 清杂与补刀兼顾 |
| FI-AC | 爆裂铆钉 | 低速重弹，高爆发上限 | `Preset_CritBranch` | ❌ | 精英斩杀 |

### 2. 冰核心（GL）

| 配方 | 名称 | 攻击形态 | 预设 | 引导 | 战术定位 |
|---|---|---|---|---|---|
| GL-FI | 熔霜射线 | 附带熔解伤害的持续光束 | `Preset_BeamLane` | ✅ | 反高血量前排 |
| GL-GL | 霜冻射线 | 纯减速引导射线 | `Preset_BeamLane` | ✅ | 主控制塔 |
| GL-FU | 极弧霜流 | 对被冰缓目标产生分叉 | `Preset_ConductiveChain` | ✅ | 多目标控场 |
| GL-AC | 晶刺爆发 | 近距离晶刺喷发 | `Preset_ImpactLane` | ❌ | 贴线拦截 |

### 3. 雷核心（FU）

| 配方 | 名称 | 攻击形态 | 预设 | 引导 | 战术定位 |
|---|---|---|---|---|---|
| FU-FI | 等离火种 | 带点燃效果的链式电火花 | `Preset_ConductiveChain` | ✅ | 波次压血 |
| FU-GL | 冰雹电流 | 反复弹射并附带减速脉冲 | `Preset_ConductiveChain` | ✅ | 人海控制 |
| FU-FU | 风暴继电 | 长距离连锁闪电 | `Preset_ConductiveChain` | ✅ | 群怪清理 |
| FU-AC | 轨电放逐 | 首次命中爆发极高 | `Preset_CritBranch` | ❌ | 单点狙杀 |

### 4. 金核心（AC）

| 配方 | 名称 | 攻击形态 | 预设 | 引导 | 战术定位 |
|---|---|---|---|---|---|
| AC-FI | 爆燃矛桩 | 地刺陷阱并点燃地格 | `Preset_TrapExecute` | ❌ | 收割陷阱 |
| AC-GL | 永霜地雷 | 触发后造成冻结积累 | `Preset_TrapExecute` | ❌ | 路口封锁 |
| AC-FU | 导电脊枪 | 重投枪，命中导电目标可反弹 | `Preset_CritBranch` | ❌ | 混合清线 |
| AC-AC | 断首钉桩 | 超高单点暴击伤害 | `Preset_TrapExecute` | ❌ | Boss 杀手 |

---

## 三、三槽协同配方

### 1. 火核心

| 配方 | 名称 | 触发条件 | 预设 | 效果 |
|---|---|---|---|---|
| FI-FI-GL | 灼雾 | 命中冰缓目标 | `Preset_StatusAmplify` | 灼烧持续时间翻倍 |
| FI-FI-FU | 链燃 | 命中燃烧目标 | `Preset_ConductiveChain` | 溅射并跳向最近单位 |
| FI-FI-AC | 炽核爆裂 | 暴击命中燃烧目标 | `Preset_CritBranch` | 立即引爆灼烧层数 |
| FI-GL-FU | 热暴风 | 冰面上的目标被命中 | `Preset_RowResonance` | 雷链范围翻倍 |
| FI-GL-AC | 碎锻 | 命中减速目标 | `Preset_CritBranch` | 暴击率必定命中 |

### 2. 冰核心

| 配方 | 名称 | 触发条件 | 预设 | 效果 |
|---|---|---|---|---|
| GL-GL-FI | 冷脉骤停 | 命中燃烧目标 | `Preset_ReactionBurst` | 消耗燃烧并触发冻结爆发 |
| GL-GL-FU | 极域网络 | 命中导电目标 | `Preset_RowResonance` | 射线横向分叉传播 |
| GL-GL-AC | 脆晶外壳 | 目标处于重度减速 | `Preset_StatusAmplify` | 使其承受更高暴击 |
| GL-FI-FU | 蒸压震荡 | 燃烧单位死亡 | `Preset_TerrainUltimate` | 留下蒸汽伤害区 |
| GL-FI-AC | 晶狱裁决 | 同时具备火与冰标记 | `Preset_ExecuteUltimate` | 处决阈值提升 |

### 3. 雷核心

| 配方 | 名称 | 触发条件 | 预设 | 效果 |
|---|---|---|---|---|
| FU-FU-FI | 雷炉 | 命中燃烧目标 | `Preset_ChainUltimate` | 每次弹跳附带爆炸 |
| FU-FU-GL | 绝对电流 | 命中冰缓目标 | `Preset_ConductiveChain` | 连锁次数 +2 |
| FU-FU-AC | 充能破袭 | 命中过载目标 | `Preset_CritBranch` | 最后一跳必暴击 |
| FU-FI-GL | 暴云雾场 | 同路存在火冰双状态 | `Preset_RowResonance` | 触发行脉冲 |
| FU-GL-AC | 静电牢笼 | 暴击减速目标 | `Preset_ReactionBurst` | 附带短暂眩晕 |

### 4. 金核心

| 配方 | 名称 | 触发条件 | 预设 | 效果 |
|---|---|---|---|---|
| AC-AC-FI | 处刑爆破 | 暴击燃烧目标 | `Preset_ExecuteUltimate` | 造成范围爆炸 |
| AC-AC-GL | 破冰裁断 | 暴击减速目标 | `Preset_ReactionBurst` | 消耗寒蚀并放出晶片波 |
| AC-AC-FU | 轨雷级联 | 暴击导电目标 | `Preset_ChainUltimate` | 触发雷链反弹 |
| AC-FI-GL | 淬火合金 | 站在焦土或冰面上的塔 | `Preset_TerrainUltimate` | 提升攻速 |
| AC-FI-FU | 超频穿枪 | 同行存在火/雷友塔 | `Preset_RowResonance` | 每第 3 次攻击穿透 |

---

## 四、四槽规则质变

### 1. 火核心奥义

| 配方 | 名称 | 规则质变 | 预设 |
|---|---|---|---|
| FI-FI-GL-FU | 元素坍缩 | 对整行造成范围清场并附加火冰雷三状态 | `Preset_ElementCollapse` |
| FI-FI-GL-AC | 岩浆裁断 | 对低血燃烧目标触发处决爆炸 | `Preset_ExecuteUltimate` |
| FI-FI-FU-AC | 日冕蓄电 | 每次引爆返还同行充能 | `Preset_ChainUltimate` |
| FI-GL-FU-AC | 熔炉灾变 | 焦土转化为导电熔炉地带 | `Preset_TerrainUltimate` |

### 2. 冰核心奥义

| 配方 | 名称 | 规则质变 | 预设 |
|---|---|---|---|
| GL-GL-FI-FU | 白夜寒潮 | 整路冻结，后续雷击全图分叉 | `Preset_ElementCollapse` |
| GL-GL-FI-AC | 晶廷裁决 | 脆化目标承受连锁暴击 | `Preset_CritBranch` |
| GL-GL-FU-AC | 棱镜冰墙 | 生成横跨多路的冰墙屏障 | `Preset_RowResonance` |
| GL-FI-FU-AC | 零度崩解 | 消耗全部状态转化为真实伤害爆发 | `Preset_ReactionBurst` |

### 3. 雷核心奥义

| 配方 | 名称 | 规则质变 | 预设 |
|---|---|---|---|
| FU-FU-FI-GL | 超胞回廊 | 多路风暴横扫并留下冻结电痕 | `Preset_ElementCollapse` |
| FU-FU-FI-AC | 裂穹标记 | 每第 4 次弹跳强化为轨道暴击 | `Preset_ChainUltimate` |
| FU-FU-GL-AC | 风暴囚笼 | 被链接目标共享暴击伤害 | `Preset_RowResonance` |
| FU-FI-GL-AC | 离子壁垒 | 相同行形成持续雷墙 | `Preset_RowResonance` |

### 4. 金核心奥义

| 配方 | 名称 | 规则质变 | 预设 |
|---|---|---|---|
| AC-AC-FI-GL | 灾厄桩阵 | 在整行已占格中生成爆裂晶桩 | `Preset_ElementCollapse` |
| AC-AC-FI-FU | 雷断头台 | 击杀后继续跃迁处决 | `Preset_ChainUltimate` |
| AC-AC-GL-FU | 绝对兵装 | 暴击陷阱继承冰雷反应 | `Preset_RowResonance` |
| AC-FI-GL-FU | 共振壁垒 | 相邻行形成反射型金属元素墙 | `Preset_RowResonance` |

---

## 五、推荐图模板

| 预设 | 用途 | 核心节点链 | 适用配方数 |
|------|------|-----------|-----------|
| `Preset_ImpactLane` | 标准单路命中型 | PreCast → CastVFX → Delay → ImpactVFX → Damage → PostCast | 7 |
| `Preset_BeamLane` | 持续射线/引导型 | PreCast → Channel → CastVFX → Delay → BeamVFX → Damage → PostCast | 3 |
| `Preset_CritBranch` | 暴击/处决分支型 | PreCast → CastVFX → Delay → RollChance → Condition(IsCrit) → PostCast | 8 |
| `Preset_ConductiveChain` | 连锁传导型 | PreCast → Channel → CastVFX → Delay → BeamVFX → Damage → Reaction → PostCast | 6 |
| `Preset_StatusAmplify` | 状态增幅型 | PreCast → CastVFX → Condition → ModifyFloat → Damage(useOverride) → PostCast | 3 |
| `Preset_ReactionBurst` | 反应爆发型 | PreCast → CastVFX → Condition → Reaction → Damage(useOverride) → PostCast | 5 |
| `Preset_RowResonance` | 行/跨行共鸣型 | PreCast → CastVFX → Delay → Resonance → SubGraph → Damage → PostCast | 8 |
| `Preset_TerrainUltimate` | 地表奥义型 | PreCast → CastVFX → Delay → ImpactVFX → Damage → Terrain → FinisherStaged → PostCast | 4 |
| `Preset_ChainUltimate` | 连锁奥义型 | PreCast → CastVFX → Delay → Parallel(BranchA+B) → FinisherStaged → PostCast | 5 |
| `Preset_ElementCollapse` | 元素坍缩/全行清场 | PreCast → CastVFX → Delay → Parallel(BranchA+B) → FinisherStaged → PostCast | 4 |
| `Preset_ExecuteUltimate` | 处决奥义型 | PreCast → CastVFX → Condition → Reaction → FinisherStaged → Damage(useOverride) → PostCast | 4 |
| `Preset_TrapExecute` | 陷阱处决型 | PreCast → CastVFX → Delay → ImpactVFX → Condition → Damage → PostCast | 3 |

> 所有 12 种预设开头均已自动插入 **PreCastNode**（前摇特效），末尾均已自动插入 **PostCastNode**（后摇特效）。含"射线"/"雷链"的配方（BeamLane、ConductiveChain 及其变体）自动插入 **ChannelNode**（引导 tick 伤害）。

---

## 六、建议内容规模

| 类别 | 数量 | 状态 |
|------|------|------|
| 二槽攻击形态 | 16 种 | ✅ 已生成 (CSV Rows 10001~40004) |
| 三槽协同逻辑 | 20 种 | ✅ 已生成 (CSV Rows 11001~41005) |
| 四槽规则质变 | 16 种 | ✅ 已生成 (CSV Rows 12001~42004) |
| 图预设模板 | 12 种 | ✅ NodePreset.csv |
| 地表形态 | 8 种 | ✅ Terrain.csv |
| 阵型共鸣 | 12 种 | ✅ 纳入 NodePreset |
| 同位素 | 30+ 种 | ✅ Isotope.csv |
| 外来元素 | 5 轮换主题 | ✅ GuestElement.csv |

---

## 七、元素→节点映射速查

| 元素 | 核心节点 | VFX 风格 | 地表格子 |
|------|---------|---------|----------|
| 🔥 火 (FI) | Damage × 1.2~1.85, PlayVFX(Impact)+CritBranch | HitSpark, ExplosionWave | scorch（焦土） |
| ❄️ 冰 (GL) | ChannelNode(Beam), Damage × 0.9~1.65, ApplyStatus(chill) | FrostBurst, LightningBeam | ice（冰面） |
| ⚡ 雷 (FU) | ChannelNode(Chain), Damage × 0.9~1.85, ReactionNode | ArcBeam, PrismBeam | metal（金属） |
| 🪙 金 (AC) | CritBranch+Execute, Damage × 1.0~1.85, PaintTerrain | HitSpark, BulwarkBeam | metal（金属） |

### 打断策略

| 元素 | 典型 interruptible | 战术含义 |
|------|------------------|---------|
| 火 | `true` | 所有二槽~四槽技能均可打断，依赖站位规避 |
| 冰 | `true` | 射线型前摇可打断，需提前布置冰面保护 |
| 雷 | `true` | 长连锁技能可打断，但传导已在途仍会继续 |
| 金 | `true` | 陷阱型前摇可打断，但已布置的地雷不受影响 |

---

## 八、相关文档

| 文档 | 用途 |
|------|------|
| `SkillDesignGuide.md` | 系统架构、完整节点库、CSV 字段说明、设计原则 |
| `NodeAuthoringWorkbook.md` | 节点搭建手册、预设对应关系、绑定规范、Blackboard 通信模式 |
| `Resources/Config/SkillRecipe.csv` | 52 条配方原始数据（二槽~四槽全覆盖） |
| `Resources/Config/NodePreset.csv` | 12 种预设模板定义 |
| `Resources/Config/Skill.csv` | 运行时加载的技能数值（单一数据源） |
