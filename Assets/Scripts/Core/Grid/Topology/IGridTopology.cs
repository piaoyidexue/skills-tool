using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 网格坐标结构体 - 统一的二维坐标表示，用于正方形和六边形网格
/// U 和 V 坐标在不同拓扑中有不同含义：
/// - 正方形：U = x, V = y
/// - 六边形（尖顶）：U = q (列), V = r (行)
/// </summary>
public struct GridCoord
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
}

/// <summary>
/// 网格拓扑接口 - 定义所有与网格形状相关的数学运算
/// 业务系统通过此接口与网格交互，完全屏蔽底层形状差异
/// </summary>
public interface IGridTopology
{
    /// <summary>
    /// 将世界坐标转换为网格坐标
    /// </summary>
    /// <param name="worldPosition">世界坐标</param>
    /// <returns>网格坐标</returns>
    GridCoord WorldToGrid(Vector3 worldPosition);
    
    /// <summary>
    /// 将网格坐标转换为世界坐标（单元格中心点）
    /// </summary>
    /// <param name="gridCoord">网格坐标</param>
    /// <returns>世界坐标</returns>
    Vector3 GridToWorld(GridCoord gridCoord);
    
    /// <summary>
    /// 计算两个网格坐标之间的距离
    /// </summary>
    /// <param name="a">第一个坐标</param>
    /// <param name="b">第二个坐标</param>
    /// <returns>距离（单位：单元格）</returns>
    float GetDistance(GridCoord a, GridCoord b);
    
    /// <summary>
    /// 获取指定坐标的邻居坐标列表
    /// </summary>
    /// <param name="center">中心坐标</param>
    /// <returns>邻居坐标列表</returns>
    List<GridCoord> GetNeighbors(GridCoord center);
    
    /// <summary>
    /// 获取指定坐标半径范围内的所有坐标
    /// </summary>
    /// <param name="center">中心坐标</param>
    /// <param name="radius">半径（曼哈顿距离）</param>
    /// <returns>范围内坐标列表</returns>
    List<GridCoord> GetCellsInRadius(GridCoord center, int radius);
    
    /// <summary>
    /// 获取指定坐标直线方向上的所有坐标（用于射线检测、共鸣等）
    /// </summary>
    /// <param name="start">起始坐标</param>
    /// <param name="direction">方向向量</param>
    /// <param name="length">长度</param>
    /// <returns>直线上的坐标列表</returns>
    List<GridCoord> GetLine(GridCoord start, Vector2 direction, int length);
    
    /// <summary>
    /// 获取指定大小的建筑占用的坐标列表
    /// </summary>
    /// <param name="center">中心坐标</param>
    /// <param name="size">建筑大小（1=单格，3=三格形态等）</param>
    /// <returns>占用的坐标列表</returns>
    List<GridCoord> GetOccupiedCells(GridCoord center, int size);
}