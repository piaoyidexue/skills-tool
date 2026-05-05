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
    
    // 预分配的邻居数组（用于 0 GC）
    private readonly GridCoord[] _neighborBuffer = new GridCoord[8];
    
    public int MaxNeighborCount => 8;
    
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
    /// 获取8方向邻居坐标（0 GC 版本）
    /// </summary>
    public void GetNeighborsNonAlloc(GridCoord center, ref GridCoord[] result, out int count)
    {
        count = 0;
        
        // 8方向偏移
        _neighborBuffer[0] = new GridCoord(center.U - 1, center.V - 1);
        _neighborBuffer[1] = new GridCoord(center.U, center.V - 1);
        _neighborBuffer[2] = new GridCoord(center.U + 1, center.V - 1);
        _neighborBuffer[3] = new GridCoord(center.U - 1, center.V);
        _neighborBuffer[4] = new GridCoord(center.U + 1, center.V);
        _neighborBuffer[5] = new GridCoord(center.U - 1, center.V + 1);
        _neighborBuffer[6] = new GridCoord(center.U, center.V + 1);
        _neighborBuffer[7] = new GridCoord(center.U + 1, center.V + 1);
        
        for (int i = 0; i < 8 && count < result.Length; i++)
        {
            result[count++] = _neighborBuffer[i];
        }
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
    /// 获取半径范围内的所有坐标（0 GC 版本）
    /// </summary>
    public void GetCellsInRadiusNonAlloc(GridCoord center, int radius, ref GridCoord[] result, out int count)
    {
        count = 0;
        
        for (int u = center.U - radius; u <= center.U + radius; u++)
        {
            for (int v = center.V - radius; v <= center.V + radius; v++)
            {
                GridCoord pos = new GridCoord(u, v);
                if (GetDistance(center, pos) <= radius && count < result.Length)
                {
                    result[count++] = pos;
                }
            }
        }
    }
    
    /// <summary>
    /// 获取半径范围内的所有坐标
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
    /// 获取直线方向上的坐标（0 GC 版本）
    /// </summary>
    public void GetLineNonAlloc(GridCoord start, Vector2 direction, int length, ref GridCoord[] result, out int count)
    {
        count = 0;
        
        for (int i = 0; i < length && count < result.Length; i++)
        {
            float u = start.U + direction.x * i;
            float v = start.V + direction.y * i;
            
            int roundedU = Mathf.RoundToInt(u);
            int roundedV = Mathf.RoundToInt(v);
            
            result[count++] = new GridCoord(roundedU, roundedV);
        }
    }
    
    /// <summary>
    /// 获取直线方向上的坐标
    /// </summary>
    public List<GridCoord> GetLine(GridCoord start, Vector2 direction, int length)
    {
        var line = new List<GridCoord>();
        
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
    /// 获取建筑占用的坐标列表（0 GC 版本）
    /// </summary>
    public void GetOccupiedCellsNonAlloc(GridCoord center, int size, ref GridCoord[] result, out int count)
    {
        count = 0;
        
        switch (size)
        {
            case 1:
                if (count < result.Length) result[count++] = center;
                break;
            case 2:
                if (count < result.Length) result[count++] = center;
                if (count < result.Length) result[count++] = new GridCoord(center.U + 1, center.V);
                break;
            case 4:
                if (count < result.Length) result[count++] = center;
                if (count < result.Length) result[count++] = new GridCoord(center.U + 1, center.V);
                if (count < result.Length) result[count++] = new GridCoord(center.U, center.V + 1);
                if (count < result.Length) result[count++] = new GridCoord(center.U + 1, center.V + 1);
                break;
            default:
                if (count < result.Length) result[count++] = center;
                break;
        }
    }
    
    /// <summary>
    /// 获取建筑占用的坐标列表
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
                cells.Add(center);
                cells.Add(new GridCoord(center.U + 1, center.V));
                break;
            case 4:
                cells.Add(center);
                cells.Add(new GridCoord(center.U + 1, center.V));
                cells.Add(new GridCoord(center.U, center.V + 1));
                cells.Add(new GridCoord(center.U + 1, center.V + 1));
                break;
            default:
                cells.Add(center);
                break;
        }
        
        return cells;
    }
}