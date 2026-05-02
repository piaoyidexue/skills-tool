using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     黑板写入节点 —— 将 Binding 解析的值写入黑板。
///     所有值通过 Binding 来源解析（SkillConfig/Blackboard/Literal），
///     节点本身不持有硬编码字面值，实现纯逻辑容器。
/// </summary>
[CreateAssetMenu(fileName = "SetValueNode", menuName = "Skill System/Nodes/Utility/SetValue")]
public class SetValueNode : SkillNodeBase
{
    public enum ValueType
    {
        Float,
        Bool,
        String
    }

    public string key = BBKey.IsInterrupted;
    public ValueType valueType = ValueType.Float;

    public FloatBinding floatValue = new()
    {
        Source = FloatBinding.SourceType.SkillConfig,
        SkillField = SkillFloatField.Damage
    };

    /// <summary>布尔值绑定（Bool 模式使用，推荐替代直接 bool 字段）</summary>
    public BoolBinding boolValue = new()
    {
        Source = BoolBinding.SourceType.Literal,
        LiteralValue = false
    };

    public StringBinding stringValue = new();

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        switch (valueType)
        {
            case ValueType.Float:
                ctx.Blackboard.SetValue(key, floatValue.Resolve(ctx));
                break;
            case ValueType.Bool:
                ctx.Blackboard.SetValue(key, boolValue.Resolve(ctx));
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
            ValueType.Bool => boolValue.Resolve(ctx).ToString(),
            ValueType.String => stringValue.Resolve(ctx),
            _ => string.Empty
        };

        return new List<SkillEffectData>
        {
            SkillEffectData.CreateSetBlackboard(key, valueStr)
        };
    }
}