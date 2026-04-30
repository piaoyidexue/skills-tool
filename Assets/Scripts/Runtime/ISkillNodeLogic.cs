/// <summary>
///     技能节点逻辑执行接口 —— 0 框架依赖的纯 C# 接口。
///     将逻辑执行权从 SkillNodeBase（自建框架节点基类）剥离，
///     使执行引擎（SkillExecution）仅依赖此接口而非具体节点类。
///
///     设计原则（组合 > 继承）：
///     - ISkillNodeLogic 管逻辑执行（OnEnter / Tick / OnExit）
///     - SkillNodeBase 管图结构（端口、边、序列化、编辑器渲染）
///     - SkillNodeBase 通过 Logic 属性组合持有 ISkillNodeLogic（HAS-A），而非实现接口（IS-A）
///     - ResolveNextNode 属于图导航职责，不在本接口中
///
///     默认行为：SkillNodeBase.Logic 返回自委托适配器（SelfLogicAdapter），
///     将节点的 OnEnter/Tick/OnExit 包装为 ISkillNodeLogic，保持向后兼容。
///
///     扩展方向：纯 C# 逻辑类可实现此接口，无需继承 SkillNodeBase，
///     通过 SkillNodeBase.SetLogic() 注入或 override Logic 属性即可接入技能图。
/// </summary>
public interface ISkillNodeLogic
{
    /// <summary>节点首次进入时调用</summary>
    void OnEnter(SkillContext ctx);

    /// <summary>每帧 Tick 驱动，返回状态（0 GC，无 IEnumerator 装箱）</summary>
    NodeTickResult Tick(SkillContext ctx, float deltaTime);

    /// <summary>节点离开时调用</summary>
    void OnExit(SkillContext ctx);
}
