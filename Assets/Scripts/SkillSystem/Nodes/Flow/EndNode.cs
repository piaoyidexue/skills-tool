public class EndNode : SkillNodeBase
{
    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        return NodeTickResult.Success;
    }

    public override SkillNodeBase ResolveNextNode(SkillContext ctx)
    {
        return null;
    }
}