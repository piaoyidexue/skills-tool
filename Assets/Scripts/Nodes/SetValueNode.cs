public class SetValueNode : SkillNode
{
    public enum ValueType
    {
        Float,
        Bool,
        String
    }

    public string key = BBKey.LastDamage;
    public ValueType valueType = ValueType.Float;

    public FloatBinding floatValue = new()
    {
        Source = FloatBinding.SourceType.SkillConfig,
        SkillField = SkillFloatField.Damage
    };

    public bool boolValue;
    public StringBinding stringValue = new();

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        switch (valueType)
        {
            case ValueType.Float:
                ctx.Blackboard.SetValue(key, floatValue.Resolve(ctx));
                break;
            case ValueType.Bool:
                ctx.Blackboard.SetValue(key, boolValue);
                break;
            case ValueType.String:
                ctx.Blackboard.SetValue(key, stringValue.Resolve(ctx));
                break;
        }

        return NodeTickResult.Success;
    }
}