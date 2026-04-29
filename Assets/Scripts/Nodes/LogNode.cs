using UnityEngine;

public class LogNode : SkillNode
{
    public StringBinding message = new() { LiteralValue = "Skill node reached." };

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        Debug.Log($"[SkillLog] {message.Resolve(ctx)}");
        return NodeTickResult.Success;
    }
}