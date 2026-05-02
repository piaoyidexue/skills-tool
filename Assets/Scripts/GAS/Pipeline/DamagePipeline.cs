using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  伤害结算管道 —— 事件驱动的伤害计算系统。
//  维度5核心原则：技能图只管"行为触发"，不管"规则结算"。
//
//  流程：
//  原始伤害 → PreCalculate → GE拦截(RaiseGameplayEvent) →
//  Modifier计算 → TagRule标签规则 → PostCalculate → 施加
//
//  标签驱动规则：
//  - 技能图中的 ApplyEffectNode 只携带 tags=["fire"] 的原始伤害
//  - DamagePipeline 自动检查目标身上的标签，触发元素反应
//  - 如目标有 status.chill 且伤害带 fire → 自动乘 2.0x（融化）
//  - 暴击、护甲、Buff 加成等规则都在此管线中处理
//  - 技能图不需要为各种反应创建特殊节点
// ============================================================

/// <summary>
///     伤害结算管道 —— 事件驱动的伤害计算系统。
///     流程：原始伤害 → PreCalculate → GE拦截 → Modifier计算 → TagRule → PostCalculate → 施加
///     维度5增强：技能图只管行为触发+标签传递，具体规则结算交给 TagDamageRule 管线。
/// </summary>
public static class DamagePipeline
{
    public static event System.Action<GEEventContext> OnPreCalculate;
    public static event System.Action<GEEventContext> OnPostCalculate;

    // ---- 维度5：标签驱动规则管线 ----

    /// <summary>
    ///     标签注册表：定义哪些标签触发什么效果。
    ///     外部系统可注册自定义标签规则（如被动技能、装备特效）。
    ///     技能图只需在 ApplyEffectNode 中携带标签，管线自动处理。
    /// </summary>
    private static readonly List<TagDamageRule> TagRules = new();

    /// <summary>
    ///     注册标签伤害规则。
    ///     示例：注册 "status.vulnerable" 标签使受到的伤害 +30%。
    /// </summary>
    public static void RegisterTagRule(TagDamageRule rule)
    {
        if (rule != null && !TagRules.Contains(rule))
            TagRules.Add(rule);
    }

    /// <summary>
    ///     清空所有标签伤害规则（场景切换时调用）。
    /// </summary>
    public static void ClearTagRules() => TagRules.Clear();

    // ============================================================
    //  核心计算
    // ============================================================

    public static float CalculateAndApply(float rawDamage, Transform target, Transform instigator,
        params string[] tags)
    {
        if (target == null || rawDamage <= 0f) return 0f;

        var host = target.GetComponent<GEHost>();
        var instigatorHost = instigator != null ? instigator.GetComponent<GEHost>() : null;

        // 构造共享事件上下文
        var ctx = new GEEventContext
        {
            EventId = "DamageCalculate", Target = target,
            Instigator = instigator, RawValue = rawDamage, Value = rawDamage
        };
        if (tags != null) ctx.Tags.AddRange(tags);

        // 全局事件拦截
        OnPreCalculate?.Invoke(ctx);

        // GEHost 事件拦截（通过公开方法 RaiseGameplayEvent，共享 ctx）
        host?.RaiseGameplayEvent(ctx);
        instigatorHost?.RaiseGameplayEvent(ctx);

        // 传统 Modifier 管道
        var value = ctx.Value;
        if (host != null)
        {
            var multiplier = 1f;
            var additive = 0f;

            foreach (var ge in host.ActiveEffects)
            {
                foreach (var mod in ge.Modifiers)
                {
                    switch (mod.Attribute)
                    {
                        case GEAttribute.DamageTakenMultiplier:
                            if (mod.Operation == GEModOp.Multiply) multiplier *= mod.Magnitude;
                            else if (mod.Operation == GEModOp.Add) additive += mod.Magnitude;
                            break;
                        case GEAttribute.DamageDealtMultiplier:
                            if (mod.Operation == GEModOp.Multiply) multiplier *= mod.Magnitude;
                            break;
                    }
                }

                foreach (var tag in tags)
                {
                    if (ge.GrantedTags.Contains(tag)) { multiplier *= 1.5f; break; }
                }
            }

            value = (value + additive) * multiplier;
        }

        // 标签规则管线（维度5 扩展）
        if (tags != null && tags.Length > 0)
        {
            value = ApplyTagRules(value, tags, host);
        }

        value = Mathf.Max(1f, value);
        ctx.Value = value;

        OnPostCalculate?.Invoke(ctx);

        // 通知吸血/回馈等后置事件
        host?.InvokeGameplayEvent("DamageApplied", value, instigator, tags);

        ApplyDirectDamage(ctx.Value, target, instigator);
        return ctx.Value;
    }

    public static float Preview(float rawDamage, Transform target, Transform instigator,
        params string[] tags)
    {
        if (target == null || rawDamage <= 0f) return 0f;

        var host = target.GetComponent<GEHost>();
        var ctx = new GEEventContext
        {
            EventId = "DamagePreview", Target = target,
            Instigator = instigator, RawValue = rawDamage, Value = rawDamage
        };
        if (tags != null) ctx.Tags.AddRange(tags);

        OnPreCalculate?.Invoke(ctx);
        host?.RaiseGameplayEvent(ctx);
        return Mathf.Max(1f, ctx.Value);
    }

    // ============================================================
    //  标签规则管线
    // ============================================================

    /// <summary>
    ///     应用标签规则管线：遍历所有已注册的 TagDamageRule，
    ///     匹配源标签和目标标签，叠加倍率和固定加成。
    ///     标签规则是纯粹的数据驱动配置，不写死在节点中。
    /// </summary>
    private static float ApplyTagRules(float currentValue, string[] tags, GEHost targetHost)
    {
        if (TagRules.Count == 0 || tags == null || tags.Length == 0) return currentValue;

        var result = currentValue;
        foreach (var rule in TagRules)
        {
            if (rule.TryApply(tags, targetHost, ref result))
            {
                Debug.Log($"[DamagePipeline] TagRule '{rule.RuleName}' applied: x{rule.DamageMultiplier} +{rule.BonusDamage}");
            }
        }
        return result;
    }

    private static float ApplyDirectDamage(float damage, Transform target, Transform instigator)
    {
        var damageable = target.GetComponent<IDamageable>();
        if (damageable == null) damageable = target.GetComponentInParent<IDamageable>();
        if (damageable != null) { damageable.TakeDamage(damage, instigator); return damage; }
        return 0f;
    }
}

/// <summary>
///     标签伤害规则 —— 数据驱动的条件伤害规则。
///     技能图只管传递标签（如 element.fire），管线根据标签组合自动结算。
///     示例：火打冰=2.0x（融化），雷打水=1.5x（感电），脆弱+30% 等。
///     注册方式：DamagePipeline.RegisterTagRule(new TagDamageRule { ... });
/// </summary>
[System.Serializable]
public class TagDamageRule
{
    /// <summary>规则名称（调试用）</summary>
    public string RuleName = string.Empty;

    /// <summary>源标签要求（伤害来源必须包含此标签，如 "element.fire"）</summary>
    public string RequiredSourceTag = string.Empty;

    /// <summary>目标标签要求（目标 GEHost 必须包含此标签，如 "status.chill"），为空则不检查</summary>
    public string RequiredTargetTag = string.Empty;

    /// <summary>伤害倍率（1.0 = 无变化）</summary>
    public float DamageMultiplier = 1f;

    /// <summary>额外固定伤害加成</summary>
    public float BonusDamage;

    /// <summary>
    ///     尝试应用规则：检查源标签和目标标签是否匹配，匹配则修改伤害值。
    /// </summary>
    /// <returns>是否成功应用</returns>
    public bool TryApply(string[] sourceTags, GEHost targetHost, ref float currentValue)
    {
        if (sourceTags == null) return false;

        // 检查源标签
        var hasSourceTag = false;
        foreach (var tag in sourceTags)
        {
            if (string.Equals(tag, RequiredSourceTag, System.StringComparison.OrdinalIgnoreCase))
            {
                hasSourceTag = true;
                break;
            }
        }
        if (!hasSourceTag) return false;

        // 检查目标标签（为空则跳过目标检查）
        if (!string.IsNullOrEmpty(RequiredTargetTag))
        {
            if (targetHost == null || !targetHost.HasTag(RequiredTargetTag))
                return false;
        }

        // 应用规则
        currentValue = currentValue * DamageMultiplier + BonusDamage;
        return true;
    }

    /// <summary>应用规则（简化版，不检查条件）</summary>
    public float Apply(float currentValue)
    {
        return currentValue * DamageMultiplier + BonusDamage;
    }
}
