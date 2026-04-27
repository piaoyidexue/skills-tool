using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NodeCanvas.Framework;
using ParadoxNotion;
using ParadoxNotion.Design;
using UnityEngine;

/// <summary>
///     技能节点基类 —— 基于 NodeCanvas CanvasCore Node 框架。
///     定义了技能图节点的通用属性和执行接口。
///     端口系统改为基于 SkillConnection 的命名端口模型。
///     使用 CanvasCore 内置的 isBreakpoint 替代自定义断点系统。
/// </summary>
public abstract class SkillNode : Node
{
    [SerializeField] private string nodeGuid = Guid.NewGuid().ToString("N");

    [NonSerialized] public bool IsExecuting;

    // ---- 端口元数据（命名端口模型，区别于 CanvasCore 的连接索引模型）----
    /// <summary>输入端口名称列表</summary>
    [SerializeField] private List<string> _inputPortNames = new() { "input" };
    /// <summary>输出端口名称列表</summary>
    [SerializeField] private List<string> _outputPortNames = new() { "output" };

    public string NodeGuid => nodeGuid;

    /// <summary>使用 CanvasCore 内置的 isBreakpoint</summary>
    public bool HasBreakpoint
    {
        get => isBreakpoint;
        set => isBreakpoint = value;
    }

    protected SkillGraph OwningGraph => graph as SkillGraph;

    // ---- CanvasCore Node 抽象属性实现 ----
    public override int maxInConnections => 1;
    public override int maxOutConnections => 1;
    public override System.Type outConnectionType => typeof(SkillConnection);
    public override bool allowAsPrime => this is StartNode;
    public override bool canSelfConnect => false;
    public override Alignment2x2 commentsAlignment => Alignment2x2.Default;
    public override Alignment2x2 iconAlignment => Alignment2x2.Default;

    /// <summary>获取连接的 SkillConnection 列表</summary>
    public List<SkillConnection> SkillInConnections => inConnections.OfType<SkillConnection>().ToList();
    public List<SkillConnection> SkillOutConnections => outConnections.OfType<SkillConnection>().ToList();

    /// <summary>输入端口名称列表（只读）</summary>
    public IReadOnlyList<string> InputPortNames => _inputPortNames;
    /// <summary>输出端口名称列表（只读）</summary>
    public IReadOnlyList<string> OutputPortNames => _outputPortNames;

    /// <summary>核心执行方法（协程驱动），子类必须实现</summary>
    public abstract IEnumerator Execute(SkillContext ctx);

    /// <summary>解析后继节点，默认使用 "output" 端口</summary>
    public virtual SkillNode ResolveNextNode(SkillContext ctx)
    {
        return GetConnectedNode("output");
    }

    /// <summary>根据输出端口名称获取连接的下一个 SkillNode</summary>
    protected SkillNode GetConnectedNode(string outputPortName)
    {
        var conn = SkillOutConnections.FirstOrDefault(c => c.portName == outputPortName);
        return conn?.targetNode as SkillNode;
    }

    /// <summary>获取指定输出端口上的所有连接节点（用于多端口场景）</summary>
    protected IReadOnlyList<SkillNode> GetConnectedNodes(string outputPortName)
    {
        return SkillOutConnections
            .Where(c => c.portName == outputPortName)
            .Select(c => c.targetNode as SkillNode)
            .Where(n => n != null)
            .ToList();
    }

    /// <summary>根据端口名称获取输入端连接的上游节点</summary>
    protected SkillNode GetInputSourceNode(string inputPortName)
    {
        var conn = SkillInConnections.FirstOrDefault(c => c.portName == inputPortName);
        return conn?.sourceNode as SkillNode;
    }

    /// <summary>初始化端口名称（子类在构造或 Init 中调用）</summary>
    protected void SetPortNames(IEnumerable<string> inputNames, IEnumerable<string> outputNames)
    {
        _inputPortNames = inputNames?.ToList() ?? new List<string> { "input" };
        _outputPortNames = outputNames?.ToList() ?? new List<string> { "output" };
    }

    public override void OnGraphStarted()
    {
        base.OnGraphStarted();
        if (string.IsNullOrWhiteSpace(nodeGuid))
            nodeGuid = Guid.NewGuid().ToString("N");
    }

#if UNITY_EDITOR
    /// <summary>
    ///     CanvasCore 编辑器钩子：在图形编辑器右侧检查器面板中展示自定义节点信息。
    ///     覆写 Node.OnNodeInspectorGUI()，使用 UnityEngine GUI 绘制（非 UnityEditor），
    ///     因为 SkillNode 位于 Assembly-CSharp 中，无 UnityEditor.dll 引用。
    /// </summary>
    protected override void OnNodeInspectorGUI()
    {
        // 类别色条
        var categoryColor = GetCategoryColor();
        var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(4f));
        var tex = Texture2D.whiteTexture;
        GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, false, 0, categoryColor, 0, 0);

        GUILayout.Space(4f);

        // 元数据信息
        var boldStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        GUILayout.Label("节点类型: " + GetNodeTypeName(), boldStyle);
        GUILayout.Label("类别: " + GetCategoryName());
        var guidStr = NodeGuid;
        if (!string.IsNullOrEmpty(guidStr))
            GUILayout.Label("GUID: " + (guidStr.Length > 8 ? guidStr.Substring(0, 8) : guidStr));

        GUILayout.Space(8f);

        DrawDefaultInspector();
    }

    private Color GetCategoryColor()
    {
        switch (this)
        {
            case StartNode:
            case EndNode:
            case DelayNode:
            case ParallelNode:
            case SubGraphNode:
                return new Color(0.23f, 0.5f, 0.88f);

            case DamageNode:
            case PlayVFXNode:
            case ModifyFloatNode:
            case RollChanceNode:
                return new Color(0.85f, 0.46f, 0.18f);

            case ApplyStatusNode:
            case ReactionNode:
            case ResonanceNode:
            case PaintTerrainNode:
                return new Color(0.38f, 0.68f, 0.35f);

            case ConditionNode:
            case SetValueNode:
                return new Color(0.62f, 0.37f, 0.82f);

            case LogNode:
                return new Color(0.45f, 0.45f, 0.45f);

            default:
                return new Color(0.35f, 0.35f, 0.35f);
        }
    }

    private string GetCategoryName()
    {
        switch (this)
        {
            case StartNode:
            case EndNode:
            case DelayNode:
            case ParallelNode:
            case SubGraphNode:
                return "流程控制";

            case DamageNode:
            case PlayVFXNode:
            case ModifyFloatNode:
            case RollChanceNode:
                return "输出行为";

            case ApplyStatusNode:
            case ReactionNode:
            case ResonanceNode:
            case PaintTerrainNode:
                return "战场机制";

            case ConditionNode:
            case SetValueNode:
                return "条件与数据";

            case LogNode:
                return "调试辅助";

            default:
                return "通用节点";
        }
    }

    private string GetNodeTypeName()
    {
        switch (this)
        {
            case StartNode:
                return "开始节点";
            case EndNode:
                return "结束节点";
            case DelayNode:
                return "延迟节点";
            case ParallelNode:
                return "并行节点";
            case SubGraphNode:
                return "子图节点";
            case DamageNode:
                return "伤害节点";
            case PlayVFXNode:
                return "特效节点";
            case ModifyFloatNode:
                return "数值修正节点";
            case RollChanceNode:
                return "概率判定节点";
            case ApplyStatusNode:
                return "状态挂载节点";
            case ReactionNode:
                return "元素反应节点";
            case ResonanceNode:
                return "阵型共鸣节点";
            case PaintTerrainNode:
                return "地表改写节点";
            case ConditionNode:
                return "条件分支节点";
            case SetValueNode:
                return "黑板写入节点";
            case LogNode:
                return "日志节点";
            default:
                return GetType().Name;
        }
    }
#endif
}