using System;
using UnityEngine;

/// <summary>
/// 单元格类型枚举（对齐设计文档）
/// </summary>
public enum GridCellType
{
    /// <summary>不可通行、不可建造</summary>
    Blocked,
    
    /// <summary>仅通行、不可建造</summary>
    Walkable,
    
    /// <summary>可建造</summary>
    Buildable,
    
    /// <summary>高台（提供视野/射程加成）</summary>
    HighGround
}

/// <summary>
/// 兼容性枚举别名（用于兼容旧代码）
/// </summary>
[Obsolete("Use GridCellType instead")]
public enum UnityGridCellType
{
    Impassable = (int)GridCellType.Blocked,
    HighGround = (int)GridCellType.HighGround,
    FlatGround = (int)GridCellType.Walkable,
    Water = (int)GridCellType.Blocked,
    Obstacle = (int)GridCellType.Blocked,
    Buildable = (int)GridCellType.Buildable,
    SpecialTerrain = (int)GridCellType.Walkable
}

/// <summary>
/// 地表元素效果枚举
/// </summary>
public enum TerrainEffect
{
    /// <summary>无效果</summary>
    None,
    
    /// <summary>冰面</summary>
    Ice,
    
    /// <summary>焦土</summary>
    Burnt,
    
    /// <summary>泥沼</summary>
    Swamp,
    
    /// <summary>岩石</summary>
    Rock,
    
    /// <summary>草丛</summary>
    Grass
}

/// <summary>
/// 0 GC 的单元格数据结构 - 包含所有必要的网格信息
/// </summary>
[Serializable]
public struct GridCellData
{
    /// <summary>
    /// 单元格类型
    /// </summary>
    public GridCellType Type;
    
    /// <summary>
    /// 占用状态（-1 = 空闲，>=0 = 被实体ID占用）
    /// </summary>
    public int OccupiedBy;
    
    /// <summary>
    /// 地表元素状态
    /// </summary>
    public TerrainEffect TerrainType;
    
    /// <summary>
    /// 地形残留时间（秒）
    /// </summary>
    public float TerrainDuration;
    
    /// <summary>
    /// 是否被标记为危险区域
    /// </summary>
    public bool IsDangerous;
    
    /// <summary>
    /// 是否被标记为安全区域
    /// </summary>
    public bool IsSafe;
    
    /// <summary>
    /// 自定义数据（用于扩展）
    /// </summary>
    public int CustomData;
    
    /// <summary>
    /// 初始化默认单元格数据
    /// </summary>
    public void Initialize()
    {
        Type = GridCellType.Walkable;
        OccupiedBy = -1;
        TerrainType = TerrainEffect.None;
        TerrainDuration = 0f;
        IsDangerous = false;
        IsSafe = false;
        CustomData = 0;
    }
    
    /// <summary>
    /// 检查单元格是否空闲
    /// </summary>
    public bool IsFree() => OccupiedBy == -1;
    
    /// <summary>
    /// 检查单元格是否可建造
    /// </summary>
    public bool IsBuildable() => Type == GridCellType.Buildable && IsFree();
    
    /// <summary>
    /// 获取单元格的字符串表示
    /// </summary>
    public override string ToString()
    {
        return $"Cell({Type}, OccupiedBy={OccupiedBy}, Terrain={TerrainType.ToString()}, CustomData={CustomData})";
    }
}