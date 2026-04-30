using System.Collections.Generic;

public class SetValueNode : SkillNodeBase
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

    public override bool CanCompile => true;

    public override List<SkillEffectData> Compile(SkillContext ctx = null)
    {
        string valueStr = valueType switch
        {
            ValueType.Float => floatValue.Resolve(ctx).ToString(),
            ValueType.Bool => boolValue.ToString(),
            ValueType.String => stringValue.Resolve(ctx),
            _ => string.Empty
        };

        return new List<SkillEffectData>
        {
            SkillEffectData.CreateSetBlackboard(key, valueStr)
        };
    }
}