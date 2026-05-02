using UnityEngine;

[CreateAssetMenu(fileName = "ApplyStatusNode", menuName = "Skill System/Nodes/Combat/ApplyStatus")]
[System.Obsolete("GAS架构迁移：请使用 ApplyEffectNode 替代。状态施加已迁移到 EffectSystem + ReactionEngine。", false)]
public class ApplyStatusNode : SkillNodeBase
{
    public string blackboardKey = BBKey.StatusTags;
    public StringBinding statusTags = new() { LiteralValue = "burn" };
    public bool append = true;
    public float defaultDuration = 2f;
    public float defaultValue = 5f;

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        var tags = statusTags.Resolve(ctx);
        if (string.IsNullOrWhiteSpace(tags)) return NodeTickResult.Success;

        // 更新黑板
        if (append)
        {
            var existing = ctx.Blackboard.GetString(blackboardKey, string.Empty);
            if (string.IsNullOrWhiteSpace(existing))
                ctx.Blackboard.SetValue(blackboardKey, tags);
            else if (!existing.Contains(tags)) ctx.Blackboard.SetValue(blackboardKey, existing + "|" + tags);
        }
        else
        {
            ctx.Blackboard.SetValue(blackboardKey, tags);
        }

        // 使用 GE 系统施加效果 —— 替代旧版 IStatusReceiver/StatusRuntime
        var target = ctx.Target;
        if (target != null)
        {
            var geHost = target.GetComponent<GEHost>();
            if (geHost == null)
                geHost = target.gameObject.AddComponent<GEHost>();

            foreach (var rawTag in tags.Split('|'))
            {
                var tag = rawTag.Trim();
                if (string.IsNullOrWhiteSpace(tag)) continue;

                var geConfig = BuildGEConfig(tag);
                geHost.ApplyEffect(geConfig, ctx.Caster);
            }
        }

        return NodeTickResult.Success;
    }

    private float ResolveStatusValue(string tag)
    {
        return tag.ToLowerInvariant() switch
        {
            "burn" => 8f,
            "chill" => 0.25f,
            "conductive" => 1f,
            "mark" => 0.15f,
            "freeze" => 1f,
            "slow" => 0.35f,
            "stun" => 1f,
            _ => defaultValue
        };
    }

    private float ResolveStatusDuration(string tag)
    {
        return tag.ToLowerInvariant() switch
        {
            "burn" => 3f,
            "chill" => 2.5f,
            "conductive" => 3f,
            "mark" => 2f,
            "freeze" => 1.2f,
            "slow" => 2f,
            "stun" => 0.45f,
            _ => defaultDuration
        };
    }

    private static StatusType ToStatusType(string tag)
    {
        return tag.ToLowerInvariant() switch
        {
            "burn" => StatusType.Burn,
            "chill" => StatusType.Chill,
            "conductive" => StatusType.Conductive,
            "mark" => StatusType.Mark,
            "freeze" => StatusType.Freeze,
            "slow" => StatusType.Slow,
            "stun" => StatusType.Stun,
            "poison" => StatusType.Poison,
            "root" => StatusType.Root,
            _ => StatusType.None
        };
    }

    /// <summary>
    ///     根据状态标签构建 GE 配置 —— 映射技能系统的状态语义到 GE 修改器。
    ///     每个状态类型对应不同的属性修改策略：
    ///     - Burn/Poison: 持续伤害 (DamagePerTick + Period)
    ///     - Chill/Slow: 移速降低 (MoveSpeed Multiplier)
    ///     - Freeze/Stun/Root: 硬控 (MoveSpeed Override 0)
    ///     - Mark/Conductive: 易伤 (DamageTakenMultiplier)
    /// </summary>
    private GEConfig BuildGEConfig(string tag)
    {
        var lowerTag = tag.ToLowerInvariant();
        var config = new GEConfig
        {
            GEId = $"Status_{lowerTag}".GetHashCode(),
            Name = $"Status_{tag}",
            DurationPolicy = GEDurationPolicy.HasDuration,
            Duration = ResolveStatusDuration(tag),
            MaxStacks = 1
        };

        // 授予的 Gameplay Tag
        config.GrantedTags.Add(lowerTag);

        switch (lowerTag)
        {
            case "burn":
            case "poison":
                // 持续伤害：每秒一跳
                config.Period = 1f;
                config.Modifiers.Add(new GEModifier
                {
                    Attribute = GEAttribute.DamagePerTick,
                    Operation = GEModOp.Add,
                    Magnitude = ResolveStatusValue(tag)
                });
                break;

            case "chill":
            case "slow":
                // 减速：乘法叠加
                config.Modifiers.Add(new GEModifier
                {
                    Attribute = GEAttribute.MoveSpeed,
                    Operation = GEModOp.Multiply,
                    Magnitude = 1f - ResolveStatusValue(tag)
                });
                break;

            case "freeze":
            case "stun":
            case "root":
                // 硬控：覆盖移速和攻速为 0
                config.Modifiers.Add(new GEModifier
                {
                    Attribute = GEAttribute.MoveSpeed,
                    Operation = GEModOp.Override,
                    Magnitude = 0f
                });
                config.Modifiers.Add(new GEModifier
                {
                    Attribute = GEAttribute.AttackSpeed,
                    Operation = GEModOp.Override,
                    Magnitude = 0f
                });
                break;

            case "mark":
            case "conductive":
                // 易伤：增加受伤倍率
                config.Modifiers.Add(new GEModifier
                {
                    Attribute = GEAttribute.DamageTakenMultiplier,
                    Operation = GEModOp.Multiply,
                    Magnitude = 1f + ResolveStatusValue(tag)
                });
                break;

            default:
                // 未识别的状态：作为自定义属性加法
                config.Modifiers.Add(new GEModifier
                {
                    Attribute = GEAttribute.Custom,
                    CustomAttribute = lowerTag,
                    Operation = GEModOp.Add,
                    Magnitude = ResolveStatusValue(tag)
                });
                break;
        }

        return config;
    }
}
