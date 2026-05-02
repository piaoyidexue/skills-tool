using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 正方形网格拓扑实现
/// U = x, V = y 坐标系
/// </summary>
public class SquareTopology : IGridTopology
{
    private readonly Vector3 _origin;
    private readonly float _cellSize;
    
    public SquareTopology(Vector3 origin, float cellSize)
    {
        _origin = origin;
        _cellSize = cellSize;
    }
    
    /// <summary>
    /// 将世界坐标转换为网格坐标（向下取整）
    /// </summary>
    public GridCoord WorldToGrid(Vector3 worldPosition)
    {
        var offset = worldPosition - _origin;
        int x = Mathf.FloorToInt(offset.x / _cellSize);
        int y = Mathf.FloorToInt(offset.z / _cellSize);
        return new GridCoord(x, y);
    }
    
    /// <summary>
    /// 将网格坐标转换为世界坐标（单元格中心点）
    /// </summary>
    public Vector3 GridToWorld(GridCoord gridCoord)
    {
        float x = _origin.x + (gridCoord.U + 0.5f) * _cellSize;
        float z = _origin.z + (gridCoord.V + 0.5f) * _cellSize;
        return new Vector3(x, _origin.y, z);
    }
    
    /// <summary>
    /// 计算曼哈顿距离（正方形网格）
    /// </summary>
    public float GetDistance(GridCoord a, GridCoord b)
    {
        return Mathf.Abs(a.U - b.U) + Mathf.Abs(a.V - b.V);
    }
    
    /// <summary>
    /// 获取8方向邻居坐标
    /// </summary>
    public List<GridCoord> GetNeighbors(GridCoord center)
    {
        return new List<GridCoord>
        {
            new GridCoord(center.U - 1, center.V - 1),
            new GridCoord(center.U, center.V - 1),
            new GridCoord(center.U + 1, center.V - 1),
            new GridCoord(center.U - 1, center.V),
            new GridCoord(center.U + 1, center.V),
            new GridCoord(center.U - 1, center.V + 1),
            new GridCoord(center.U, center.V + 1),
            new GridCoord(center.U + 1, center.V + 1)
        };
    }
    
    /// <summary>
    /// 获取半径范围内的所有坐标（正方形）
    /// </summary>
    public List<GridCoord> GetCellsInRadius(GridCoord center, int radius)
    {
        var cells = new List<GridCoord>();
        
        for (int u = center.U - radius; u <= center.U + radius; u++)
        {
            for (int v = center.V - radius; v <= center.V + radius; v++)
            {
                var pos = new GridCoord(u, v);
                if (GetDistance(center, pos) <= radius)
                {
                    cells.Add(pos);
                }
            }
        }
        
        return cells;
    }
    
    /// <summary>
    /// 获取直线方向上的坐标（正方形）
    /// </summary>
    public List<GridCoord> GetLine(GridCoord start, Vector2 direction, int length)
    {
        var line = new List<GridCoord>();
        
        // 简单的直线步进，根据方向四舍五入到最近的网格
        for (int i = 0; i < length; i++)
        {
            float u = start.U + direction.x * i;
            float v = start.V + direction.y * i;
            
            int roundedU = Mathf.RoundToInt(u);
            int roundedV = Mathf.RoundToInt(v);
            
            line.Add(new GridCoord(roundedU, roundedV));
        }
        
        return line;
    }
    
    /// <summary>
    /// 获取建筑占用的坐标列表（正方形）
    /// </summary>
    public List<GridCoord> GetOccupiedCells(GridCoord center, int size)
    {
        var cells = new List<GridCoord>();
        
        switch (size)
        {
            case 1:
                cells.Add(center);
                break;
            case 2:
                // 2x1 形态
                cells.Add(center);
                cells.Add(new GridCoord(center.U + 1, center.V));
                break;
            case 4:
                // 2x2 形态
                cells.Add(center);
                cells.Add(new GridCoord(center.U + 1, center.V));
                cells.Add(new GridCoord(center.U, center.V + 1));
                cells.Add(new GridCoord(center.U + 1, center.V + 1));
                break;
            default:
                // 默认单格
                cells.Add(center);
                break;
        }
        
        return cells;
    }
}