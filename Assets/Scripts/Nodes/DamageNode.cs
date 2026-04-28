using System.Collections;
using UnityEngine;

public class DamageNode : SkillNode
{
    public FloatBinding damageAmount = new()
    {
        Source = FloatBinding.SourceType.SkillConfig,
        SkillField = SkillFloatField.Damage,
        DefaultValue = 0f
    };

    public FloatBinding damageRate = new()
    {
        Source = FloatBinding.SourceType.SkillConfig,
        SkillField = SkillFloatField.DamageRate,
        DefaultValue = 1f
    };

    public bool multiplyByDamageRate = true;

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        if (ctx == null || ctx.Target == null) return NodeTickResult.Success;

        var damageable = ctx.Target.GetComponent<IDamageable>();
        if (damageable == null) return NodeTickResult.Success;

        var finalDamage = damageAmount.Resolve(ctx);
        if (multiplyByDamageRate) finalDamage *= Mathf.Max(0f, damageRate.Resolve(ctx));

        ctx.Blackboard.SetValue(BBKey.LastDamage, finalDamage);
        damageable.TakeDamage(finalDamage, ctx.Caster);
        return NodeTickResult.Success;
    }

    public override IEnumerator Execute(SkillContext ctx)
    {
        if (ctx == null || ctx.Target == null) yield break;

        var damageable = ctx.Target.GetComponent<IDamageable>();
        if (damageable == null) yield break;

        var finalDamage = damageAmount.Resolve(ctx);
        if (multiplyByDamageRate) finalDamage *= Mathf.Max(0f, damageRate.Resolve(ctx));

        ctx.Blackboard.SetValue(BBKey.LastDamage, finalDamage);
        damageable.TakeDamage(finalDamage, ctx.Caster);
        yield break;
    }
}
