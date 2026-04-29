using UnityEngine;

public class ReactionNode : SkillNode
{
    public StringBinding reactionSummary = new() { LiteralValue = "无反应" };

    public FloatBinding damageMultiplier = new()
    {
        Source = FloatBinding.SourceType.Literal,
        LiteralValue = 1.5f,
        DefaultValue = 1.5f
    };

    public bool writeDamageOverride = true;

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        var receiver = ctx.Target != null ? ctx.Target.GetComponent<IStatusReceiver>() : null;
        var reaction = ResolveReaction(ctx, receiver);
        var summary = reaction != null ? reaction.ReactionName : reactionSummary.Resolve(ctx);
        ctx.Blackboard.SetValue(BBKey.ReactionSummary, summary);

        if (writeDamageOverride)
        {
            var currentDamage =
                ctx.Blackboard.GetFloat(BBKey.DamageOverride, ctx.Config != null ? ctx.Config.Damage : 0f);
            var multiplier = reaction != null ? reaction.DamageMultiplier : damageMultiplier.Resolve(ctx);
            ctx.Blackboard.SetValue(BBKey.DamageOverride, currentDamage * multiplier);
        }

        if (receiver != null && reaction != null)
        {
            ApplyReactionSideEffects(receiver, reaction);
        }

        return NodeTickResult.Success;
    }

    private ReactionConfig ResolveReaction(SkillContext ctx, IStatusReceiver receiver)
    {
        if (receiver != null)
        {
            var isCrit = ctx.Blackboard.GetBool(BBKey.IsCrit);
            var hasBurn = receiver.HasStatus(StatusType.Burn);
            var hasChill = receiver.HasStatus(StatusType.Chill) || receiver.HasStatus(StatusType.Freeze);
            var hasConductive = receiver.HasStatus(StatusType.Conductive);

            if (hasBurn && hasChill && hasConductive)
            {
                return ConfigLoader.GetReactionConfig("坍缩");
            }

            if (hasBurn && isCrit)
            {
                return ConfigLoader.GetReactionConfig("引爆");
            }

            if (hasChill && isCrit)
            {
                return ConfigLoader.GetReactionConfig("碎裂");
            }

            if (hasBurn && hasChill)
            {
                return ConfigLoader.GetReactionConfig("熔断");
            }

            if (hasConductive && hasChill)
            {
                return ConfigLoader.GetReactionConfig("雷狱");
            }

            if (hasConductive)
            {
                return ConfigLoader.GetReactionConfig("传导");
            }
        }

        return ConfigLoader.GetReactionConfig(reactionSummary.Resolve(ctx));
    }

    private void ApplyReactionSideEffects(IStatusReceiver receiver, ReactionConfig reaction)
    {
        switch (reaction.ReactionName)
        {
            case "碎裂":
                receiver.ConsumeStatus(StatusType.Chill, out _);
                break;
            case "引爆":
                receiver.ConsumeStatus(StatusType.Burn, out _);
                break;
            case "熔断":
                receiver.ConsumeStatus(StatusType.Burn, out _);
                receiver.ConsumeStatus(StatusType.Chill, out _);
                ApplyControl(receiver, reaction.CcSeconds, "melt");
                break;
            case "雷狱":
                ApplyControl(receiver, reaction.CcSeconds, "lightning_prison");
                break;
        }
    }

    private void ApplyControl(IStatusReceiver receiver, float duration, string sourceTag)
    {
        var controlDuration = Mathf.Max(0.1f, duration);
        receiver.ApplyStatus(new StatusRuntime
        {
            Type = StatusType.Stun,
            SourceTag = sourceTag,
            Value = controlDuration,
            Duration = controlDuration,
            Remaining = controlDuration
        });
    }
}
