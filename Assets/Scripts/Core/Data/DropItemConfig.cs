using System;
using System.Collections.Generic;

/// <summary>
///     掉落项配置数据结构。
///     表示掉落表中的一个具体掉落项。
/// </summary>
[Serializable]
public class DropItemConfig
{
    /// <summary>所属掉落表ID</summary>
    public int TableID;

    /// <summary>物品ID</summary>
    public int ItemID;

    /// <summary>权重（用于随机选择）</summary>
    public int Weight;

    /// <summary>最小数量</summary>
    public int MinQty;

    /// <summary>最大数量</summary>
    public int MaxQty;

    /// <summary>全局掉率（0.0~1.0）</summary>
    public float GlobalChance;

    /// <summary>是否稀有掉落</summary>
    public bool IsRare;
}

/// <summary>
///     掉落表配置数据结构。
///     对应 DropTable.csv 的每一行数据。
///     包含该表的所有掉落项。
/// </summary>
[Serializable]
public class DropTableConfig
{
    /// <summary>掉落表唯一ID</summary>
    public int TableID;

    /// <summary>掉落表名称</summary>
    public string Name;

    /// <summary>该掉落表包含的所有掉落项</summary>
    public List<DropItemConfig> Items = new();

    /// <summary>计算总权重</summary>
    public int TotalWeight
    {
        get
        {
            int total = 0;
            foreach (var item in Items)
            {
                total += item.Weight;
            }
            return total;
        }
    }
}