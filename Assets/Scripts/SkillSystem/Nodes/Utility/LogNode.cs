using UnityEngine;

[CreateAssetMenu(fileName = "LogNode", menuName = "Skill System/Nodes/Utility/Log")]
public class LogNode : SkillNodeBase
{
    public StringBinding message = new() { LiteralValue = "Skill node reached." };

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        Debug.Log($"[SkillLog] {message.Resolve(ctx)}");
        return NodeTickResult.Success;
    }
}