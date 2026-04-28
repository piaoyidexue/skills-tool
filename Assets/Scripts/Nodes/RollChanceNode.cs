using System.Collections;
using UnityEngine;

public class RollChanceNode : SkillNode
{
    public string outputKey = BBKey.IsCrit;

    public FloatBinding probability = new()
    {
        Source = FloatBinding.SourceType.SkillConfig,
        SkillField = SkillFloatField.CritChance,
        DefaultValue = 0f
    };

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        var chance = Mathf.Clamp01(probability.Resolve(ctx));
        ctx.Blackboard.SetValue(outputKey, Random.value <= chance);
        return NodeTickResult.Success;
    }

    public override IEnumerator Execute(SkillContext ctx)
    {
        var chance = Mathf.Clamp01(probability.Resolve(ctx));
        ctx.Blackboard.SetValue(outputKey, Random.value <= chance);
        yield break;
    }
}