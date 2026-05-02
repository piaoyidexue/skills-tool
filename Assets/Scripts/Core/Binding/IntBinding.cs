using System;

/// <summary>
///     整数数据绑定 —— 与 FloatBinding 同构，用于需要整数参数的节点。
///     支持三种来源：SkillConfig（枚举字段映射）、Blackboard（黑板键）、Literal（字面值）。
///     节点通过 Binding 声明数据来源，运行时自动解析，实现"纯逻辑容器"。
/// </summary>
[Serializable]
public class IntBinding
{
    public enum SourceType
    {
        SkillConfig,
        Blackboard,
        Literal
    }

    public SourceType Source = SourceType.Literal;
    public SkillFloatField SkillField = SkillFloatField.ChainCount;
    public string BlackboardKey = BBKey.TargetCount;
    public int LiteralValue;
    public int DefaultValue;

    public int Resolve(SkillContext ctx)
    {
        switch (Source)
        {
            case SourceType.SkillConfig:
                return ctx != null && ctx.Config != null
                    ? (int)ctx.Config.GetFloat(SkillField)
                    : DefaultValue;
            case SourceType.Blackboard:
                return ctx != null ? ctx.Blackboard.GetValue<int>(BlackboardKey, DefaultValue) : DefaultValue;
            case SourceType.Literal:
                return LiteralValue;
            default:
                return DefaultValue;
        }
    }
}
