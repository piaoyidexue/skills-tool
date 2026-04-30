using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
///     自建技能图容器 —— 替代 NodeCanvas Graph。
///     继承 ScriptableObject，作为 SkillGraph 资产的运行时容器。
///     管理节点列表 + 边列表，提供图导航 API。
///     不依赖任何第三方框架。
/// </summary>
[CreateAssetMenu(fileName = "NewSkillGraph", menuName = "Skill System/Skill Graph")]
public class SkillGraphAsset : ScriptableObject
{
    // ──────────── 序列化数据 ────────────

    [SerializeField] private string graphId = System.Guid.NewGuid().ToString("N");

    [SerializeField] private List<SkillNodeBase> _nodes = new List<SkillNodeBase>();

    [SerializeField] private List<SkillEdge> _edges = new List<SkillEdge>();

    // ──────────── 运行时缓存 ────────────

    [NonSerialized] private Dictionary<string, SkillNodeBase> _nodeLookup;

    // ──────────── 公开属性 ────────────

    /// <summary>图唯一标识符</summary>
    public string GraphId => string.IsNullOrWhiteSpace(graphId) ? name : graphId;

    /// <summary>所有节点（只读）</summary>
    public IReadOnlyList<SkillNodeBase> Nodes => _nodes;

    /// <summary>所有边（只读）</summary>
    public List<SkillEdge> Edges => _edges;

    /// <summary>起始节点（第一个 StartNode）</summary>
    public SkillNodeBase StartNode => _nodes.FirstOrDefault(n => n is StartNode);

    // ──────────── 节点管理 ────────────

    /// <summary>
    ///     创建并添加一个新节点到图中。
    ///     等价替换原 NodeCanvas 的 Graph.AddNode&lt;T&gt;()。
    /// </summary>
    public T AddNode<T>() where T : SkillNodeBase
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
    ///     等价替换原 NodeCanvas 的 Graph.RemoveNode()。
    /// </summary>
    public void RemoveNode(SkillNodeBase node)
    {
        if (node == null) return;

        // 移除关联边
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
    ///     等价替换原 SkillConnection.Create()。
    /// </summary>
    public void AddEdge(SkillEdge edge)
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
        AddEdge(new SkillEdge(sourceGuid, sourcePort, targetGuid, targetPort));
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
    public SkillNodeBase FindNodeByGuid(string nodeGuid)
    {
        if (_nodeLookup == null) BuildCache();
        return _nodeLookup.TryGetValue(nodeGuid, out var node) ? node : null;
    }

    /// <summary>
    ///     获取指定节点的所有输出目标节点。
    /// </summary>
    public IReadOnlyList<SkillNodeBase> GetOutputNodes(SkillNodeBase node, string portName = null)
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
    public IReadOnlyList<SkillNodeBase> GetInputNodes(SkillNodeBase node, string portName = null)
    {
        var edges = portName != null
            ? _edges.Where(e => e.TargetNodeGuid == node.NodeGuid && e.TargetPort == portName)
            : _edges.Where(e => e.TargetNodeGuid == node.NodeGuid);

        return edges
            .Select(e => FindNodeByGuid(e.SourceNodeGuid))
            .Where(n => n != null)
            .ToList();
    }

    // ──────────── 验证 ────────────

    /// <summary>
    ///     检查图中是否恰好有一个 StartNode。
    /// </summary>
    public bool HasSingleStartNode()
    {
        return _nodes.Count(n => n is StartNode) == 1;
    }

    /// <summary>
    ///     获取起始节点（兼容旧 SkillGraph.GetStartNode() 语义）。
    /// </summary>
    public SkillNodeBase GetStartNode()
    {
        return StartNode;
    }

    // ──────────── 图生命周期 ────────────

    /// <summary>
    ///     启动图，通知所有节点 OnGraphStarted。
    /// </summary>
    public void StartGraph()
    {
        BuildCache();
        foreach (var node in _nodes)
            node.OnGraphStarted();
    }

    /// <summary>
    ///     停止图，通知所有节点 OnGraphStopped。
    /// </summary>
    public void StopGraph()
    {
        foreach (var node in _nodes)
            node.OnGraphStopped();
    }

    /// <summary>
    ///     清空图（移除所有节点和边）。
    /// </summary>
    public void Clear()
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

    private void BuildCache()
    {
        _nodeLookup = new Dictionary<string, SkillNodeBase>(_nodes.Count);
        foreach (var node in _nodes)
        {
            node.OwningGraph = this;
            if (!string.IsNullOrEmpty(node.NodeGuid))
                _nodeLookup[node.NodeGuid] = node;
        }
    }

    private void InvalidateCache()
    {
        _nodeLookup = null;
    }

    // ──────────── ScriptableObject 生命周期 ────────────

    private void OnEnable()
    {
        BuildCache();
    }
}
