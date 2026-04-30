using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     伤害结算管道 —— 事件驱动的伤害计算系统。
///     流程：原始伤害 → PreCalculate → GE拦截(RaiseGameplayEvent) → Modifier计算 → PostCalculate → 施加
/// </summary>
public static class DamagePipeline
{
    public static event System.Action<GEEventContext> OnPreCalculate;
    public static event System.Action<GEEventContext> OnPostCalculate;

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

    private static float ApplyDirectDamage(float damage, Transform target, Transform instigator)
    {
        var damageable = target.GetComponent<IDamageable>();
        if (damageable == null) damageable = target.GetComponentInParent<IDamageable>();
        if (damageable != null) { damageable.TakeDamage(damage, instigator); return damage; }
        return 0f;
    }
}
