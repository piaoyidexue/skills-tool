using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 网格坐标结构体 - 统一的二维坐标表示，用于正方形和六边形网格
/// U 和 V 坐标在不同拓扑中有不同含义：
/// - 正方形：U = x, V = y
/// - 六边形（尖顶）：U = q (列), V = r (行)
/// </summary>
public struct GridCoord : IEquatable<GridCoord>
{
    public int U;
    public int V;
    
    public GridCoord(int u, int v)
    {
        U = u;
        V = v;
    }
    
    public static GridCoord Zero => new GridCoord(0, 0);
    
    public override string ToString()
    {
        return $"({U}, {V})";
    }
    
    public bool Equals(GridCoord other)
    {
        return U == other.U && V == other.V;
    }
    
    public override bool Equals(object obj)
    {
        return obj is GridCoord other && Equals(other);
    }
    
    public override int GetHashCode()
    {
        unchecked
        {
            return (U * 397) ^ V;
        }
    }
    
    public static bool operator ==(GridCoord left, GridCoord right)
    {
        return left.Equals(right);
    }
    
    public static bool operator !=(GridCoord left, GridCoord right)
    {
        return !left.Equals(right);
    }
    
    public static GridCoord operator +(GridCoord a, GridCoord b)
    {
        return new GridCoord(a.U + b.U, a.V + b.V);
    }
    
    public static GridCoord operator -(GridCoord a, GridCoord b)
    {
        return new GridCoord(a.U - b.U, a.V - b.V);
    }
}

/// <summary>
/// 网格拓扑接口 - 定义所有与网格形状相关的数学运算
/// 业务系统通过此接口与网格交互，完全屏蔽底层形状差异
/// 
/// 设计原则：
/// 1. 提供 0 GC 版本的方法（使用 ref 参数和 out 参数）
/// 2. 同时保留 List<T> 版本以保持向后兼容
/// </summary>
public interface IGridTopology
{
    /// <summary>
    /// 将世界坐标转换为网格坐标
    /// </summary>
    GridCoord WorldToGrid(Vector3 worldPosition);
    
    /// <summary>
    /// 将网格坐标转换为世界坐标（单元格中心点）
    /// </summary>
    Vector3 GridToWorld(GridCoord gridCoord);
    
    /// <summary>
    /// 计算两个网格坐标之间的距离
    /// </summary>
    float GetDistance(GridCoord a, GridCoord b);
    
    /// <summary>
    /// 获取指定坐标的邻居坐标列表（0 GC 版本）
    /// </summary>
    /// <param name="center">中心坐标</param>
    /// <param name="result">预分配的结果数组</param>
    /// <param name="count">实际结果数量</param>
    void GetNeighborsNonAlloc(GridCoord center, ref GridCoord[] result, out int count);
    
    /// <summary>
    /// 获取指定坐标的邻居坐标列表
    /// </summary>
    List<GridCoord> GetNeighbors(GridCoord center);
    
    /// <summary>
    /// 获取指定坐标半径范围内的所有坐标（0 GC 版本）
    /// </summary>
    /// <param name="center">中心坐标</param>
    /// <param name="radius">半径</param>
    /// <param name="result">预分配的结果数组</param>
    /// <param name="count">实际结果数量</param>
    void GetCellsInRadiusNonAlloc(GridCoord center, int radius, ref GridCoord[] result, out int count);
    
    /// <summary>
    /// 获取指定坐标半径范围内的所有坐标
    /// </summary>
    List<GridCoord> GetCellsInRadius(GridCoord center, int radius);
    
    /// <summary>
    /// 获取指定坐标直线方向上的所有坐标（0 GC 版本）
    /// </summary>
    void GetLineNonAlloc(GridCoord start, Vector2 direction, int length, ref GridCoord[] result, out int count);
    
    /// <summary>
    /// 获取指定坐标直线方向上的所有坐标
    /// </summary>
    List<GridCoord> GetLine(GridCoord start, Vector2 direction, int length);
    
    /// <summary>
    /// 获取指定大小的建筑占用的坐标列表（0 GC 版本）
    /// </summary>
    void GetOccupiedCellsNonAlloc(GridCoord center, int size, ref GridCoord[] result, out int count);
    
    /// <summary>
    /// 获取指定大小的建筑占用的坐标列表
    /// </summary>
    List<GridCoord> GetOccupiedCells(GridCoord center, int size);
    
    /// <summary>
    /// 获取最大邻居数量（用于预分配数组）
    /// </summary>
    int MaxNeighborCount { get; }
}