using System.Collections;

public class ResonanceNode : SkillNode
{
    public StringBinding resonanceTags = new() { LiteralValue = "row_focus" };
    public bool defaultActive = true;

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        var tags = resonanceTags.Resolve(ctx);
        ctx.Blackboard.SetValue(BBKey.ResonanceTags, tags);
        ctx.Blackboard.SetValue(BBKey.HasResonance, defaultActive && !string.IsNullOrWhiteSpace(tags));
        return NodeTickResult.Success;
    }

    public override IEnumerator Execute(SkillContext ctx)
    {
        var tags = resonanceTags.Resolve(ctx);
        ctx.Blackboard.SetValue(BBKey.ResonanceTags, tags);
        ctx.Blackboard.SetValue(BBKey.HasResonance, defaultActive && !string.IsNullOrWhiteSpace(tags));
        yield break;
    }
}