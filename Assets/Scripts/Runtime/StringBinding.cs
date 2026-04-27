using System;

[Serializable]
public class StringBinding
{
    public enum SourceType
    {
        SkillConfigField,
        Blackboard,
        Literal
    }

    public SourceType Source = SourceType.Literal;
    public string SkillConfigFieldName = nameof(SkillConfig.ImpactVFXKey);
    public string BlackboardKey = BBKey.CurrentGraph;
    public string LiteralValue;
    public string DefaultValue;

    public string Resolve(SkillContext ctx)
    {
        switch (Source)
        {
            case SourceType.SkillConfigField:
                return ctx != null && ctx.Config != null ? ctx.Config.GetString(SkillConfigFieldName) : DefaultValue;
            case SourceType.Blackboard:
                return ctx != null ? ctx.Blackboard.GetString(BlackboardKey, DefaultValue) : DefaultValue;
            case SourceType.Literal:
                return LiteralValue;
            default:
                return DefaultValue;
        }
    }
}