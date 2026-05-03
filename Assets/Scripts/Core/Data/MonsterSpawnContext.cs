using System;
using UnityEngine;

/// <summary>
///     怪物生成上下文载荷 —— 生成器与怪物之间的唯一契约。
///     遵循迪米特法则：生成器只发快递，不拆包裹；怪物自己拆箱并整理内部状态。
/// </summary>
[Serializable]
public struct MonsterSpawnContext
{
    /// <summary>怪物ID</summary>
    public int MonsterID;

    /// <summary>怪物等级</summary>
    public int Level;

    /// <summary>怪物AI等级（Minion/Elite/Boss）</summary>
    public string AiTier;

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

    /// <summary>
    ///     创建塔防模式的上下文
    /// </summary>
    public static MonsterSpawnContext CreateForTowerDefense(int monsterId, int level, string aiTier, Transform[] pathNodes, int squadId)
    {
        return new MonsterSpawnContext
        {
            MonsterID = monsterId,
            Level = level,
            AiTier = aiTier,
            TargetMode = AITargetMode.Waypoint,
            PathNodes = pathNodes,
            SquadID = squadId,
            EnableAggroPropagation = true
        };
    }

    /// <summary>
    ///     创建ARPG模式的上下文
    /// </summary>
    public static MonsterSpawnContext CreateForARPG(int monsterId, int level, string aiTier, Vector3 patrolCenter, float patrolRadius, int squadId)
    {
        return new MonsterSpawnContext
        {
            MonsterID = monsterId,
            Level = level,
            AiTier = aiTier,
            TargetMode = AITargetMode.FreeRoam,
            PatrolCenter = patrolCenter,
            PatrolRadius = patrolRadius,
            SquadID = squadId,
            EnableAggroPropagation = true
        };
    }
}