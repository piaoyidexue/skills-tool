using System;
using System.Collections.Generic;
using SkillAI;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
///     怪物工厂 —— 负责按需实例化怪物实体。
///     基于 CSV 配置 + 对象池 + 属性系统。
///     遵循单一职责原则：创建实例并注入属性，AI 数据直接注入到 AIController/MinionBrain。
/// </summary>
public static class MonsterFactory
{
    /// <summary>怪物对象池</summary>
    private static readonly Dictionary<string, ObjectPool<GameObject>> _monsterPools = new();

    /// <summary>预加载的怪物预制体字典</summary>
    private static readonly Dictionary<string, GameObject> _prefabCache = new();

    /// <summary>
    ///     创建怪物实例。
    /// </summary>
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
    ///     批量创建小队。
    ///     包含完整的初始化流程：创建实例 → 注入属性 → 初始化 AI 组件 → 应用词缀 → 注册仇恨系统。
    /// </summary>
    public static Transform[] CreateSquad(int squadId, int level, Vector3 spawnPosition, Quaternion spawnRotation,
        MonsterSpawnContext? context = null, int[] affixIds = null)
    {
        var squadConfig = ConfigLoader.GetSquadConfig(squadId);
        if (squadConfig == null)
        {
            Debug.LogError($"[MonsterFactory] Squad config not found: {squadId}");
            return new Transform[0];
        }

        var transforms = new List<Transform>();

        for (int i = 0; i < squadConfig.MemberCount && i < squadConfig.MonsterIDs.Count; i++)
        {
            var monsterId = squadConfig.MonsterIDs[i];
            var offset = new Vector3((i - squadConfig.MemberCount / 2) * 1.5f, 0f, 0f);
            var spawnPos = spawnPosition + offset;

            var monsterTransform = CreateMonster(monsterId, level, spawnPos, spawnRotation);
            if (monsterTransform == null) continue;

            if (context.HasValue)
            {
                InjectAIContext(monsterTransform, monsterId, level, context.Value);
            }

            if (affixIds != null && affixIds.Length > 0)
            {
                AffixApplier.ApplyAffixes(monsterTransform, affixIds);
            }

            if (squadId > 0)
            {
                AggroManager.RegisterMonster(monsterTransform, squadId);
            }

            transforms.Add(monsterTransform);
        }

        return transforms.ToArray();
    }

    /// <summary>
    ///     向 AI 组件注入上下文数据。
    /// </summary>
    private static void InjectAIContext(Transform monsterTransform, int monsterId, int level, MonsterSpawnContext context)
    {
        var monsterConfig = ConfigLoader.GetMonsterConfig(monsterId);
        var aiTier = monsterConfig?.AiTier ?? "minion";

        switch (aiTier.ToLowerInvariant())
        {
            case "minion":
                var minionBrain = monsterTransform.GetComponent<MinionBrain>();
                if (minionBrain != null)
                {
                    minionBrain.InjectNavigation(context);
                }
                break;

            case "elite":
            case "boss":
                var aiController = monsterTransform.GetComponent<AIController>();
                if (aiController != null)
                {
                    aiController.SquadId = context.SquadID;
                    aiController.SetBBValue("MonsterID", monsterId);
                    aiController.SetBBValue("Level", level);
                    aiController.SetBBValue("AiTier", aiTier);
                    aiController.SetBBValue("TargetMode", (int)context.TargetMode);
                    aiController.SetBBValue("SquadID", context.SquadID);
                }
                break;
        }
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
                if (instance.GetComponent<MinionBrain>() == null)
                    instance.AddComponent<MinionBrain>();
                break;
            case "elite":
            case "boss":
                if (instance.GetComponent<AIController>() == null)
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