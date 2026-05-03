using System;
using UnityEngine;

/// <summary>
///     怪物生成上下文载荷 —— 生成器与怪物之间的唯一契约。
///     遵循迪米特法则：生成器只发快递，不拆包裹；怪物自己拆箱并整理内部状态。
/// </summary>
public struct MonsterSpawnContext
{
    /// <summary>初始目标模式</summary>
    public AITargetMode TargetMode;

    /// <summary>路径点数组（塔防专用）</summary>
    public Transform[] PathNodes;

    /// <summary>巡逻中心点（ARPG专用）</summary>
    public Vector3 PatrolCenter;

    /// <summary>巡逻半径（ARPG专用）</summary>
    public float PatrolRadius;

    /// <summary>是否启用仇恨联动</summary>
    public bool EnableAggroPropagation;

    /// <summary>小队ID（用于仇恨管理）</summary>
    public int SquadID;

    /// <summary>显式构造函数（必需）</summary>
    public MonsterSpawnContext(AITargetMode targetMode, Transform[] pathNodes, Vector3 patrolCenter, float patrolRadius, bool enableAggroPropagation, int squadID)
    {
        TargetMode = targetMode;
        PathNodes = pathNodes;
        PatrolCenter = patrolCenter;
        PatrolRadius = patrolRadius;
        EnableAggroPropagation = enableAggroPropagation;
        SquadID = squadID;
    }
}