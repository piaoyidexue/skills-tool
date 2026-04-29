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

    /// <summary>
    ///     额外 Tag 列表（分号分隔），传入 DamagePipeline 供 GE 事件拦截。
    ///     示例："fire;crit" → 触发火系元素反应 + 暴击加成。
    /// </summary>
    public string extraTags = string.Empty;

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        if (ctx == null || ctx.Target == null) return NodeTickResult.Success;

        var rawDamage = damageAmount.Resolve(ctx);
        if (multiplyByDamageRate)
            rawDamage *= Mathf.Max(0f, damageRate.Resolve(ctx));

        // 解析额外 Tag
        var tags = string.IsNullOrWhiteSpace(extraTags)
            ? null
            : extraTags.Split(';');

        // 走 DamagePipeline（GE 事件拦截 + 元素反应计算一站式完成）
        var finalDamage = DamagePipeline.CalculateAndApply(rawDamage, ctx.Target, ctx.Caster, tags);

        ctx.Blackboard.SetValue(BBKey.LastDamage, finalDamage);
        return NodeTickResult.Success;
    }
}
