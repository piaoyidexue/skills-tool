using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     掉落计算器 —— 纯逻辑类，用于计算掉落物。
///     输入掉落表ID，根据全局掉率和权重随机抽取物品。
/// </summary>
public static class LootCalculator
{
    /// <summary>
    ///     计算掉落物。
    ///     从掉落表中根据权重随机选择一个物品，检查全局掉率后添加到结果。
    /// </summary>
    /// <param name="dropTableId">掉落表ID</param>
    /// <param name="result">结果列表（缓存，避免GC）</param>
    /// <returns>掉落物数量</returns>
    public static int CalculateLoot(int dropTableId, List<LootItem> result)
    {
        if (result == null) return 0;
        result.Clear();

        var dropTable = ConfigLoader.GetDropTableConfig(dropTableId);
        if (dropTable == null || dropTable.Items.Count == 0) return 0;

        var totalWeight = dropTable.TotalWeight;
        if (totalWeight <= 0) return 0;

        // 根据权重随机选择一个物品
        var selectedItem = WeightedRandomSelect(dropTable.Items, totalWeight);
        if (selectedItem == null) return 0;

        // 检查全局掉率
        if (selectedItem.GlobalChance > 0f && selectedItem.GlobalChance < 1f)
        {
            if (UnityEngine.Random.value > selectedItem.GlobalChance) return 0;
        }

        // 生成随机数量
        var quantity = UnityEngine.Random.Range(selectedItem.MinQty, selectedItem.MaxQty + 1);
        if (quantity <= 0) return 0;

        result.Add(new LootItem
        {
            ItemID = selectedItem.ItemID,
            Quantity = quantity,
            IsRare = selectedItem.IsRare
        });

        return result.Count;
    }

    /// <summary>
    ///     计算掉落物（支持多次掉落尝试）。
    ///     每次尝试独立进行，适用于"击杀后多次掉落"场景。
    /// </summary>
    /// <param name="dropTableId">掉落表ID</param>
    /// <param name="result">结果列表</param>
    /// <param name="dropAttempts">掉落尝试次数</param>
    /// <returns>掉落物总数量</returns>
    public static int CalculateLootWithAttempts(int dropTableId, List<LootItem> result, int dropAttempts = 1)
    {
        if (result == null || dropAttempts <= 0) return 0;

        for (int i = 0; i < dropAttempts; i++)
        {
            CalculateLoot(dropTableId, result);
        }

        return result.Count;
    }

    /// <summary>
    ///     根据权重随机选择一个物品。
    /// </summary>
    private static DropItemConfig WeightedRandomSelect(List<DropItemConfig> items, int totalWeight)
    {
        if (items == null || items.Count == 0 || totalWeight <= 0) return null;

        var roll = UnityEngine.Random.Range(0, totalWeight);
        var accumulated = 0;

        foreach (var item in items)
        {
            accumulated += item.Weight;
            if (roll < accumulated)
            {
                return item;
            }
        }

        // 兜底返回最后一个（理论上不应该走到这里）
        return items[items.Count - 1];
    }

    /// <summary>
    ///     根据掉落表获取所有可能的掉落物品（不进行随机）。
    ///     用于预览或展示所有可掉落物品。
    /// </summary>
    /// <param name="dropTableId">掉落表ID</param>
    /// <returns>可掉落物品列表</returns>
    public static IReadOnlyList<DropItemConfig> GetPossibleDrops(int dropTableId)
    {
        var dropTable = ConfigLoader.GetDropTableConfig(dropTableId);
        return dropTable?.Items ?? new List<DropItemConfig>();
    }
}

/// <summary>
///     掉落物项结构体。
/// </summary>
[Serializable]
public struct LootItem
{
    public int ItemID;
    public int Quantity;
    public bool IsRare;

    public override string ToString() => $"LootItem(ItemID={ItemID}, Qty={Quantity}, Rare={IsRare})";
}