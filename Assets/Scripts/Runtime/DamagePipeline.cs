using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     伤害结算管道 —— 事件驱动的伤害计算系统。
///     所有伤害结算经过此管道，GE 系统可在结算前后拦截修改。
///     替代在节点中直接调用 IDamageable.TakeDamage() 的旧模式。
/// </summary>
public static class DamagePipeline
{
    /// <summary>
    ///     计算并施加最终伤害（经过所有 GE Modifier 处理）。
    /// </summary>
    /// <param name="rawDamage">原始伤害值</param>
    /// <param name="target">受击者</param>
    /// <param name="instigator">施加者</param>
    /// <param name="tags">额外 Tag（如暴击、元素类型等）</param>
    /// <returns>实际造成的伤害值</returns>
    public static float CalculateAndApply(float rawDamage, Transform target, Transform instigator,
        params string[] tags)
    {
        if (target == null || rawDamage <= 0f) return 0f;

        // 1. 获取目标上的 GE Host
        var host = target.GetComponent<GEHost>();
        if (host == null)
        {
            // 无 GE 系统，直接施加伤害（回退到简单模式）
            return ApplyDirectDamage(rawDamage, target, instigator);
        }

        // 2. 检查 Tag 条件（例如：目标有 "Status.Chill"，火系伤害翻倍）
        var multiplier = 1f;
        var additive = 0f;

        // 3. 遍历目标身上的 GE Modifier
        foreach (var ge in host.ActiveEffects)
        {
            foreach (var mod in ge.Modifiers)
            {
                switch (mod.Attribute)
                {
                    case GEAttribute.DamageTakenMultiplier:
                        if (mod.Operation == GEModOp.Multiply)
                            multiplier *= mod.Magnitude;
                        else if (mod.Operation == GEModOp.Add)
                            additive += mod.Magnitude;
                        break;

                    case GEAttribute.DamageDealtMultiplier:
                        // 施加者身上的增伤
                        if (mod.Operation == GEModOp.Multiply)
                            multiplier *= mod.Magnitude;
                        break;
                }
            }

            // Tag 匹配：如果传入的 tags 匹配 GE 的 GrantedTags，触发额外效果
            foreach (var tag in tags)
            {
                if (ge.GrantedTags.Contains(tag))
                {
                    // 特殊 Tag 触发（如元素反应加成）
                    multiplier *= 1.5f; // 默认元素反应 1.5 倍
                    break;
                }
            }
        }

        var finalDamage = (rawDamage + additive) * multiplier;
        finalDamage = Mathf.Max(1f, finalDamage);

        // 4. 施加伤害
        ApplyDirectDamage(finalDamage, target, instigator);
        return finalDamage;
    }

    /// <summary>
    ///     直接施加伤害（绕过 GE 系统）。
    /// </summary>
    private static float ApplyDirectDamage(float damage, Transform target, Transform instigator)
    {
        var damageable = target.GetComponent<IDamageable>();
        if (damageable == null)
            damageable = target.GetComponentInParent<IDamageable>();

        if (damageable != null)
        {
            damageable.TakeDamage(damage, instigator);
            return damage;
        }

        return 0f;
    }
}
