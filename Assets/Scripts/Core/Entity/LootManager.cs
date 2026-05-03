using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     掉落管理器 —— 监听敌人死亡事件，处理掉落逻辑。
/// </summary>
public class LootManager : MonoBehaviour
{
    /// <summary>掉落物缓存列表</summary>
    private readonly List<LootItem> _lootCache = new();

    /// <summary>
    ///     初始化时注册事件监听。
    /// </summary>
    private void Awake()
    {
        GlobalEventBus.Subscribe<EntityDeathEvent>(OnEnemyDied);
    }

    /// <summary>
    ///     清理时注销事件监听。
    /// </summary>
    private void OnDestroy()
    {
        GlobalEventBus.Unsubscribe<EntityDeathEvent>(OnEnemyDied);
    }

    /// <summary>
    ///     敌人死亡事件处理。
    /// </summary>
    private void OnEnemyDied(EntityDeathEvent eventData)
    {
        if (eventData.Entity == null) return;

        // 获取怪物配置
        var monsterConfig = ConfigLoader.GetMonsterConfig(eventData.EntityId);
        if (monsterConfig == null || monsterConfig.DropTableID <= 0) return;

        // 计算掉落物
        var lootCount = LootCalculator.CalculateLoot(monsterConfig.DropTableID, _lootCache);
        if (lootCount == 0) return;

        // 在地图上生成掉落物
        foreach (var loot in _lootCache)
        {
            SpawnLootPickup(eventData.Entity.position, loot.ItemID, loot.Quantity);
        }
    }

    /// <summary>
    ///     生成掉落物拾取实体。
    /// </summary>
    private void SpawnLootPickup(Vector3 position, int itemId, int quantity)
    {
        // 查找掉落物预制体
        var lootPrefab = Resources.Load<GameObject>($"Prefabs/Loot/{itemId}");
        if (lootPrefab == null)
        {
            // 使用默认掉落物预制体
            lootPrefab = Resources.Load<GameObject>("Prefabs/Loot/DefaultLoot");
        }

        if (lootPrefab == null)
        {
            Debug.LogWarning("[LootManager] Default loot prefab not found.");
            return;
        }

        // 实例化掉落物
        var lootInstance = GameObject.Instantiate(lootPrefab, position, Quaternion.identity);
        
        // 设置掉落物数据
        var lootComponent = lootInstance.GetComponent<LootPickup>();
        if (lootComponent != null)
        {
            lootComponent.ItemID = itemId;
            lootComponent.Quantity = quantity;
        }
    }
}