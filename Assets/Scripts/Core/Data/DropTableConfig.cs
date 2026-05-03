using System;

/// <summary>
///     掉落表配置数据结构。
///     对应 DropTable.csv 的每一行数据。
/// </summary>
[Serializable]
public class DropTableConfig
{
    /// <summary>掉落表唯一ID</summary>
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
}