using System.Collections;

public class EndNode : SkillNode
{
    public override IEnumerator Execute(SkillContext ctx)
    {
        yield break;
    }

    public override SkillNode ResolveNextNode(SkillContext ctx)
    {
        return null;
    }
}