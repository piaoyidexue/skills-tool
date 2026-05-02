using System.Collections.Generic;

/// <summary>
///     技能浮点数字段枚举
/// </summary>
public enum SkillFloatField
{
    /// <summary>伤害值</summary>
    Damage,

    /// <summary>伤害倍率</summary>
    DamageRate,

    /// <summary>冷却时间</summary>
    Cooldown,

    /// <summary>施法范围</summary>
    CastRange,

    /// <summary>延迟时间(秒)</summary>
    DelaySeconds,

    /// <summary>暴击几率</summary>
    CritChance,

    /// <summary>半径/作用范围</summary>
    Radius,

    /// <summary>连锁次数</summary>
    ChainCount,

    /// <summary>特效持续时间</summary>
    VFXDuration,

    /// <summary>施法前腰时间(秒)</summary>
    CastTime,

    /// <summary>吟唱持续时间(秒), 0=非吟唱</summary>
    ChannelDuration,

    /// <summary>后摇/收招时间(秒)</summary>
    PostCastTime,

    /// <summary>投射物速度, 0=非投射物</summary>
    ProjectileSpeed,

    /// <summary>资源消耗(法力/能量等)</summary>
    ResourceCost
}

/// <summary>
///     技能配置类，用于存储和管理技能的各项属性配置
/// </summary>
public class SkillConfig
{
    /// <summary>起手特效键名</summary>
    public string CastVFXKey;

    /// <summary>光束特效键名</summary>
    public string BeamVFXKey;

    /// <summary>施法范围</summary>
    public float CastRange;

    /// <summary>连锁次数</summary>
    public float ChainCount;

    /// <summary>冷却时间(秒)</summary>
    public float Cooldown;

    /// <summary>暴击几率(0-1)</summary>
    public float CritChance;

    /// <summary>伤害值</summary>
    public float Damage;

    /// <summary>伤害倍率</summary>
    public float DamageRate;

    /// <summary>延迟时间(秒)</summary>
    public float DelaySeconds;

    /// <summary>技能图表路径</summary>
    public string GraphPath;

    /// <summary>命中特效键名</summary>
    public string ImpactVFXKey;

    /// <summary>终结特效键名</summary>
    public string FinisherVFXKey;

    /// <summary>反应特效键名</summary>
    public string ReactionVFXKey;

    /// <summary>半径/作用范围</summary>
    public float Radius;

    /// <summary>技能ID</summary>
    public int SkillID;

    /// <summary>技能名称</summary>
    public string SkillName;

    /// <summary>地表特效键名</summary>
    public string TerrainVFXKey;

    /// <summary>特效风格包键名</summary>
    public string VFXProfileKey;

    /// <summary>特效持续时间(秒)</summary>
    public float VFXDuration;

    /// <summary>视觉说明标签</summary>
    public string VisualHook;

    /// <summary>视觉设计备注</summary>
    public string VisualNotes;

    /// <summary>视觉主题</summary>
    public string VisualTheme;

    /// <summary>施法前腰时间(秒)</summary>
    public float CastTime;

    /// <summary>吟唱持续时间(秒), 0=非吟唱</summary>
    public float ChannelDuration;

    /// <summary>后摇/收招时间(秒)</summary>
    public float PostCastTime;

    /// <summary>是否可打断(前腰/吟唱期间受控即中断)</summary>
    public bool IsInterruptible;

    /// <summary>投射物配置 Key（引用 ProjectileConfig.csv 中的 projectile_id）</summary>
    public string ProjectileConfigKey;

    /// <summary>投射物速度, 0=非投射物</summary>
    public float ProjectileSpeed;

    /// <summary>投射物预制体 Key</summary>
    public string ProjectilePrefab;

    /// <summary>投射物弹道类型（0=直线, 1=追踪, 2=抛物线）</summary>
    public int ProjectileTrajectory;

    /// <summary>投射物命中判定半径</summary>
    public float ProjectileHitRadius = 0.5f;

    /// <summary>投射物最大存活时间（秒）</summary>
    public float ProjectileLifetime = 5f;

    /// <summary>投射物抛物线重力系数（Trajectory=2 时生效）</summary>
    public float ProjectileGravity = 9.8f;

    /// <summary>投射物携带的元素/状态标签（如 "element.fire|element.ice"），命中时透传给 DamagePipeline 触发元素反应</summary>
    public List<string> ProjectileTags = new();

    /// <summary>资源消耗(法力/能量等)</summary>
    public float ResourceCost;

    /// <summary>
    ///     根据字段类型获取对应的浮点数值
    /// </summary>
    /// <param name="field">技能浮点数字段类型</param>
    /// <returns>对应的字段值，如果字段不存在则返回0</returns>
    public float GetFloat(SkillFloatField field)
    {
        switch (field)
        {
            case SkillFloatField.Damage:
                return Damage;
            case SkillFloatField.DamageRate:
                return DamageRate;
            case SkillFloatField.Cooldown:
                return Cooldown;
            case SkillFloatField.CastRange:
                return CastRange;
            case SkillFloatField.DelaySeconds:
                return DelaySeconds;
            case SkillFloatField.CritChance:
                return CritChance;
            case SkillFloatField.Radius:
                return Radius;
            case SkillFloatField.ChainCount:
                return ChainCount;
            case SkillFloatField.VFXDuration:
                return VFXDuration;
            case SkillFloatField.CastTime:
                return CastTime;
            case SkillFloatField.ChannelDuration:
                return ChannelDuration;
            case SkillFloatField.PostCastTime:
                return PostCastTime;
            case SkillFloatField.ProjectileSpeed:
                return ProjectileSpeed;
            case SkillFloatField.ResourceCost:
                return ResourceCost;
            default:
                return 0f;
        }
    }

    /// <summary>
    ///     根据字段名称获取对应的字符串值
    /// </summary>
    /// <param name="fieldName">字段名称</param>
    /// <returns>对应的字符串值，如果字段不存在则返回空字符串</returns>
    public string GetString(string fieldName)
    {
        switch (fieldName)
        {
            case nameof(GraphPath):
                return GraphPath;
            case nameof(CastVFXKey):
                return CastVFXKey;
            case nameof(ImpactVFXKey):
                return ImpactVFXKey;
            case nameof(BeamVFXKey):
                return BeamVFXKey;
            case nameof(ReactionVFXKey):
                return ReactionVFXKey;
            case nameof(TerrainVFXKey):
                return TerrainVFXKey;
            case nameof(FinisherVFXKey):
                return FinisherVFXKey;
            case nameof(VFXProfileKey):
                return VFXProfileKey;
            case nameof(SkillName):
                return SkillName;
            case nameof(VisualTheme):
                return VisualTheme;
            case nameof(VisualHook):
                return VisualHook;
            case nameof(ProjectilePrefab):
                return ProjectilePrefab;
            case nameof(VisualNotes):
                return VisualNotes;
            default:
                return string.Empty;
        }
    }
}
