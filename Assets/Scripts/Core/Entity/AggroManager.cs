using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     仇恨管理器 —— 处理怪物间的仇恨联动。
///     当一只怪物发现玩家时，向同小队的其他怪物广播仇恨事件。
/// </summary>
public static class AggroManager
{
    private static readonly Dictionary<int, List<Transform>> _squadMonsters = new();

    /// <summary>仇恨事件</summary>
    public static event Action<Transform, Transform> OnMonsterAggroed;

    /// <summary>
    ///     注册怪物到仇恨系统。
    /// </summary>
    public static void RegisterMonster(Transform monsterTransform, int squadId)
    {
        if (monsterTransform == null || squadId <= 0) return;

        var squadMonsters = GetSquadMonsters(squadId);
        if (!squadMonsters.Contains(monsterTransform))
        {
            squadMonsters.Add(monsterTransform);
        }
    }

    /// <summary>
    ///     移除怪物从仇恨系统。
    /// </summary>
    public static void UnregisterMonster(Transform monsterTransform, int squadId)
    {
        if (monsterTransform == null || squadId <= 0) return;

        var squadMonsters = GetSquadMonsters(squadId);
        squadMonsters.Remove(monsterTransform);
    }

    /// <summary>
    ///     触发仇恨事件。
    /// </summary>
    public static void TriggerAggro(Transform aggroer, Transform target)
    {
        if (aggroer == null || target == null) return;

        var squadId = GetSquadId(aggroer);
        if (squadId <= 0) return;

        var squadMonsters = GetSquadMonsters(squadId);
        if (squadMonsters.Count <= 1) return;

        foreach (var monster in squadMonsters)
        {
            if (monster != aggroer)
            {
                OnMonsterAggroed?.Invoke(monster, target);
            }
        }
    }

    /// <summary>
    ///     从 AI 组件获取小队ID。
    /// </summary>
    private static int GetSquadId(Transform monsterTransform)
    {
        var aiController = monsterTransform.GetComponent<SkillAI.AIController>();
        if (aiController != null)
            return aiController.SquadId;

        var minionBrain = monsterTransform.GetComponent<MinionBrain>();
        if (minionBrain != null)
            return minionBrain.SquadId;

        return 0;
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
}