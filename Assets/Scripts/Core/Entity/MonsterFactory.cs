using System;
using System.Collections.Generic;
using SkillAI;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
///     怪物工厂 —— 负责按需实例化怪物实体。
///     基于 CSV 配置 + 对象池 + GAS 属性系统。
/// </summary>
public static class MonsterFactory
{
    /// <summary>怪物对象池</summary>
    private static readonly Dictionary<string, ObjectPool<GameObject>> _monsterPools = new();

    /// <summary>预加载的怪物预制体字典</summary>
    private static readonly Dictionary<int, GameObject> _prefabCache = new();

    /// <summary>
    ///     创建怪物实例。
    ///     从对象池获取壳子，注入属性、AI组件和词缀效果。
    /// </summary>
    /// <param name="monsterId">怪物ID</param>
    /// <param name="level">怪物等级</param>
    /// <param name="spawnPosition">生成位置</param>
    /// <param name="spawnRotation">生成旋转</param>
    /// <returns>创建的怪物Transform</returns>
    public static Transform CreateMonster(int monsterId, int level, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        // 获取怪物配置
        var config = ConfigLoader.GetMonsterConfig(monsterId);
        if (config == null)
        {
            Debug.LogError($"[MonsterFactory] Monster config not found: {monsterId}");
            return null;
        }

        // 获取预制体
        var prefab = GetPrefab(config.PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[MonsterFactory] Prefab not found: {config.PrefabPath}");
            return null;
        }

        // 从对象池获取实例
        var poolKey = config.PrefabPath;
        var instance = GetOrCreatePool(poolKey).Get();
        if (instance == null)
        {
            Debug.LogError($"[MonsterFactory] Failed to get instance from pool: {poolKey}");
            return null;
        }

        // 设置位置和旋转
        instance.transform.position = spawnPosition;
        instance.transform.rotation = spawnRotation;

        // 注入属性
        var attributeSet = instance.GetComponent<MonsterAttributeSet>();
        if (attributeSet != null)
        {
            var leveledAttributes = attributeSet.CalculateForLevel(level);
            // 这里需要将计算后的属性应用到怪物身上
            // 实际项目中会通过GEHost或自定义组件来管理
        }

        // 挂载AI组件
        var aiTier = config.AiTier.ToLowerInvariant();
        switch (aiTier)
        {
            case "minion":
                instance.AddComponent<MinionBrain>();
                break;
            case "elite":
                instance.AddComponent<AIController>();
                break;
            case "boss":
                instance.AddComponent<AIController>();
                break;
        }

        // 设置黑板变量（用于AI行为树）
        var blackboardComponent = instance.GetComponent<BlackboardComponent>();
        if (blackboardComponent != null)
        {
            var blackboard = blackboardComponent.Blackboard;
            blackboard.SetValue("MonsterID", monsterId);
            blackboard.SetValue("Level", level);
            blackboard.SetValue("AiTier", aiTier);
        }

        // 返回Transform
        return instance.transform;
    }

    /// <summary>
    ///     批量创建小队。
    /// </summary>
    /// <param name="squadId">小队ID</param>
    /// <param name="spawnPosition">生成位置</param>
    /// <param name="spawnRotation">生成旋转</param>
    /// <returns>创建的怪物Transform数组</returns>
    public static Transform[] CreateSquad(int squadId, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        var squadConfig = ConfigLoader.GetSquadConfig(squadId);
        if (squadConfig == null)
        {
            Debug.LogError($"[MonsterFactory] Squad config not found: {squadId}");
            return new Transform[0];
        }

        var transforms = new Transform[squadConfig.MemberCount];
        for (int i = 0; i < squadConfig.MemberCount && i < squadConfig.MonsterIDs.Count; i++)
        {
            var monsterId = squadConfig.MonsterIDs[i];
            var offset = new Vector3((i - squadConfig.MemberCount / 2) * 1.5f, 0f, 0f);
            transforms[i] = CreateMonster(monsterId, 1, spawnPosition + offset, spawnRotation);
        }

        return transforms;
    }

    /// <summary>
    ///     获取或创建对象池。
    /// </summary>
    private static ObjectPool<GameObject> GetOrCreatePool(string poolKey)
    {
        if (!_monsterPools.TryGetValue(poolKey, out var pool))
        {
            pool = new ObjectPool<GameObject>(() =>
            {
                var prefab = GetPrefab(poolKey);
                return prefab != null ? GameObject.Instantiate(prefab) : null;
            }, obj =>
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                    obj.transform.SetParent(null);
                }
            });
            _monsterPools[poolKey] = pool;
        }
        return pool;
    }

    /// <summary>
    ///     根据路径获取预制体。
    /// </summary>
    private static GameObject GetPrefab(string prefabPath)
    {
        if (string.IsNullOrEmpty(prefabPath)) return null;

        if (_prefabCache.TryGetValue(prefabPath.GetHashCode(), out var cached))
            return cached;

        var prefab = Resources.Load<GameObject>(prefabPath);
        if (prefab != null)
        {
            _prefabCache[prefabPath.GetHashCode()] = prefab;
        }
        return prefab;
    }
}