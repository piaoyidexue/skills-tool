using System;

/// <summary>
///     布尔数据绑定 —— 用于需要布尔参数的节点（如条件判断开关）。
///     支持三种来源：Blackboard（黑板键）、Literal（字面值）、InvertedBlackboard（黑板取反）。
///     技能配置表通常不存布尔值，因此不提供 SkillConfig 来源。
/// </summary>
[Serializable]
public class BoolBinding
{
    public enum SourceType
    {
        Blackboard,
        Literal,
        InvertedBlackboard
    }

    public SourceType Source = SourceType.Literal;
    public string BlackboardKey = BBKey.IsInterrupted;
    public bool LiteralValue;

    public bool Resolve(SkillContext ctx)
    {
        switch (Source)
        {
            case SourceType.Blackboard:
                return ctx != null ? ctx.Blackboard.GetBool(BlackboardKey, false) : false;
            case SourceType.InvertedBlackboard:
                return ctx != null ? !ctx.Blackboard.GetBool(BlackboardKey, true) : true;
            case SourceType.Literal:
                return LiteralValue;
            default:
                return false;
        }
    }
}
