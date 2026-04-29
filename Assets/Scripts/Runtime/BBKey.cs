public static class BBKey
{
    public const string DamageOverride = "DamageOverride";
    public const string DelayOverride = "DelayOverride";
    public const string IsCrit = "IsCrit";
    public const string LastDamage = "LastDamage";
    public const string BranchCount = "BranchCount";
    public const string TargetDistance = "TargetDistance";
    public const string CurrentGraph = "CurrentGraph";
    public const string StatusTags = "StatusTags";
    public const string TerrainTags = "TerrainTags";
    public const string ResonanceTags = "ResonanceTags";
    public const string ReactionSummary = "ReactionSummary";
    public const string RecipeId = "RecipeId";
    public const string HasResonance = "HasResonance";

    // -- cast pipeline --
    public const string IsPreCasting = "IsPreCasting";
    public const string IsPostCasting = "IsPostCasting";
    public const string IsChanneling = "IsChanneling";
    public const string IsInterrupted = "IsInterrupted";
    public const string InterruptReason = "InterruptReason";
    public const string PreCastTime = "PreCastTime";
    public const string PostCastTime = "PostCastTime";
    public const string PreCastProgress = "PreCastProgress";
    public const string PostCastProgress = "PostCastProgress";

    // -- channel --
    public const string ChannelProgress = "ChannelProgress";
    public const string ChannelDuration = "ChannelDuration";
    public const string ChannelCurrentTick = "ChannelCurrentTick";
    public const string ChannelTotalTicks = "ChannelTotalTicks";

    // -- projectile --
    public const string ProjectileActive = "ProjectileActive";
    public const string ProjectileHitPosition = "ProjectileHitPosition";
    public const string ProjectileHitTarget = "ProjectileHitTarget";

    // -- target query --
    public const string TargetCount = "TargetCount";
    public const string TargetList = "TargetList";

    // -- skill config injection --
    public const string SkillID = "SkillID";
    public const string DamagePercent = "DamagePercent";
    public const string CritChance = "CritChance";

    // -- animation sync --
    /// <summary>动画事件标识（如 "OnHit", "OnCastStart", "OnCastEnd"）</summary>
    public const string AnimEvent = "AnimEvent";
    /// <summary>上次动画事件触发时间</summary>
    public const string AnimLastEventTime = "AnimLastEventTime";
    /// <summary>当前动画标准化时间 (0-1)</summary>
    public const string AnimNormalizedTime = "AnimNormalizedTime";
    /// <summary>动画是否正在播放</summary>
    public const string AnimIsPlaying = "AnimIsPlaying";
    /// <summary>命中帧标记（单帧有效，读取后由节点清除）</summary>
    public const string AnimOnHit = "AnimOnHit";
    /// <summary>动画结束标记</summary>
    public const string AnimOnCastEnd = "AnimOnCastEnd";

    /// <summary>生成 ChannelTick_{n} 键名</summary>
    public static string ChannelTick(int tickIndex) => $"ChannelTick_{tickIndex}";
}
