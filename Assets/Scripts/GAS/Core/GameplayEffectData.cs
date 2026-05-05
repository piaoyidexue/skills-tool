using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  游戏效果数据 (GameplayEffectData)
//  纯数据结构，可由 CSV/Luban 直接反序列化。
//  只描述"将要发生什么"，禁止包含任何业务执行逻辑。
// ============================================================

/// <summary>
///     游戏效果数据 —— 纯数据载体，CSV/Luban 驱动。
///     只描述"将要发生什么"，不包含任何执行逻辑。
/// </summary>
[Serializable]
public class GameplayEffectData
{
    // ---- 基础标识 ----
    [Tooltip("效果唯一ID（对应配置表）")]
    public int EffectId;

    [Tooltip("效果名称（用于日志/调试）")]
    public string EffectName = string.Empty;

    // ---- 持续时间策略 ----
    [Tooltip("持续时间策略：0=瞬间, 1=有持续时间, 2=无限")]
    public GEDurationPolicy DurationPolicy = GEDurationPolicy.Instant;

    /// <summary>持续时间（秒），HasDuration 时有效</summary>
    public float Duration = 0f;

    /// <summary>周期（秒），0=无周期效果</summary>
    public float Period = 0f;

    // ---- 数值属性 ----
    /// <summary>基础伤害值</summary>
    public float BaseDamage = 0f;

    /// <summary>基础治疗值</summary>
    public float BaseHealing = 0f;

    /// <summary>属性缩放系数（用于技能等级缩放）</summary>
    public float ScalingFactor = 1f;

    /// <summary>暴击率加成（百分比，0-1）</summary>
    public float CritChanceBonus = 0f;

    /// <summary>暴击伤害倍率</summary>
    public float CritMultiplier = 2f;

    // ---- 标签系统 ----
    /// <summary>此效果授予的标签列表</summary>
    public List<string> GrantedTags = new();

    /// <summary>此效果需要目标拥有的标签（缺失则无法生效）</summary>
    public List<string> RequiredTargetTags = new();

    /// <summary>此效果需要施法者拥有的标签</summary>
    public List<string> RequiredSourceTags = new();

    /// <summary>此效果免疫的标签（目标有此标签则无效）</summary>
    public List<string> ImmuneTags = new();
    
    // 【新增】在解析 CSV 时填充此列表
    public List<GEModifier> Modifiers = new List<GEModifier>(); 

    // ---- 叠加策略 ----
    [Tooltip("叠加策略：0=刷新, 1=增加层数, 2=忽略")]
    public GEStackPolicy StackPolicy = GEStackPolicy.Refresh;

    [Tooltip("最大叠加层数")]
    public int MaxStacks = 1;

    // ---- 表现层关联 ----
    /// <summary>命中特效 Key</summary>
    public string ImpactVFXKey = string.Empty;

    /// <summary>持续特效 Key（用于有持续时间的效果）</summary>
    public string LoopVFXKey = string.Empty;

    /// <summary>移除特效 Key</summary>
    public string RemoveVFXKey = string.Empty;

    /// <summary>音效 Key</summary>
    public string SFXKey = string.Empty;

    // ---- 区域效果 ----
    /// <summary>是否为区域效果</summary>
    public bool IsAreaOfEffect = false;

    /// <summary>区域半径</summary>
    public float AreaRadius = 0f;

    /// <summary>区域中心偏移（相对于目标点）</summary>
    public Vector3 AreaOffset = Vector3.zero;

    /// <summary>区域最大目标数（0=无限制）</summary>
    public int AreaMaxTargets = 0;

    // ---- 特殊标志 ----
    [Tooltip("是否无视无敌状态")]
    public bool IgnoreInvulnerability = false;

    [Tooltip("是否穿透护盾优先扣血")]
    public bool BypassShields = false;

    [Tooltip("是否可以被打断")]
    public bool IsInterruptible = false;

    // ============================================================
    //  便捷方法
    // ============================================================

    /// <summary>
    ///     计算技能等级缩放后的伤害值。
    /// </summary>
    public float GetScaledDamage(int abilityLevel)
    {
        return BaseDamage * (1f + (abilityLevel - 1) * ScalingFactor);
    }

    /// <summary>
    ///     计算技能等级缩放后的治疗值。
    /// </summary>
    public float GetScaledHealing(int abilityLevel)
    {
        return BaseHealing * (1f + (abilityLevel - 1) * ScalingFactor);
    }

    /// <summary>
    ///     检查是否包含指定授予标签。
    /// </summary>
    public bool HasGrantedTag(string tag)
    {
        if (GrantedTags == null || string.IsNullOrEmpty(tag)) return false;
        foreach (var t in GrantedTags)
            if (string.Equals(t, tag, StringComparison.OrdinalIgnoreCase) ||
                t.StartsWith(tag + ".", StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    /// <summary>
    ///     检查所有必需的标签是否满足。
    /// </summary>
    public bool CheckRequirements(EffectContext context)
    {
        // 检查免疫标签
        if (ImmuneTags != null && context.TargetHost != null)
        {
            foreach (var tag in ImmuneTags)
                if (context.TargetHost.HasTag(tag)) return false;
        }

        // 检查目标必需标签
        if (RequiredTargetTags != null && context.TargetHost != null)
        {
            foreach (var tag in RequiredTargetTags)
                if (!context.TargetHost.HasTag(tag)) return false;
        }

        // 检查施法者必需标签
        if (RequiredSourceTags != null && context.InstigatorHost != null)
        {
            foreach (var tag in RequiredSourceTags)
                if (!context.InstigatorHost.HasTag(tag)) return false;
        }

        return true;
    }

    /// <summary>
    ///     从 CSV 行数据创建 GameplayEffectData。
    /// </summary>
    public static GameplayEffectData FromCsvRow(Dictionary<string, string> row)
    {
        var data = new GameplayEffectData
        {
            EffectId = ParseInt(row, "effect_id"),
            EffectName = GetString(row, "effect_name", string.Empty),
            BaseDamage = ParseFloat(row, "base_damage"),
            BaseHealing = ParseFloat(row, "base_healing"),
            Duration = ParseFloat(row, "duration"),
            DurationPolicy = ParseFloat(row, "duration") > 0 ? GEDurationPolicy.HasDuration : GEDurationPolicy.Instant
        };

        // 解析标签（逗号分隔）
        var granted = GetString(row, "granted_tags", string.Empty);
        if (!string.IsNullOrEmpty(granted))
            data.GrantedTags = new List<string>(granted.Split(',', StringSplitOptions.RemoveEmptyEntries));

        var requiredTarget = GetString(row, "required_target_tags", string.Empty);
        if (!string.IsNullOrEmpty(requiredTarget))
            data.RequiredTargetTags = new List<string>(requiredTarget.Split(',', StringSplitOptions.RemoveEmptyEntries));

        var immune = GetString(row, "immune_tags", string.Empty);
        if (!string.IsNullOrEmpty(immune))
            data.ImmuneTags = new List<string>(immune.Split(',', StringSplitOptions.RemoveEmptyEntries));

        return data;
    }

    private static int ParseInt(Dictionary<string, string> row, string key, int defaultValue = 0)
    {
        return row.TryGetValue(key, out var raw) && int.TryParse(raw, out var v) ? v : defaultValue;
    }

    private static float ParseFloat(Dictionary<string, string> row, string key, float defaultValue = 0f)
    {
        return row.TryGetValue(key, out var raw) && float.TryParse(raw, out var v) ? v : defaultValue;
    }

    private static string GetString(Dictionary<string, string> row, string key, string defaultValue = "")
    {
        return row.TryGetValue(key, out var v) ? v : defaultValue;
    }
}