using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     子图节点 —— 将执行权下放给引用的 SkillGraphAsset，实现逻辑块级复用。
///     核心机制：
///     - 执行到本节点时，将子图压入 SkillExecution 的帧栈
///     - 黑板桥接：通过 Mapping 表将父图黑板值映射到子图黑板（参数透传）
///     - 子图执行完毕后自动回到本节点的后续节点
///     - 支持返回值映射：子图黑板值回写到父图黑板
/// </summary>
[CreateAssetMenu(fileName = "SubGraphNode", menuName = "Skill System/Nodes/Flow/SubGraph")]
public class SubGraphNode : SkillNodeBase
{
    // ──────────── 子图引用 ────────────

    /// <summary>引用的子图资产（公共逻辑块，如 Common_ImpactDamage）</summary>
    [Tooltip("引用的子图资产")] public SkillGraphAsset subGraph;

    // ──────────── 黑板桥接：入参映射（父图 → 子图） ────────────

    /// <summary>
    ///     入参映射表：将父图黑板的值写入子图黑板。
    ///     Key = 父图黑板键, Value = 子图黑板键。
    ///     执行子图前自动写入，实现参数透传。
    /// </summary>
    [Tooltip("入参映射：父图Key → 子图Key")] public List<BBMapping> inputMappings = new();

    // ──────────── 黑板桥接：返回值映射（子图 → 父图） ────────────

    /// <summary>
    ///     返回值映射表：子图执行完毕后，将子图黑板的值回写到父图黑板。
    ///     Key = 子图黑板键, Value = 父图黑板键。
    /// </summary>
    [Tooltip("返回值映射：子图Key → 父图Key")] public List<BBMapping> outputMappings = new();

    // ──────────── 运行时状态 ────────────

    /// <summary>子图执行是否已完成</summary>
    [System.NonSerialized] private bool _subGraphCompleted;

    /// <summary>子图的独立黑板（桥接层，隔离父子图黑板）</summary>
    [System.NonSerialized] private Blackboard _subBlackboard;

    /// <summary>保存子图执行前的父图黑板引用</summary>
    [System.NonSerialized] private Blackboard _parentBlackboard;

    public override void OnEnter(SkillContext ctx)
    {
        _subGraphCompleted = false;
        _subBlackboard = null;
        _parentBlackboard = null;

        if (subGraph == null)
        {
            Debug.LogError("[SubGraphNode] Missing subGraph reference.");
            return;
        }

        // 保存父图黑板引用
        _parentBlackboard = ctx.Blackboard;

        // 创建子图独立黑板并桥接入参
        _subBlackboard = new Blackboard();
        ApplyInputMappings(_parentBlackboard, _subBlackboard);

        // 将子图黑板设置为当前上下文黑板
        ctx.Blackboard = _subBlackboard;

        // 通过 SkillExecution 的帧栈机制，子图会在当前帧栈中执行
        // SkillExecution.Initialize 会自动将子图压栈
    }

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        if (subGraph == null) return NodeTickResult.Success;

        // 子图执行由 SkillExecution 的帧栈自动管理。
        // 本节点在 OnEnter 中已完成黑板桥接，
        // 帧栈弹出时（OnGraphComplete）会自动恢复父图黑板。
        // 这里只返回 Success 表示本节点职责已完成。
        return NodeTickResult.Success;
    }

    public override void OnExit(SkillContext ctx)
    {
        // 子图执行完毕，应用返回值映射
        if (_subBlackboard != null && _parentBlackboard != null)
        {
            ApplyOutputMappings(_subBlackboard, _parentBlackboard);
        }

        // 恢复父图黑板
        if (_parentBlackboard != null && ctx != null)
        {
            ctx.Blackboard = _parentBlackboard;
        }

        _subBlackboard = null;
        _parentBlackboard = null;
    }

    // ──────────── 黑板桥接 ────────────

    /// <summary>
    ///     将父图黑板的值映射写入子图黑板（入参透传）。
    /// </summary>
    private void ApplyInputMappings(Blackboard source, Blackboard target)
    {
        if (inputMappings == null) return;
        foreach (var mapping in inputMappings)
        {
            if (string.IsNullOrEmpty(mapping.SourceKey) || string.IsNullOrEmpty(mapping.TargetKey)) continue;
            if (source.TryGetValue<object>(mapping.SourceKey, out var value))
                target.SetValue(mapping.TargetKey, value);
        }
    }

    /// <summary>
    ///     将子图黑板的值映射回写到父图黑板（返回值提取）。
    /// </summary>
    private void ApplyOutputMappings(Blackboard source, Blackboard target)
    {
        if (outputMappings == null) return;
        foreach (var mapping in outputMappings)
        {
            if (string.IsNullOrEmpty(mapping.SourceKey) || string.IsNullOrEmpty(mapping.TargetKey)) continue;
            if (source.TryGetValue<object>(mapping.SourceKey, out var value))
                target.SetValue(mapping.TargetKey, value);
        }
    }
}

/// <summary>
///     黑板键映射条目 —— 用于子图节点的入参/返回值桥接。
/// </summary>
[System.Serializable]
public class BBMapping
{
    /// <summary>源黑板键（入参时=父图键，返回值时=子图键）</summary>
    [Tooltip("源黑板键")] public string SourceKey;

    /// <summary>目标黑板键（入参时=子图键，返回值时=父图键）</summary>
    [Tooltip("目标黑板键")] public string TargetKey;

    public BBMapping() { }

    public BBMapping(string sourceKey, string targetKey)
    {
        SourceKey = sourceKey;
        TargetKey = targetKey;
    }
}
