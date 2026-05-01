using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ============================================================
//  通用逻辑图节点基类 (LogicNodeBase)
//  从 SkillNodeBase 底层剥离的泛化节点基类。
//  管理图元数据（GUID、位置、端口）、图导航 API。
//  SkillNodeBase 和 QuestNodeBase 均继承此基类。
//
//  设计准则：
//  - 与 SkillNodeBase 结构保持一致，确保编辑器绘制逻辑兼容
//  - 不包含任何业务逻辑（Tick、编译等）
//  - 子类通过 override 实现领域特定的行为
// ============================================================

/// <summary>
///     通用逻辑图有向边 —— 纯数据结构，替代 SkillEdge。
///     与 SkillEdge 结构一致，但独立于技能系统。
/// </summary>
[Serializable]
public struct LogicEdge
{
    /// <summary>源节点 GUID</summary>
    public string SourceNodeGuid;

    /// <summary>源节点输出端口名</summary>
    public string SourcePort;

    /// <summary>目标节点 GUID</summary>
    public string TargetNodeGuid;

    /// <summary>目标节点输入端口名</summary>
    public string TargetPort;

    public LogicEdge(string sourceNodeGuid, string sourcePort, string targetNodeGuid, string targetPort)
    {
        SourceNodeGuid = sourceNodeGuid;
        SourcePort = sourcePort;
        TargetNodeGuid = targetNodeGuid;
        TargetPort = targetPort;
    }

    public override string ToString() => $"{SourceNodeGuid}.{SourcePort} → {TargetNodeGuid}.{TargetPort}";
}

/// <summary>
///     通用逻辑图节点基类 —— 从 SkillNodeBase 泛化出的底层基类。
///     管理图元数据（GUID、位置、端口）、图导航 API。
///     不包含业务逻辑（Tick、编译等），子类自行扩展。
/// </summary>
public abstract class LogicNodeBase : ScriptableObject
{
    // ──────────── 图元数据 ────────────

    /// <summary>节点在图中的唯一标识</summary>
    [SerializeField] private string _nodeGuid;

    /// <summary>编辑器中的位置</summary>
    [SerializeField] private Vector2 _position;

    /// <summary>输入端口名称列表</summary>
    [SerializeField] private List<string> _inputPortNames = new() { "input" };

    /// <summary>输出端口名称列表</summary>
    [SerializeField] private List<string> _outputPortNames = new() { "output" };

    /// <summary>所属图引用</summary>
    [NonSerialized] private LogicGraphAsset _owningGraph;

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
    public LogicGraphAsset OwningGraph
    {
        get => _owningGraph;
        internal set => _owningGraph = value;
    }

    /// <summary>节点显示名称（用于编辑器和日志）</summary>
    public string NodeName => name;

    /// <summary>是否为断点</summary>
    public bool IsBreakpoint
    {
        get => _isBreakpoint;
        set => _isBreakpoint = value;
    }

    // ──────────── 端口管理 ────────────

    /// <summary>
    ///     设置输入/输出端口名称。
    /// </summary>
    protected void SetPortNames(IEnumerable<string> inputNames, IEnumerable<string> outputNames)
    {
        _inputPortNames = inputNames?.ToList() ?? new List<string> { "input" };
        _outputPortNames = outputNames?.ToList() ?? new List<string> { "output" };
    }

    // ──────────── 图导航 API ────────────

    /// <summary>
    ///     获取指定输出端口连接的第一个目标节点。
    /// </summary>
    protected LogicNodeBase GetConnectedNode(string outputPortName)
    {
        var edge = GetOwningGraphEdges()
            .FirstOrDefault(e => e.SourceNodeGuid == _nodeGuid && e.SourcePort == outputPortName);
        return edge.Equals(default(LogicEdge)) ? null : ResolveNode(edge.TargetNodeGuid);
    }

    /// <summary>
    ///     获取指定输出端口连接的所有目标节点。
    /// </summary>
    protected IReadOnlyList<LogicNodeBase> GetConnectedNodes(string outputPortName)
    {
        return GetOwningGraphEdges()
            .Where(e => e.SourceNodeGuid == _nodeGuid && e.SourcePort == outputPortName)
            .Select(e => ResolveNode(e.TargetNodeGuid))
            .Where(n => n != null)
            .ToList();
    }

    /// <summary>
    ///     获取指定输入端口连接的源节点。
    /// </summary>
    protected LogicNodeBase GetInputSourceNode(string inputPortName)
    {
        var edge = GetOwningGraphEdges()
            .FirstOrDefault(e => e.TargetNodeGuid == _nodeGuid && e.TargetPort == inputPortName);
        return edge.Equals(default(LogicEdge)) ? null : ResolveNode(edge.SourceNodeGuid);
    }

    /// <summary>
    ///     获取本节点的所有输出边。
    /// </summary>
    public List<LogicEdge> GetOutputEdges()
    {
        return GetOwningGraphEdges()
            .Where(e => e.SourceNodeGuid == _nodeGuid)
            .ToList();
    }

    /// <summary>
    ///     获取本节点的所有输入边。
    /// </summary>
    public List<LogicEdge> GetInputEdges()
    {
        return GetOwningGraphEdges()
            .Where(e => e.TargetNodeGuid == _nodeGuid)
            .ToList();
    }

    // ──────────── 生命周期 ────────────

    /// <summary>
    ///     图启动时调用。
    /// </summary>
    public virtual void OnGraphStarted() { }

    /// <summary>
    ///     图停止时调用。
    /// </summary>
    public virtual void OnGraphStopped() { }

    /// <summary>
    ///     重置节点状态（用于图重新启动时）。
    /// </summary>
    public virtual void OnReset() { }

    // ──────────── 内部辅助 ────────────

    private List<LogicEdge> GetOwningGraphEdges()
    {
        return _owningGraph != null ? _owningGraph.Edges : new List<LogicEdge>();
    }

    private LogicNodeBase ResolveNode(string nodeGuid)
    {
        return _owningGraph != null ? _owningGraph.FindNodeByGuid(nodeGuid) : null;
    }

    // ──────────── ScriptableObject 生命周期 ────────────

    protected virtual void OnEnable()
    {
        if (string.IsNullOrEmpty(_nodeGuid))
            _nodeGuid = GUID.Generate().ToString();
    }
}
