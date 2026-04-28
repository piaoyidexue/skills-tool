using System.Collections;

public class ModifyFloatNode : SkillNode
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

    public override IEnumerator Execute(SkillContext ctx)
    {
        var result = inputValue.Resolve(ctx) * multiplier.Resolve(ctx) + additive.Resolve(ctx);
        ctx.Blackboard.SetValue(outputKey, result);
        yield break;
    }
}