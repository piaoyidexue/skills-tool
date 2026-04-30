public static class BBKey
{
    // ============================================================
    //  GAS 架构红线：黑板仅允许存储图节点寻址所需的基础引用或控制计数器。
    //  禁止存储数值流转变量（如 DamageOverride）。
    //  禁止存储业务状态判定（如 IsCrit、ReactionSummary）。
    //  所有害数计算由 EffectSystem + Modifier Pipeline 接管。
    // ============================================================

    // ---- 允许：图控制计数器 ----
    public const string BranchCount = "BranchCount";
    public const string ExecutionCount = "ExecutionCount";

    // ---- 允许：目标引用（图寻址） ----
    public const string CurrentTarget = "CurrentTarget";
    public const string CurrentGraph = "CurrentGraph";
    public const string TargetCount = "TargetCount";
    public const string TargetList = "TargetList";
    public const string TargetDistance = "TargetDistance";

    // ---- 允许：施法管线状态（时间轴控制） ----
    public const string IsPreCasting = "IsPreCasting";
    public const string IsPostCasting = "IsPostCasting";
    public const string IsChanneling = "IsChanneling";
    public const string IsInterrupted = "IsInterrupted";
    public const string InterruptReason = "InterruptReason";
    public const string PreCastTime = "PreCastTime";
    public const string PostCastTime = "PostCastTime";
    public const string PreCastProgress = "PreCastProgress";
    public const string PostCastProgress = "PostCastProgress";

    // ---- 允许：通道控制 ----
    public const string ChannelProgress = "ChannelProgress";
    public const string ChannelDuration = "ChannelDuration";
    public const string ChannelCurrentTick = "ChannelCurrentTick";
    public const string ChannelTotalTicks = "ChannelTotalTicks";

    // ---- 允许：投射物寻址 ----
    public const string ProjectileActive = "ProjectileActive";
    public const string ProjectileHitPosition = "ProjectileHitPosition";
    public const string ProjectileHitTarget = "ProjectileHitTarget";

    // ---- 允许：技能配置注入（只读引用） ----
    public const string SkillID = "SkillID";

    // ---- 允许：动画同步标记 ----
    public const string AnimEvent = "AnimEvent";
    public const string AnimLastEventTime = "AnimLastEventTime";
    public const string AnimNormalizedTime = "AnimNormalizedTime";
    public const string AnimIsPlaying = "AnimIsPlaying";
    public const string AnimOnHit = "AnimOnHit";
    public const string AnimOnCastEnd = "AnimOnCastEnd";

    /// <summary>生成 ChannelTick_{n} 键名</summary>
    public static string ChannelTick(int tickIndex) => $"ChannelTick_{tickIndex}";

    // ============================================================
    //  以下键名已被 GAS 架构废弃，禁止在新代码中使用。
    //  保留定义仅为向后兼容旧序列化资产。
    // ============================================================

    [System.Obsolete("GAS红线：伤害覆写禁止存储在黑板，由 EffectSystem.ModifierPipeline 接管")]
    public const string DamageOverride = "DamageOverride";
    [System.Obsolete("GAS红线：暴击判定禁止存储在黑板，由 EffectSpec.IsCriticalHit 接管")]
    public const string IsCrit = "IsCrit";
    [System.Obsolete("GAS红线：最终伤害禁止存储在黑板，由 EffectSpec.CalculatedDamage 接管")]
    public const string LastDamage = "LastDamage";
    [System.Obsolete("GAS红线：状态标签禁止存储在黑板，由 GEHost.TagContainer 接管")]
    public const string StatusTags = "StatusTags";
    [System.Obsolete("GAS红线：地形标签禁止存储在黑板，由 PaintTerrainNode 直接使用")]
    public const string TerrainTags = "TerrainTags";
    [System.Obsolete("GAS红线：共鸣标签禁止存储在黑板，由 ResonanceNode 直接使用")]
    public const string ResonanceTags = "ResonanceTags";
    [System.Obsolete("GAS红线：反应摘要禁止存储在黑板，由 ReactionEngine 事件接管")]
    public const string ReactionSummary = "ReactionSummary";
    [System.Obsolete("GAS红线：配置ID只读引用，不应在黑板中流转")]
    public const string RecipeId = "RecipeId";
    [System.Obsolete("GAS红线：共鸣状态由 TagContainer 接管")]
    public const string HasResonance = "HasResonance";
    [System.Obsolete("GAS红线：延迟覆写禁止存储在黑板")]
    public const string DelayOverride = "DelayOverride";
    [System.Obsolete("GAS红线：伤害百分比由 GameplayEffectData.BaseDamage 接管")]
    public const string DamagePercent = "DamagePercent";
    [System.Obsolete("GAS红线：暴击率由 GameplayEffectData.CritChanceBonus 接管")]
    public const string CritChance = "CritChance";
}
