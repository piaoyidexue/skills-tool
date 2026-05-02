using UnityEngine;

/// <summary>
///     概率判定节点 —— 根据概率产生布尔结果写入黑板。
///     概率来源通过 FloatBinding 解析，实现参数外部配置化。
///     典型用途：暴击判定、掉落判定、分支概率等。
/// </summary>
[CreateAssetMenu(fileName = "RollChanceNode", menuName = "Skill System/Nodes/Utility/RollChance")]
public class RollChanceNode : SkillNodeBase
{
    /// <summary>结果写入的黑板键</summary>
    public string outputKey = BBKey.IsInterrupted;

    /// <summary>概率来源（0~1），支持 SkillConfig/Blackboard/Literal</summary>
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
}