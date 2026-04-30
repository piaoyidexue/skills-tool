public class SkillExecutionFrame
{
    public SkillExecutionFrame(SkillGraphAsset graph, SkillContext context)
    {
        Graph = graph;
        Context = context;
    }

    public SkillGraphAsset Graph { get; private set; }
    public SkillContext Context { get; private set; }

    /// <summary>当前执行到的图节点（用于图导航 + 调试显示）</summary>
    public SkillNodeBase CurrentNode { get; set; }

    /// <summary>当前节点对应的逻辑接口（0 框架依赖，组合获取）</summary>
    public ISkillNodeLogic CurrentLogic => CurrentNode?.Logic;
}