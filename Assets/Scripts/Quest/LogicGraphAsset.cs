using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ============================================================
//  通用逻辑图容器 (LogicGraphAsset)
//  从 SkillGraphAsset 底层剥离的泛化图节点拓扑容器。
//  管理节点列表 + 边列表，提供图导航 API。
//  SkillGraphAsset 和 QuestGraphAsset 均继承此基类。
//
//  设计准则：
//  - 不依赖任何业务逻辑（技能/任务/对话）
//  - 仅管图结构（节点、边、缓存、生命周期）
//  - 子类通过泛型约束指定节点基类
// ============================================================

/// <summary>
///     通用逻辑图容器 —— 从 SkillGraphAsset 泛化出的底层基类。
///     管理节点列表 + 边列表，提供图导航 API。
///     不依赖任何业务逻辑，仅管图结构。
/// </summary>
public abstract class LogicGraphAsset : ScriptableObject
{
    // ──────────── 序列化数据 ────────────

    [SerializeField] private string graphId = System.Guid.NewGuid().ToString("N");

    [SerializeField] private List<LogicNodeBase> _nodes = new();

    [SerializeField] private List<LogicEdge> _edges = new();

    // ──────────── 运行时缓存 ────────────

    [NonSerialized] private Dictionary<string, LogicNodeBase> _nodeLookup;

    // ──────────── 公开属性 ────────────

    /// <summary>图唯一标识符</summary>
    public string GraphId => string.IsNullOrWhiteSpace(graphId) ? name : graphId;

    /// <summary>所有节点（只读）</summary>
    public IReadOnlyList<LogicNodeBase> Nodes => _nodes;

    /// <summary>所有边（只读）</summary>
    public List<LogicEdge> Edges => _edges;

    /// <summary>起始节点（子类决定什么算"起始"）</summary>
    public abstract LogicNodeBase StartNode { get; }

    // ──────────── 节点管理 ────────────

    /// <summary>
    ///     创建并添加一个新节点到图中。
    /// </summary>
    public T AddNode<T>() where T : LogicNodeBase
    {
        var node = CreateInstance<T>();
        node.NodeGuid = GUID.Generate().ToString();
        node.OwningGraph = this;
        node.name = typeof(T).Name;
        _nodes.Add(node);

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.AddObjectToAsset(node, this);
        node.hideFlags = HideFlags.HideInHierarchy;
        UnityEditor.EditorUtility.SetDirty(this);
#endif

        InvalidateCache();
        return node;
    }

    /// <summary>
    ///     从图中移除节点及其所有关联边。
    /// </summary>
    public void RemoveNode(LogicNodeBase node)
    {
        if (node == null) return;

        _edges.RemoveAll(e =>
            e.SourceNodeGuid == node.NodeGuid || e.TargetNodeGuid == node.NodeGuid);

        _nodes.Remove(node);
        node.OwningGraph = null;

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.RemoveObjectFromAsset(node);
        DestroyImmediate(node, true);
        UnityEditor.EditorUtility.SetDirty(this);
#endif

        InvalidateCache();
    }

    // ──────────── 边管理 ────────────

    /// <summary>
    ///     添加一条有向边。
    /// </summary>
    public void AddEdge(LogicEdge edge)
    {
        _edges.Add(edge);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    /// <summary>
    ///     添加一条有向边（便利方法）。
    /// </summary>
    public void AddEdge(string sourceGuid, string sourcePort, string targetGuid, string targetPort)
    {
        AddEdge(new LogicEdge(sourceGuid, sourcePort, targetGuid, targetPort));
    }

    /// <summary>
    ///     移除所有以指定节点为源/目标的边。
    /// </summary>
    public void RemoveEdgesForNode(string nodeGuid)
    {
        _edges.RemoveAll(e =>
            e.SourceNodeGuid == nodeGuid || e.TargetNodeGuid == nodeGuid);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    // ──────────── 图导航 ────────────

    /// <summary>
    ///     按 GUID 查找节点。
    /// </summary>
    public LogicNodeBase FindNodeByGuid(string nodeGuid)
    {
        if (_nodeLookup == null) BuildCache();
        return _nodeLookup.TryGetValue(nodeGuid, out var node) ? node : null;
    }

    /// <summary>
    ///     获取指定节点的所有输出目标节点。
    /// </summary>
    public IReadOnlyList<LogicNodeBase> GetOutputNodes(LogicNodeBase node, string portName = null)
    {
        var edges = portName != null
            ? _edges.Where(e => e.SourceNodeGuid == node.NodeGuid && e.SourcePort == portName)
            : _edges.Where(e => e.SourceNodeGuid == node.NodeGuid);

        return edges
            .Select(e => FindNodeByGuid(e.TargetNodeGuid))
            .Where(n => n != null)
            .ToList();
    }

    /// <summary>
    ///     获取指定节点的所有输入源节点。
    /// </summary>
    public IReadOnlyList<LogicNodeBase> GetInputNodes(LogicNodeBase node, string portName = null)
    {
        var edges = portName != null
            ? _edges.Where(e => e.TargetNodeGuid == node.NodeGuid && e.TargetPort == portName)
            : _edges.Where(e => e.TargetNodeGuid == node.NodeGuid);

        return edges
            .Select(e => FindNodeByGuid(e.SourceNodeGuid))
            .Where(n => n != null)
            .ToList();
    }

    // ──────────── 图生命周期 ────────────

    /// <summary>
    ///     启动图，通知所有节点 OnGraphStarted。
    /// </summary>
    public virtual void StartGraph()
    {
        BuildCache();
        foreach (var node in _nodes)
            node.OnGraphStarted();
    }

    /// <summary>
    ///     停止图，通知所有节点 OnGraphStopped。
    /// </summary>
    public virtual void StopGraph()
    {
        foreach (var node in _nodes)
            node.OnGraphStopped();
    }

    /// <summary>
    ///     清空图（移除所有节点和边）。
    /// </summary>
    public virtual void Clear()
    {
        for (var i = _nodes.Count - 1; i >= 0; i--)
        {
            var node = _nodes[i];

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.RemoveObjectFromAsset(node);
            DestroyImmediate(node, true);
#else
            _nodes.RemoveAt(i);
#endif
        }

        _nodes.Clear();
        _edges.Clear();
        InvalidateCache();

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    // ──────────── 缓存 ────────────

    protected void BuildCache()
    {
        _nodeLookup = new Dictionary<string, LogicNodeBase>(_nodes.Count);
        foreach (var node in _nodes)
        {
            node.OwningGraph = this;
            if (!string.IsNullOrEmpty(node.NodeGuid))
                _nodeLookup[node.NodeGuid] = node;
        }
    }

    protected void InvalidateCache()
    {
        _nodeLookup = null;
    }

    // ──────────── ScriptableObject 生命周期 ────────────

    private void OnEnable()
    {
        BuildCache();
    }
}
