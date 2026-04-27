using System.Collections;
using UnityEngine;

public class ApplyStatusNode : SkillNode
{
    public string blackboardKey = BBKey.StatusTags;
    public StringBinding statusTags = new() { LiteralValue = "burn" };
    public bool append = true;
    public float defaultDuration = 2f;
    public float defaultValue = 5f;

    public override IEnumerator Execute(SkillContext ctx)
    {
        var tags = statusTags.Resolve(ctx);
        if (string.IsNullOrWhiteSpace(tags)) yield break;

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

        var receiver = ctx.Target != null ? ctx.Target.GetComponent<IStatusReceiver>() : null;
        if (receiver != null)
        {
            foreach (var rawTag in tags.Split('|'))
            {
                var tag = rawTag.Trim();
                if (string.IsNullOrWhiteSpace(tag))
                {
                    continue;
                }

                var duration = ResolveStatusDuration(tag);
                receiver.ApplyStatus(new StatusRuntime
                {
                    Type = ToStatusType(tag),
                    SourceTag = tag,
                    Value = ResolveStatusValue(tag),
                    Duration = duration,
                    Remaining = duration,
                    Instigator = ctx.Caster
                });
            }
        }
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
}
