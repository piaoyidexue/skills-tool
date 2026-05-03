using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     掉落计算器 —— 纯逻辑类，用于计算掉落物。
///     输入掉落表ID，输出物品ID和数量列表。
/// </summary>
public static class LootCalculator
{
    /// <summary>
    ///     计算掉落物。
    /// </summary>
    /// <param name="dropTableId">掉落表ID</param>
    /// <param name="result">结果列表（缓存，避免GC）</param>
    /// <returns>掉落物数量</returns>
    public static int CalculateLoot(int dropTableId, List<LootItem> result)
    {
        if (result == null) return 0;

        result.Clear();

        // 获取掉落表配置
        var dropTableConfigs = ConfigLoader.GetAllDropTableConfigs();
        if (dropTableConfigs == null) return 0;

        // 过滤出指定掉落表ID的配置
        var tableEntries = new List<DropTableConfig>();
        foreach (var config in dropTableConfigs)
        {
            if (config.TableID == dropTableId)
            {
                tableEntries.Add(config);
            }
        }

        if (tableEntries.Count == 0) return 0;

        // 按权重排序
        tableEntries.Sort((a, b) => b.Weight.CompareTo(a.Weight));

        // 执行权重Roll点算法
        int totalWeight = 0;
        foreach (var entry in tableEntries)
        {
            totalWeight += entry.Weight;
        }

        if (totalWeight <= 0) return 0;

        // 随机选择一个条目
        var roll = UnityEngine.Random.Range(0, totalWeight);
        int accumulated = 0;

        foreach (var entry in tableEntries)
        {
            accumulated += entry.Weight;
            if (roll < accumulated)
            {
                // 检查全局掉率
                if (UnityEngine.Random.value < entry.GlobalChance)
                {
                    // 生成随机数量
                    var qty = UnityEngine.Random.Range(entry.MinQty, entry.MaxQty + 1);
                    result.Add(new LootItem { ItemID = entry.ItemID, Quantity = qty });
                }
                break;
            }
        }

        return result.Count;
    }
}

/// <summary>
///     掉落物项结构体。
/// </summary>
public struct LootItem
{
    public int ItemID;
    public int Quantity;
}