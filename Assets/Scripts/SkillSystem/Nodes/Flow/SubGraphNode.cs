using UnityEngine;

public class SubGraphNode : SkillNodeBase
{
    public SkillGraphAsset subGraph;

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        if (subGraph == null)
        {
            Debug.LogError("[SubGraphNode] Missing subGraph reference.");
            return NodeTickResult.Success;
        }

        // 子图通过 SkillTickManager 注册新的 SkillExecution，
        // 由 SkillExecution 内建的帧栈机制自动处理进出。
        return NodeTickResult.Success;
    }
}
