using System;
using System.Collections.Generic;
using SkillAI;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
///     怪物工厂 —— 负责按需实例化怪物实体。
///     基于 CSV 配置 + 对象池 + 属性系统。
///     遵循单一职责原则：只负责创建实例和注入属性，不直接访问黑板系统。
///     黑板数据通过 IMonsterInitializer 接口由生成器注入。
/// </summary>
public static class MonsterFactory
{
    /// <summary>怪物对象池</summary>
    private static readonly Dictionary<string, ObjectPool<GameObject>> _monsterPools = new();

    /// <summary>预加载的怪物预制体字典</summary>
    private static readonly Dictionary<string, GameObject> _prefabCache = new();

    /// <summary>
    ///     创建怪物实例。
    ///     从对象池获取壳子，注入属性和AI组件。
    ///     黑板数据由调用者通过 IMonsterInitializer.Initialize() 注入。
    /// </summary>
    /// <param name="monsterId">怪物ID</param>
    /// <param name="level">怪物等级</param>
    /// <param name="spawnPosition">生成位置</param>
    /// <param name="spawnRotation">生成旋转</param>
    /// <returns>创建的怪物Transform</returns>
    public static Transform CreateMonster(int monsterId, int level, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        var config = ConfigLoader.GetMonsterConfig(monsterId);
        if (config == null)
        {
            Debug.LogError($"[MonsterFactory] Monster config not found: {monsterId}");
            return null;
        }

        var instance = CreateMonsterInstance(config, spawnPosition, spawnRotation);
        if (instance == null) return null;

        InjectAttributes(instance, config, level);
        MountAIComponent(instance, config);

        return instance.transform;
    }

    /// <summary>
    ///     创建怪物实例并返回创建信息。
    ///     包含 Transform 和对应的配置信息，用于生成器注入上下文。
    /// </summary>
    public static MonsterSpawnResult CreateMonsterWithContext(int monsterId, int level, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        var config = ConfigLoader.GetMonsterConfig(monsterId);
        if (config == null)
        {
            Debug.LogError($"[MonsterFactory] Monster config not found: {monsterId}");
            return default;
        }

        var instance = CreateMonsterInstance(config, spawnPosition, spawnRotation);
        if (instance == null) return default;

        InjectAttributes(instance, config, level);
        MountAIComponent(instance, config);

        return new MonsterSpawnResult
        {
            Transform = instance.transform,
            MonsterID = monsterId,
            Level = level,
            AiTier = config.AiTier,
            SquadID = 0
        };
    }

    /// <summary>
    ///     批量创建小队并返回创建信息列表。
    /// </summary>
    public static List<MonsterSpawnResult> CreateSquadWithContext(int squadId, int level, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        var results = new List<MonsterSpawnResult>();
        var squadConfig = ConfigLoader.GetSquadConfig(squadId);
        if (squadConfig == null)
        {
            Debug.LogError($"[MonsterFactory] Squad config not found: {squadId}");
            return results;
        }

        for (int i = 0; i < squadConfig.MemberCount && i < squadConfig.MonsterIDs.Count; i++)
        {
            var monsterId = squadConfig.MonsterIDs[i];
            var offset = new Vector3((i - squadConfig.MemberCount / 2) * 1.5f, 0f, 0f);
            var result = CreateMonsterWithContext(monsterId, level, spawnPosition + offset, spawnRotation);
            if (result.Transform != null)
            {
                result.SquadID = squadId;
                results.Add(result);
            }
        }

        return results;
    }

    /// <summary>
    ///     批量创建小队（兼容旧接口）。
    /// </summary>
    [Obsolete("Use CreateSquadWithContext instead for proper initialization.")]
    public static Transform[] CreateSquad(int squadId, int level, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        var results = CreateSquadWithContext(squadId, level, spawnPosition, spawnRotation);
        var transforms = new Transform[results.Count];
        for (int i = 0; i < results.Count; i++)
        {
            transforms[i] = results[i].Transform;
        }
        return transforms;
    }

    /// <summary>
    ///     创建怪物实例（内部方法）。
    /// </summary>
    private static GameObject CreateMonsterInstance(MonsterConfig config, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        var prefab = GetPrefab(config.PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[MonsterFactory] Prefab not found: {config.PrefabPath}");
            return null;
        }

        var poolKey = config.PrefabPath;
        var instance = GetOrCreatePool(poolKey).Get();
        if (instance == null)
        {
            Debug.LogError($"[MonsterFactory] Failed to get instance from pool: {poolKey}");
            return null;
        }

        instance.transform.position = spawnPosition;
        instance.transform.rotation = spawnRotation;

        return instance;
    }

    /// <summary>
    ///     注入怪物属性。
    /// </summary>
    private static void InjectAttributes(GameObject instance, MonsterConfig config, int level)
    {
        var attributeSet = instance.GetComponent<MonsterAttributeSet>();
        if (attributeSet != null)
        {
            var leveledAttributes = attributeSet.CalculateForLevel(level);

            var attributeRuntime = instance.GetComponent<MonsterAttributeRuntime>();
            if (attributeRuntime == null)
            {
                attributeRuntime = instance.AddComponent<MonsterAttributeRuntime>();
            }
            attributeRuntime.InitializeFrom(leveledAttributes);
        }
    }

    /// <summary>
    ///     挂载AI组件。
    /// </summary>
    private static void MountAIComponent(GameObject instance, MonsterConfig config)
    {
        var aiTier = config.AiTier?.ToLowerInvariant() ?? "minion";
        switch (aiTier)
        {
            case "minion":
                instance.AddComponent<MinionBrain>();
                break;
            case "elite":
            case "boss":
                instance.AddComponent<AIController>();
                break;
        }
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

        if (_prefabCache.TryGetValue(prefabPath, out var cached))
            return cached;

        var prefab = Resources.Load<GameObject>(prefabPath);
        if (prefab != null)
        {
            _prefabCache[prefabPath] = prefab;
        }
        return prefab;
    }
}

/// <summary>
///     怪物创建结果 —— 包含创建的怪物实例及其配置信息。
///     用于生成器创建正确的初始化上下文。
/// </summary>
public struct MonsterSpawnResult
{
    public Transform Transform;
    public int MonsterID;
    public int Level;
    public string AiTier;
    public int SquadID;
}