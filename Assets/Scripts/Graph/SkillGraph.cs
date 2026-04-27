using System;
using NodeCanvas.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
///     技能图 —— 基于 NodeCanvas CanvasCore 框架的可视化技能逻辑容器。
///     继承 Graph 获得完整的序列化、节点管理、连接系统支持。
/// </summary>
[CreateAssetMenu(fileName = "NewSkillGraph", menuName = "Skill System/Skill Graph")]
public class SkillGraph : Graph
{
    [SerializeField] private string graphId = Guid.NewGuid().ToString("N");

    /// <summary>图唯一标识符</summary>
    public string GraphId => string.IsNullOrWhiteSpace(graphId) ? name : graphId;

    // ---- CanvasCore Graph 抽象属性实现 ----
    public override System.Type baseNodeType => typeof(SkillNode);
    public override bool requiresAgent => false;
    public override bool requiresPrimeNode => true;
    public override bool isTree => true;
    public override bool allowBlackboardOverrides => false;
    public override bool canAcceptVariableDrops => false;

    /// <summary>获取图的 StartNode（primeNode）</summary>
    public StartNode GetStartNode()
    {
        if (primeNode is StartNode startNode)
            return startNode;

        foreach (var node in allNodes)
            if (node is StartNode sn)
                return sn;

        return null;
    }

    /// <summary>校验是否恰好包含一个 StartNode</summary>
    public bool HasSingleStartNode()
    {
        var count = 0;
        foreach (var node in allNodes)
            if (node is StartNode)
                count++;
        return count == 1;
    }

    /// <summary>
    ///     添加节点到图中。
    ///     包装 CanvasCore AddNode。
    /// </summary>
    public T AddNodeToGraph<T>() where T : SkillNode
    {
        var node = AddNode<T>();
        return node;
    }

    /// <summary>添加指定类型的节点</summary>
    public SkillNode AddNodeToGraph(System.Type nodeType)
    {
        var node = (SkillNode)AddNode(nodeType);
        return node;
    }

    /// <summary>
    ///     移除节点并标记图为脏。
    ///     包装 CanvasCore RemoveNode。
    /// </summary>
    public void RemoveNodeAndSave(Node node)
    {
        RemoveNode(node, true, false);
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }
}