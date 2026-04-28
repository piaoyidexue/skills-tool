using System.Collections;
using UnityEngine;

public class SubGraphNode : SkillNode
{
    public SkillGraph subGraph;

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        if (subGraph == null)
        {
            Debug.LogError("[SubGraphNode] Missing subGraph reference.");
            return NodeTickResult.Success;
        }

        // 在当前 Tick 系统中，子图通过 SkillTickManager 注册新的执行
        // 但这里同步模式下，子图被视为一个整体执行
        // 注意：完整的子图 Tick 支持需要在 SkillExecution 中处理帧栈
        return NodeTickResult.Success;
    }

    public override IEnumerator Execute(SkillContext ctx)
    {
        if (subGraph == null)
        {
            Debug.LogError("[SubGraphNode] Missing subGraph reference.");
            yield break;
        }

        yield return SkillRunner.Instance.RunSkill(subGraph, ctx);
    }
}
