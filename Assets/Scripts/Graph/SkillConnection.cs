using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

/// <summary>
///     技能连接 —— 扩展 CanvasCore Connection，支持命名端口。
///     用于区分 ConditionNode 的 true/false 端口、ParallelNode 的动态分支等场景。
/// </summary>
[System.Serializable]
[Name("Skill Connection")]
public class SkillConnection : Connection
{
    /// <summary>
    ///     端口名称。对于普通节点为 "input"/"output"，
    ///     对于 ConditionNode 为 "truePort"/"falsePort"，
    ///     对于 ParallelNode 为 "branches 0", "branches 1" 等。
    /// </summary>
    [SerializeField]
    [Tooltip("端口名称，用于区分多端口场景")]
    private string _portName = "output";

    /// <summary>此连接对应的源节点输出端口名</summary>
    public string portName
    {
        get => string.IsNullOrEmpty(_portName) ? "output" : _portName;
        set => _portName = value;
    }

    /// <summary>创建带端口名称的连接</summary>
    public static SkillConnection Create(SkillNode source, SkillNode target, string portName, int sourceIndex = -1, int targetIndex = -1)
    {
        if (source == null || target == null) return null;

        var conn = Create(source, target, sourceIndex, targetIndex) as SkillConnection;
        if (conn != null)
            conn._portName = portName;
        return conn;
    }
}
