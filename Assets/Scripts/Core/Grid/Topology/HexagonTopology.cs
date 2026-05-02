using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 六边形网格拓扑实现（尖顶朝上模型）
/// 使用轴向坐标系 (q, r)，其中 q = U, r = V
/// </summary>
public class HexagonTopology : IGridTopology
{
    private readonly Vector3 _origin;
    private readonly float _cellSize;
    
    // 六边形的宽度和高度（用于坐标转换）
    private readonly float _hexWidth;
    private readonly float _hexHeight;
    
    public HexagonTopology(Vector3 origin, float cellSize)
    {
        _origin = origin;
        _cellSize = cellSize;
        
        // 尖顶六边形的尺寸计算
        _hexWidth = _cellSize * 2f; // 六边形宽度
        _hexHeight = _cellSize * Mathf.Sqrt(3f); // 六边形高度
    }
    
    /// <summary>
    /// 将世界坐标转换为六边形网格坐标（使用立方体坐标系进行取整）
    /// </summary>
    public GridCoord WorldToGrid(Vector3 worldPosition)
    {
        var offset = worldPosition - _origin;
        
        // 转换为立方体坐标系 (x, y, z)
        // 对于尖顶六边形：x = q, y = r, z = -q-r
        // 但我们需要从世界坐标反推
        
        // 计算在六边形网格中的局部坐标
        float q = offset.x / (_hexWidth * 0.75f);
        float r = (offset.z / _hexHeight) - (offset.x / (_hexWidth * 1.5f));
        
        // 六边形取整算法（Hex-Rounding）
        return HexRound(q, r);
    }
    
    /// <summary>
    /// 六边形取整算法
    /// </summary>
    private GridCoord HexRound(float q, float r)
    {
        // 转换为立方体坐标系
        float x = q;
        float y = r;
        float z = -q - r;
        
        // 四舍五入到最近的整数
        int rx = Mathf.RoundToInt(x);
        int ry = Mathf.RoundToInt(y);
        int rz = Mathf.RoundToInt(z);
        
        // 计算四舍五入后的误差
        float xDiff = Mathf.Abs(rx - x);
        float yDiff = Mathf.Abs(ry - y);
        float zDiff = Mathf.Abs(rz - z);
        
        // 修正最大误差的坐标
        if (xDiff > yDiff && xDiff > zDiff)
        {
            rx = -ry - rz;
        }
        else if (yDiff > zDiff)
        {
            ry = -rx - rz;
        }
        else
        {
            rz = -rx - ry;
        }
        
        // 返回轴向坐标 (q, r)
        return new GridCoord(rx, ry);
    }
    
    /// <summary>
    /// 将六边形网格坐标转换为世界坐标（单元格中心点）
    /// </summary>
    public Vector3 GridToWorld(GridCoord gridCoord)
    {
        // 尖顶六边形的中心点计算
        float x = _origin.x + gridCoord.U * _hexWidth * 0.75f;
        float z = _origin.z + gridCoord.V * _hexHeight + (gridCoord.U % 2 == 0 ? 0 : _hexHeight * 0.5f);
        
        return new Vector3(x, _origin.y, z);
    }
    
    /// <summary>
    /// 计算六边形距离（三轴曼哈顿距离的一半）
    /// </summary>
    public float GetDistance(GridCoord a, GridCoord b)
    {
        // 在轴向坐标系中，距离 = (|dq| + |dr| + |dq+dr|) / 2
        int dq = a.U - b.U;
        int dr = a.V - b.V;
        return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(dq + dr)) / 2f;
    }
    
    /// <summary>
    /// 获取6方向邻居坐标（尖顶六边形）
    /// </summary>
    public List<GridCoord> GetNeighbors(GridCoord center)
    {
        // 尖顶六边形的6个方向偏移量
        return new List<GridCoord>
        {
            new GridCoord(center.U + 1, center.V),     // 东
            new GridCoord(center.U - 1, center.V),     // 西
            new GridCoord(center.U, center.V + 1),     // 北
            new GridCoord(center.U, center.V - 1),     // 南
            new GridCoord(center.U + 1, center.V + 1), // 东北
            new GridCoord(center.U - 1, center.V - 1)  // 西南
        };
    }
    
    /// <summary>
    /// 获取半径范围内的所有坐标（六边形）
    /// </summary>
    public List<GridCoord> GetCellsInRadius(GridCoord center, int radius)
    {
        var cells = new List<GridCoord>();
        
        // 六边形范围遍历
        for (int u = -radius; u <= radius; u++)
        {
            for (int v = Mathf.Max(-radius, -u - radius); v <= Mathf.Min(radius, -u + radius); v++)
            {
                var pos = new GridCoord(u + center.U, v + center.V);
                if (GetDistance(center, pos) <= radius)
                {
                    cells.Add(pos);
                }
            }
        }
        
        return cells;
    }
    
    /// <summary>
    /// 获取直线方向上的坐标（六边形射线步进）
    /// </summary>
    public List<GridCoord> GetLine(GridCoord start, Vector2 direction, int length)
    {
        var line = new List<GridCoord>();
        
        // 六边形直线步进算法
        for (int i = 0; i < length; i++)
        {
            // 计算世界坐标
            float worldX = start.U * _hexWidth * 0.75f + direction.x * i * _hexWidth * 0.5f;
            float worldZ = start.V * _hexHeight + direction.y * i * _hexHeight * 0.5f;
            
            // 转换回网格坐标
            Vector3 worldPos = new Vector3(worldX, 0, worldZ);
            GridCoord coord = WorldToGrid(worldPos);
            
            line.Add(coord);
        }
        
        return line;
    }
    
    /// <summary>
    /// 获取建筑占用的坐标列表（六边形）
    /// </summary>
    public List<GridCoord> GetOccupiedCells(GridCoord center, int size)
    {
        var cells = new List<GridCoord>();
        
        switch (size)
        {
            case 1:
                cells.Add(center);
                break;
            case 3:
                // 三格形态（L型）
                cells.Add(center);
                cells.Add(new GridCoord(center.U + 1, center.V));
                cells.Add(new GridCoord(center.U, center.V + 1));
                break;
            case 7:
                // 七格形态（蜂窝状）
                cells.Add(center);
                cells.Add(new GridCoord(center.U + 1, center.V));
                cells.Add(new GridCoord(center.U - 1, center.V));
                cells.Add(new GridCoord(center.U, center.V + 1));
                cells.Add(new GridCoord(center.U, center.V - 1));
                cells.Add(new GridCoord(center.U + 1, center.V + 1));
                cells.Add(new GridCoord(center.U - 1, center.V - 1));
                break;
            default:
                // 默认单格
                cells.Add(center);
                break;
        }
        
        return cells;
    }
}