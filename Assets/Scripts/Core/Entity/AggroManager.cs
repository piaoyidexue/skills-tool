using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     仇恨管理器 —— 处理怪物间的仇恨联动。
///     当一只怪物发现玩家时，向同小队的其他怪物广播仇恨事件。
/// </summary>
public static class AggroManager
{
    /// <summary>仇恨事件</summary>
    public static event Action<Transform, Transform> OnMonsterAggroed;

    /// <summary>
    ///     注册怪物到仇恨系统。
    /// </summary>
    /// <param name="monsterTransform">怪物Transform</param>
    /// <param name="squadId">所属小队ID</param>
    public static void RegisterMonster(Transform monsterTransform, int squadId)
    {
        if (monsterTransform == null) return;

        // 存储怪物与小队的映射关系
        var squadMonsters = GetSquadMonsters(squadId);
        if (!squadMonsters.Contains(monsterTransform))
        {
            squadMonsters.Add(monsterTransform);
        }
    }

    /// <summary>
    ///     移除怪物从仇恨系统。
    /// </summary>
    /// <param name="monsterTransform">怪物Transform</param>
    /// <param name="squadId">所属小队ID</param>
    public static void UnregisterMonster(Transform monsterTransform, int squadId)
    {
        if (monsterTransform == null) return;

        var squadMonsters = GetSquadMonsters(squadId);
        squadMonsters.Remove(monsterTransform);
    }

    /// <summary>
    ///     触发仇恨事件。
    /// </summary>
    /// <param name="aggroer">触发仇恨的怪物</param>
    /// <param name="target">目标（通常是玩家）</param>
    public static void TriggerAggro(Transform aggroer, Transform target)
    {
        if (aggroer == null || target == null) return;

        // 获取怪物的小队ID
        var blackboardComponent = aggroer.GetComponent<BlackboardComponent>();
        if (blackboardComponent == null) return;
        var blackboard = blackboardComponent.Blackboard;

        var squadId = blackboard.GetValue<int>("SquadID", 0);
        if (squadId <= 0) return;

        // 获取同小队的所有怪物
        var squadMonsters = GetSquadMonsters(squadId);
        if (squadMonsters.Count <= 1) return;

        // 向所有同小队怪物广播仇恨事件
        foreach (var monster in squadMonsters)
        {
            if (monster != aggroer)
            {
                OnMonsterAggroed?.Invoke(monster, target);
            }
        }
    }

    /// <summary>
    ///     获取小队怪物列表。
    /// </summary>
    private static List<Transform> GetSquadMonsters(int squadId)
    {
        if (!_squadMonsters.TryGetValue(squadId, out var monsters))
        {
            monsters = new List<Transform>();
            _squadMonsters[squadId] = monsters;
        }
        return monsters;
    }

    /// <summary>小队怪物字典</summary>
    private static readonly Dictionary<int, List<Transform>> _squadMonsters = new();
}