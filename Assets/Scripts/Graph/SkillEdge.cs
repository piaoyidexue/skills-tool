using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     技能图有向边 —— 纯数据结构，替代 NodeCanvas Connection。
///     描述从源节点输出端口到目标节点输入端口的一条连接。
///     不依赖任何第三方框架。
/// </summary>
[Serializable]
public struct SkillEdge
{
    /// <summary>源节点 GUID</summary>
    public string SourceNodeGuid;

    /// <summary>源节点输出端口名</summary>
    public string SourcePort;

    /// <summary>目标节点 GUID</summary>
    public string TargetNodeGuid;

    /// <summary>目标节点输入端口名</summary>
    public string TargetPort;

    public SkillEdge(string sourceNodeGuid, string sourcePort, string targetNodeGuid, string targetPort)
    {
        SourceNodeGuid = sourceNodeGuid;
        SourcePort = sourcePort;
        TargetNodeGuid = targetNodeGuid;
        TargetPort = targetPort;
    }

    public override string ToString() => $"{SourceNodeGuid}.{SourcePort} → {TargetNodeGuid}.{TargetPort}";
}
