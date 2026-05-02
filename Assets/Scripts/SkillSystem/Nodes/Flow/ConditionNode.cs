using UnityEngine;

public enum ConditionMode
{
    BlackboardBool,
    Distance,
    Random,
    CompareFloat,
    BoolBinding
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
///     支持 5 种条件模式：
///     - BlackboardBool: 读取黑板布尔值
///     - Distance: 距离比较
///     - Random: 概率判定
///     - CompareFloat: 浮点数比较
///     - BoolBinding: 通过 BoolBinding 解析（推荐，解耦硬编码黑板键）
/// </summary>
[CreateAssetMenu(fileName = "ConditionNode", menuName = "Skill System/Nodes/Flow/Condition")]
public class ConditionNode : SkillNodeBase
{
    public ConditionMode mode = ConditionMode.BlackboardBool;
    public FloatCompareMode compareMode = FloatCompareMode.Greater;

    /// <summary>黑板键（BlackboardBool / CompareFloat 模式使用）</summary>
    public string bbKey = BBKey.IsInterrupted;

    /// <summary>比较用黑板键（CompareFloat 模式的左操作数来源）</summary>
    public string compareKey = BBKey.TargetDistance;

    /// <summary>比较阈值（Distance / CompareFloat / Random 模式使用）</summary>
    public FloatBinding threshold = new()
    {
        Source = FloatBinding.SourceType.SkillConfig,
        SkillField = SkillFloatField.CritChance,
        DefaultValue = 0f
    };

    /// <summary>布尔绑定（BoolBinding 模式使用，推荐替代 BlackboardBool）</summary>
    public BoolBinding boolCondition = new()
    {
        Source = BoolBinding.SourceType.Literal,
        LiteralValue = false
    };

    private bool _result;

    protected override void OnEnable()
    {
        base.OnEnable();
        SetPortNames(new[] { "input" }, new[] { "truePort", "falsePort" });
    }

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
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
            case ConditionMode.BoolBinding:
                _result = boolCondition.Resolve(ctx);
                break;
            default:
                _result = false;
                break;
        }

        return NodeTickResult.Success;
    }

    public override SkillNodeBase ResolveNextNode(SkillContext ctx)
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