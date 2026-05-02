using UnityEngine;

[CreateAssetMenu(fileName = "StartNode", menuName = "Skill System/Nodes/Flow/Start")]
public class StartNode : SkillNodeBase
{
    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        return NodeTickResult.Success;
    }
}