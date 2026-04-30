[System.Obsolete("GAS架构迁移：请使用 ApplyEffectNode + IEffectModifier 替代。数值修改已迁移到 Modifier Pipeline。", false)]
public class ModifyFloatNode : SkillNodeBase
{
    public string outputKey = BBKey.DamageOverride;

    public FloatBinding inputValue = new()
    {
        Source = FloatBinding.SourceType.SkillConfig,
        SkillField = SkillFloatField.Damage
    };

    public FloatBinding multiplier = new()
    {
        Source = FloatBinding.SourceType.Literal,
        LiteralValue = 1f,
        DefaultValue = 1f
    };

    public FloatBinding additive = new()
    {
        Source = FloatBinding.SourceType.Literal,
        LiteralValue = 0f,
        DefaultValue = 0f
    };

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        var result = inputValue.Resolve(ctx) * multiplier.Resolve(ctx) + additive.Resolve(ctx);
        ctx.Blackboard.SetValue(outputKey, result);
        return NodeTickResult.Success;
    }
}