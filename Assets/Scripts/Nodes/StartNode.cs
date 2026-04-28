using System.Collections;

public class StartNode : SkillNode
{
    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        return NodeTickResult.Success;
    }

    public override IEnumerator Execute(SkillContext ctx)
    {
        yield break;
    }
}