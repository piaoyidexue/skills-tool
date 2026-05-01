using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  元素反应引擎 (ReactionEngine)
//  全局独立规则表，处理元素反应判定与数值修改。
// ============================================================

/// <summary>
///     元素反应类型枚举。
/// </summary>
public enum ReactionType
{
    None = 0,
    Melt,          // 融化：火 + 冰
    Vaporize,      // 蒸发：火 + 水
    Overload,      // 超载：火 + 雷
    Superconduct,  // 超导：冰 + 雷
    ElectroCharged,// 感电：雷 + 水
    Frozen,        // 冻结：水 + 冰
    Burn,          // 燃烧：火 + 火（持续伤害）
    Chill,         // 寒冰：冰附着
    Conductive,    // 导电：雷附着
    MeltBonus,     // 融化加成
    VaporizeBonus, // 蒸发加成
}

/// <summary>
///     单个反应规则定义。
/// </summary>
[Serializable]
public class ReactionRule
{
    public ReactionType ReactionType = ReactionType.None;
    public string DisplayName = string.Empty;

    /// <summary>触发标签 A（如 "element.fire"）</summary>
    public string TriggerTagA = string.Empty;

    /// <summary>触发标签 B（如 "element.ice"）</summary>
    public string TriggerTagB = string.Empty;

    /// <summary>触发时授予的标签</summary>
    public string GrantedTag = string.Empty;

    /// <summary>触发时移除的标签 A</summary>
    public string RemoveTagA = string.Empty;

    /// <summary>触发时移除的标签 B</summary>
    public string RemoveTagB = string.Empty;

    /// <summary>伤害倍率加成（叠加到基础伤害上）</summary>
    public float DamageMultiplierBonus = 0f;

    /// <summary>额外伤害值</summary>
    public float BonusDamage = 0f;

    /// <summary>持续时间（秒），0 表示瞬时</summary>
    public float Duration = 0f;

    /// <summary>触发冷却时间（秒）</summary>
    public float Cooldown = 0.5f;

    /// <summary>特效 Key</summary>
    public string VFXKey = string.Empty;

    /// <summary>音效 Key</summary>
    public string SFXKey = string.Empty;
}

/// <summary>
///     元素反应事件数据。
/// </summary>
public class ReactionEventArgs : EventArgs
{
    public EffectSpec SourceSpec;
    public ReactionType Reaction;
    public string ReactionName;
    public float BonusDamage;
    public float TotalDamage;
    public Transform Target;
    public Transform Instigator;
}

/// <summary>
///     元素反应引擎。
///     全局独立规则表，作为 Modifier 管线的一环。
/// </summary>
public class ReactionEngine : IEffectModifier
{
    // ---- 规则表 ----
    private readonly List<ReactionRule> _rules = new();
    private readonly Dictionary<(string, string), ReactionRule> _ruleLookup = new();
    private readonly Dictionary<int, float> _cooldownTimers = new();

    // ---- 事件 ----
    public event EventHandler<ReactionEventArgs> OnReactionTriggered;

    public int Priority => 100; // 在普通 Modifier 之后执行

    // ============================================================
    //  初始化
    // ============================================================

    public ReactionEngine()
    {
        // 注册默认规则
        RegisterDefaultRules();
    }

    private void RegisterDefaultRules()
    {
        // 融化：火 + 冰 → 额外伤害 2.0x
        AddRule(new ReactionRule
        {
            ReactionType = ReactionType.Melt,
            DisplayName = "融化",
            TriggerTagA = "element.fire",
            TriggerTagB = "status.chill",
            GrantedTag = "reaction.melt",
            RemoveTagA = "element.fire",
            RemoveTagB = "status.chill",
            DamageMultiplierBonus = 2.0f,
            VFXKey = "vfx_reaction_melt",
            SFXKey = "sfx_reaction_melt"
        });

        // 超载：火 + 雷 → AOE 伤害 1.5x
        AddRule(new ReactionRule
        {
            ReactionType = ReactionType.Overload,
            DisplayName = "超载",
            TriggerTagA = "element.fire",
            TriggerTagB = "status.conductive",
            GrantedTag = "reaction.overload",
            RemoveTagA = "element.fire",
            RemoveTagB = "status.conductive",
            DamageMultiplierBonus = 1.5f,
            VFXKey = "vfx_reaction_overload",
            SFXKey = "sfx_reaction_overload"
        });

        // 超导：冰 + 雷 → 降低防御
        AddRule(new ReactionRule
        {
            ReactionType = ReactionType.Superconduct,
            DisplayName = "超导",
            TriggerTagA = "status.chill",
            TriggerTagB = "status.conductive",
            GrantedTag = "reaction.superconduct",
            RemoveTagA = "status.chill",
            RemoveTagB = "status.conductive",
            Duration = 8f,
            VFXKey = "vfx_reaction_superconduct",
            SFXKey = "sfx_reaction_superconduct"
        });

        // 感电：雷 + 水 → 持续伤害
        AddRule(new ReactionRule
        {
            ReactionType = ReactionType.ElectroCharged,
            DisplayName = "感电",
            TriggerTagA = "status.conductive",
            TriggerTagB = "status.wet",
            GrantedTag = "reaction.electrocharged",
            RemoveTagA = "status.conductive",
            RemoveTagB = "status.wet",
            BonusDamage = 10f,
            Duration = 4f,
            VFXKey = "vfx_reaction_electro",
            SFXKey = "sfx_reaction_electro"
        });

        // 冻结：水 + 冰
        AddRule(new ReactionRule
        {
            ReactionType = ReactionType.Frozen,
            DisplayName = "冻结",
            TriggerTagA = "status.wet",
            TriggerTagB = "status.chill",
            GrantedTag = "status.frozen",
            RemoveTagA = "status.wet",
            RemoveTagB = "status.chill",
            Duration = 3f,
            VFXKey = "vfx_reaction_freeze",
            SFXKey = "sfx_reaction_freeze"
        });

        // 燃烧：火 + 火 → 持续灼烧
        AddRule(new ReactionRule
        {
            ReactionType = ReactionType.Burn,
            DisplayName = "燃烧",
            TriggerTagA = "element.fire",
            TriggerTagB = "element.fire",
            GrantedTag = "status.burning",
            DamageMultiplierBonus = 0.5f,
            Duration = 6f,
            Cooldown = 1f,
            VFXKey = "vfx_reaction_burn",
            SFXKey = "sfx_reaction_burn"
        });
    }

    /// <summary>
    ///     添加反应规则。
    /// </summary>
    public void AddRule(ReactionRule rule)
    {
        if (rule == null || string.IsNullOrEmpty(rule.TriggerTagA) || string.IsNullOrEmpty(rule.TriggerTagB))
            return;

        _rules.Add(rule);

        // 建立双向查找（兼容 A+B 和 B+A 顺序）
        var key1 = (NormalizeTag(rule.TriggerTagA), NormalizeTag(rule.TriggerTagB));
        var key2 = (key1.Item2, key1.Item1);
        _ruleLookup[key1] = rule;
        _ruleLookup[key2] = rule;
    }

    /// <summary>
    ///     清除所有规则。
    /// </summary>
    public void ClearRules()
    {
        _rules.Clear();
        _ruleLookup.Clear();
    }

    // ============================================================
    //  IEffectModifier 实现
    // ============================================================

    public void Modify(EffectSpec spec)
    {
        if (spec.Context.TargetHost == null) return;

        var targetHost = spec.Context.TargetHost;
        var sourceTags = spec.AppliedTags ?? new List<string>();

        // 遍历所有规则检查是否匹配
        foreach (var rule in _rules)
        {
            if (!IsRuleApplicable(rule, targetHost, sourceTags)) continue;
            if (IsOnCooldown(rule)) continue;

            // 匹配成功，应用反应效果
            ApplyReaction(spec, rule);
            StartCooldown(rule);
            break; // 一次只触发一个反应
        }
    }

    // ============================================================
    //  内部方法
    // ============================================================

    private bool IsRuleApplicable(ReactionRule rule, GEHost targetHost, List<string> sourceTags)
    {
        // 检查目标是否有 Tag B
        if (!targetHost.HasTag(rule.TriggerTagB)) return false;

        // 检查源标签是否有 Tag A
        foreach (var tag in sourceTags)
        {
            if (string.Equals(NormalizeTag(tag), NormalizeTag(rule.TriggerTagA), StringComparison.OrdinalIgnoreCase) ||
                tag.StartsWith(rule.TriggerTagA + ".", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private bool IsOnCooldown(ReactionRule rule)
    {
        var key = rule.GetHashCode();
        if (_cooldownTimers.TryGetValue(key, out var endTime))
        {
            return Time.time < endTime;
        }
        return false;
    }

    private void StartCooldown(ReactionRule rule)
    {
        if (rule.Cooldown > 0)
        {
            _cooldownTimers[rule.GetHashCode()] = Time.time + rule.Cooldown;
        }
    }

    private void ApplyReaction(EffectSpec spec, ReactionRule rule)
    {
        var targetHost = spec.Context.TargetHost;

        // 添加反应标签
        spec.AddAppliedTag(rule.GrantedTag);

        // 计算额外伤害
        if (rule.DamageMultiplierBonus > 0)
        {
            spec.CalculatedDamage *= (1f + rule.DamageMultiplierBonus);
        }
        if (rule.BonusDamage > 0)
        {
            spec.CalculatedDamage += rule.BonusDamage;
        }

        // 移除消耗的标签
        if (!string.IsNullOrEmpty(rule.RemoveTagA))
        {
            targetHost.RemoveInnateTag(rule.RemoveTagA);
            // 移除 GE 授予的对应标签
            RemoveEffectTags(targetHost, rule.RemoveTagA);
        }
        if (!string.IsNullOrEmpty(rule.RemoveTagB))
        {
            targetHost.RemoveInnateTag(rule.RemoveTagB);
            RemoveEffectTags(targetHost, rule.RemoveTagB);
        }

        // 应用持续效果
        if (rule.Duration > 0)
        {
            ApplyReactionBuff(spec, rule);
        }

        // 派发事件
        var args = new ReactionEventArgs
        {
            SourceSpec = spec,
            Reaction = rule.ReactionType,
            ReactionName = rule.DisplayName,
            BonusDamage = rule.BonusDamage,
            TotalDamage = spec.CalculatedDamage,
            Target = spec.Context.Target,
            Instigator = spec.Context.Instigator
        };
        OnReactionTriggered?.Invoke(this, args);

        // 全局事件总线：抛出元素反应触发事件
        GlobalEventBus.Publish(new ReactionTriggeredEvent
        {
            ReactionType = rule.ReactionType,
            ReactionName = rule.DisplayName,
            Target = spec.Context.Target,
            Instigator = spec.Context.Instigator,
            BonusDamage = rule.BonusDamage,
            TotalDamage = spec.CalculatedDamage
        });

        // 日志
        Debug.Log($"[ReactionEngine] {rule.DisplayName} triggered on {spec.Context.Target.name}");
    }

    private void RemoveEffectTags(GEHost host, string tag)
    {
        // 遍历所有 active effects，移除匹配的标签
        foreach (var effect in host.ActiveEffects)
        {
            effect.GrantedTags.RemoveAll(t =>
                string.Equals(t, tag, StringComparison.OrdinalIgnoreCase) ||
                t.StartsWith(tag + ".", StringComparison.OrdinalIgnoreCase));
        }
    }

    private void ApplyReactionBuff(EffectSpec spec, ReactionRule rule)
    {
        var geConfig = new GEConfig
        {
            GEId = rule.GetHashCode(),
            Name = rule.GrantedTag,
            DurationPolicy = GEDurationPolicy.HasDuration,
            Duration = rule.Duration,
            StackPolicy = GEStackPolicy.Refresh,
            MaxStacks = 1
        };
        geConfig.GrantedTags.Add(rule.GrantedTag);

        spec.Context.TargetHost?.ApplyEffect(geConfig, spec.Context.Instigator);
    }

    private static string NormalizeTag(string tag)
    {
        return tag?.Trim().ToLowerInvariant() ?? string.Empty;
    }
}

// ============================================================
//  ReactionEngine 简单实现（非 MonoBehaviour）
//  用于直接集成到 EffectSystem
// ============================================================

/// <summary>
///     全局唯一的 ReactionEngine 实例。
///     在游戏初始化时自动注册到 EffectSystem。
/// </summary>
public static class ReactionEngineGlobal
{
    private static ReactionEngine _instance;
    private static bool _registered;

    public static ReactionEngine Instance
    {
        get
        {
            _instance ??= new ReactionEngine();
            return _instance;
        }
    }

    /// <summary>
    ///     初始化并注册到 EffectSystem。
    ///     应在游戏启动时调用一次。
    /// </summary>
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        if (!_registered)
        {
            EffectSystem.RegisterModifier(Instance);
            _registered = true;
        }
    }
}