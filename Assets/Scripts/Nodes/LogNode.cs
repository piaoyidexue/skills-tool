using System.Collections;
using UnityEngine;

public class LogNode : SkillNode
{
    public StringBinding message = new() { LiteralValue = "Skill node reached." };

    public override IEnumerator Execute(SkillContext ctx)
    {
        Debug.Log($"[SkillLog] {message.Resolve(ctx)}");
        yield break;
    }
}