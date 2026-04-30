using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
///     自建技能图节点基类 —— 替代 NodeCanvas Node。
///     继承 ScriptableObject 以获得 Unity 原生序列化支持，
///     通过组合持有 ISkillNodeLogic 实现执行权分离。
///     不依赖任何第三方框架。
///
///     架构原则（组合 > 继承）：
///     - SkillNodeBase 管图结构（端口、边、序列化、编辑器渲染）
///     - ISkillNodeLogic 管逻辑执行（OnEnter / Tick / OnExit）
///     - 节点通过 Logic 属性持有 ISkillNodeLogic（组合），而非实现接口（继承）
///     - 默认使用自委托适配器，子类可 override 提供独立逻辑类
/// </summary>
public abstract class SkillNodeBase : ScriptableObject
{
    // ──────────── 图元数据 ────────────

    /// <summary>节点在图中的唯一标识（替代 NodeCanvas 的运行时 ID）</summary>
    [SerializeField] private string _nodeGuid;

    /// <summary>编辑器中的位置</summary>
    [SerializeField] private Vector2 _position;

    /// <summary>输入端口名称列表</summary>
    [SerializeField] private List<string> _inputPortNames = new List<string> { "input" };

    /// <summary>输出端口名称列表</summary>
    [SerializeField] private List<string> _outputPortNames = new List<string> { "output" };

    /// <summary>所属图引用</summary>
    [NonSerialized] private SkillGraphAsset _owningGraph;

    /// <summary>是否正在执行（调试用）</summary>
    [NonSerialized] public bool IsExecuting;

    /// <summary>是否为断点（调试用）</summary>
    [SerializeField] private bool _isBreakpoint;

    // ──────────── 公开属性 ────────────

    /// <summary>节点唯一标识</summary>
    public string NodeGuid
    {
        get => _nodeGuid;
        internal set => _nodeGuid = value;
    }

    /// <summary>编辑器位置</summary>
    public Vector2 Position
    {
        get => _position;
        set => _position = value;
    }

    /// <summary>输入端口名称列表（只读）</summary>
    public IReadOnlyList<string> InputPortNames => _inputPortNames;

    /// <summary>输出端口名称列表（只读）</summary>
    public IReadOnlyList<string> OutputPortNames => _outputPortNames;

    /// <summary>所属图</summary>
    public SkillGraphAsset OwningGraph
    {
        get => _owningGraph;
        internal set => _owningGraph = value;
    }

    /// <summary>节点显示名称（用于编辑器和日志）</summary>
    public string NodeName => name;

    /// <summary>是否为断点（替代 CanvasCore isBreakpoint）</summary>
    public bool IsBreakpoint
    {
        get => _isBreakpoint;
        set => _isBreakpoint = value;
    }

    // ──────────── 端口管理 ────────────

    /// <summary>
    ///     设置输入/输出端口名称。
    ///     等价替换原 NodeCanvas 的 SetPortNames。
    /// </summary>
    protected void SetPortNames(IEnumerable<string> inputNames, IEnumerable<string> outputNames)
    {
        _inputPortNames = inputNames?.ToList() ?? new List<string> { "input" };
        _outputPortNames = outputNames?.ToList() ?? new List<string> { "output" };
    }

    // ──────────── 图导航 API ────────────

    /// <summary>
    ///     获取指定输出端口连接的第一个目标节点。
    ///     等价替换原 SkillNode.GetConnectedNode(outputPortName)。
    /// </summary>
    protected SkillNodeBase GetConnectedNode(string outputPortName)
    {
        var edge = GetOwningGraphEdges()
            .FirstOrDefault(e => e.SourceNodeGuid == _nodeGuid && e.SourcePort == outputPortName);
        return edge.Equals(default(SkillEdge)) ? null : ResolveNode(edge.TargetNodeGuid);
    }

    /// <summary>
    ///     获取指定输出端口连接的所有目标节点。
    ///     等价替换原 SkillNode.GetConnectedNodes(outputPortName)。
    /// </summary>
    protected IReadOnlyList<SkillNodeBase> GetConnectedNodes(string outputPortName)
    {
        return GetOwningGraphEdges()
            .Where(e => e.SourceNodeGuid == _nodeGuid && e.SourcePort == outputPortName)
            .Select(e => ResolveNode(e.TargetNodeGuid))
            .Where(n => n != null)
            .ToList();
    }

    /// <summary>
    ///     获取指定输入端口连接的源节点。
    ///     等价替换原 SkillNode.GetInputSourceNode(inputPortName)。
    /// </summary>
    protected SkillNodeBase GetInputSourceNode(string inputPortName)
    {
        var edge = GetOwningGraphEdges()
            .FirstOrDefault(e => e.TargetNodeGuid == _nodeGuid && e.TargetPort == inputPortName);
        return edge.Equals(default(SkillEdge)) ? null : ResolveNode(edge.SourceNodeGuid);
    }

    /// <summary>
    ///     获取本节点的所有输出边。
    ///     等价替换原 SkillOutConnections。
    /// </summary>
    public List<SkillEdge> GetOutputEdges()
    {
        return GetOwningGraphEdges()
            .Where(e => e.SourceNodeGuid == _nodeGuid)
            .ToList();
    }

    /// <summary>
    ///     获取本节点的所有输入边。
    ///     等价替换原 SkillInConnections。
    /// </summary>
    public List<SkillEdge> GetInputEdges()
    {
        return GetOwningGraphEdges()
            .Where(e => e.TargetNodeGuid == _nodeGuid)
            .ToList();
    }

    // ──────────── 默认导航 ────────────

    /// <summary>
    ///     默认实现：沿 "output" 端口获取下一个节点。
    ///     子类可 override 实现自定义导航逻辑（如 ConditionNode 的 truePort/falsePort）。
    ///     返回 SkillNodeBase（图导航职责），不是 ISkillNodeLogic。
    /// </summary>
    public virtual SkillNodeBase ResolveNextNode(SkillContext ctx)
    {
        return GetConnectedNode("output");
    }

    // ──────────── 自委托适配器（组合模式） ────────────

    /// <summary>
    ///     自委托适配器：将 SkillNodeBase 的 OnEnter/Tick/OnExit 方法包装为 ISkillNodeLogic。
    ///     这是组合模式的核心 —— 节点 HAS-A 逻辑，而非 IS-A 逻辑。
    /// </summary>
    private sealed class SelfLogicAdapter : ISkillNodeLogic
    {
        private readonly SkillNodeBase _owner;
        public SelfLogicAdapter(SkillNodeBase owner) => _owner = owner;
        public void OnEnter(SkillContext ctx) => _owner.OnEnter(ctx);
        public NodeTickResult Tick(SkillContext ctx, float deltaTime) => _owner.Tick(ctx, deltaTime);
        public void OnExit(SkillContext ctx) => _owner.OnExit(ctx);
    }

    // ──────────── 组合：逻辑持有 ────────────

    /// <summary>
    ///     节点持有的逻辑实例（组合关系，非继承）。
    ///     默认使用自委托适配器，将本节点的 OnEnter/Tick/OnExit 包装为 ISkillNodeLogic。
    ///     子类可 override 返回独立的 ISkillNodeLogic 实现类（纯 C# 类，无需继承 SkillNodeBase）。
    /// </summary>
    public virtual ISkillNodeLogic Logic
    {
        get
        {
            _logic ??= new SelfLogicAdapter(this);
            return _logic;
        }
    }

    [NonSerialized] private ISkillNodeLogic _logic;

    /// <summary>
    ///     注入外部逻辑实现（组合模式）。
    ///     设置后，节点不再使用自委托适配器，而是使用注入的逻辑实例。
    /// </summary>
    public void SetLogic(ISkillNodeLogic logic) => _logic = logic;

    // ──────────── 逻辑方法（供子类 override + 自委托适配器调用） ────────────

    /// <summary>节点首次进入时调用，子类可 override</summary>
    public virtual void OnEnter(SkillContext ctx) { }

    /// <summary>每帧 Tick 驱动，返回状态（0 GC，无 IEnumerator 装箱），子类必须 override</summary>
    public abstract NodeTickResult Tick(SkillContext ctx, float deltaTime);

    /// <summary>节点离开时调用，子类可 override</summary>
    public virtual void OnExit(SkillContext ctx) { }

    // ──────────── 生命周期 ────────────

    /// <summary>
    ///     图启动时调用。替代 NodeCanvas 的 OnGraphStarted。
    ///     子类可 override 执行初始化。
    /// </summary>
    public virtual void OnGraphStarted() { }

    /// <summary>
    ///     图停止时调用。替代 NodeCanvas 的 OnGraphStoped。
    /// </summary>
    public virtual void OnGraphStopped() { }

    // ──────────── 内部辅助 ────────────

    private List<SkillEdge> GetOwningGraphEdges()
    {
        return _owningGraph != null ? _owningGraph.Edges : new List<SkillEdge>();
    }

    private SkillNodeBase ResolveNode(string nodeGuid)
    {
        return _owningGraph != null ? _owningGraph.FindNodeByGuid(nodeGuid) : null;
    }

    // ──────────── ScriptableObject 生命周期 ────────────

    protected virtual void OnEnable()
    {
        // 确保 GUID 存在（编辑器创建时可能尚未赋值）
        if (string.IsNullOrEmpty(_nodeGuid))
            _nodeGuid = GUID.Generate().ToString();
    }

    /// <summary>
    ///     重置节点状态（用于图重新启动时）。
    ///     等价替换原 NodeCanvas 的 Reset。
    /// </summary>
    public virtual void OnReset() { }
}
