public class EndNode : SkillNode
{
    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        return NodeTickResult.Success;
    }

    public override SkillNode ResolveNextNode(SkillContext ctx)
    {
        return null;
    }
}