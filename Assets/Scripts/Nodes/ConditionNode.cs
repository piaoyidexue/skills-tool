using System.Collections;
using UnityEngine;

public enum ConditionMode
{
    BlackboardBool,
    Distance,
    Random,
    CompareFloat
}

public enum FloatCompareMode
{
    Greater,
    GreaterOrEqual,
    Less,
    LessOrEqual
}

/// <summary>
///     条件分支节点 —— 根据条件判定结果选择不同的执行路径。
///     使用命名端口 "truePort" 和 "falsePort" 分别对应 true/false 分支。
/// </summary>
public class ConditionNode : SkillNode
{
    public ConditionMode mode = ConditionMode.BlackboardBool;
    public FloatCompareMode compareMode = FloatCompareMode.Greater;
    public string bbKey = BBKey.IsCrit;
    public string compareKey = BBKey.LastDamage;

    public FloatBinding threshold = new()
    {
        Source = FloatBinding.SourceType.SkillConfig,
        SkillField = SkillFloatField.CritChance,
        DefaultValue = 0f
    };

    private bool _result;

    // ---- 多端口配置 ----
    public override int maxOutConnections => -1;

    public ConditionNode()
    {
        SetPortNames(new[] { "input" }, new[] { "truePort", "falsePort" });
    }

    public override IEnumerator Execute(SkillContext ctx)
    {
        var thresholdValue = threshold.Resolve(ctx);
        switch (mode)
        {
            case ConditionMode.BlackboardBool:
                _result = ctx.Blackboard.GetBool(bbKey);
                break;
            case ConditionMode.Distance:
                _result = ctx.Caster != null &&
                          ctx.Target != null &&
                          Compare(Vector3.Distance(ctx.Caster.position, ctx.Target.position), thresholdValue);
                if (ctx.Caster != null && ctx.Target != null)
                    ctx.Blackboard.SetValue(BBKey.TargetDistance,
                        Vector3.Distance(ctx.Caster.position, ctx.Target.position));
                break;
            case ConditionMode.Random:
                _result = Random.value <= Mathf.Clamp01(thresholdValue);
                break;
            case ConditionMode.CompareFloat:
                _result = Compare(ctx.Blackboard.GetFloat(compareKey), thresholdValue);
                break;
            default:
                _result = false;
                break;
        }

        yield break;
    }

    public override SkillNode ResolveNextNode(SkillContext ctx)
    {
        return GetConnectedNode(_result ? "truePort" : "falsePort");
    }

    private bool Compare(float left, float right)
    {
        switch (compareMode)
        {
            case FloatCompareMode.Greater:
                return left > right;
            case FloatCompareMode.GreaterOrEqual:
                return left >= right;
            case FloatCompareMode.Less:
                return left < right;
            case FloatCompareMode.LessOrEqual:
                return left <= right;
            default:
                return false;
        }
    }
}