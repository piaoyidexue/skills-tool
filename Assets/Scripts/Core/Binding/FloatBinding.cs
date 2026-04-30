using System;

[Serializable]
public class FloatBinding
{
    public enum SourceType
    {
        SkillConfig,
        Blackboard,
        Literal
    }

    public SourceType Source = SourceType.SkillConfig;
    public SkillFloatField SkillField = SkillFloatField.Damage;
    public string BlackboardKey = BBKey.DamageOverride;
    public float LiteralValue;
    public float DefaultValue;

    public float Resolve(SkillContext ctx)
    {
        switch (Source)
        {
            case SourceType.SkillConfig:
                return ctx != null && ctx.Config != null ? ctx.Config.GetFloat(SkillField) : DefaultValue;
            case SourceType.Blackboard:
                return ctx != null ? ctx.Blackboard.GetFloat(BlackboardKey, DefaultValue) : DefaultValue;
            case SourceType.Literal:
                return LiteralValue;
            default:
                return DefaultValue;
        }
    }
}