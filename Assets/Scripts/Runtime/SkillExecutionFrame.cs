public class SkillExecutionFrame
{
    public SkillExecutionFrame(SkillGraph graph, SkillContext context)
    {
        Graph = graph;
        Context = context;
    }

    public SkillGraph Graph { get; private set; }
    public SkillContext Context { get; private set; }
    public SkillNode CurrentNode { get; set; }
}