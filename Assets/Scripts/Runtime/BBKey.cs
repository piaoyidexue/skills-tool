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

    /// <summary>生成 ChannelTick_{n} 键名</summary>
    public static string ChannelTick(int tickIndex) => $"ChannelTick_{tickIndex}";
}
