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
    
    // 预分配的邻居数组（用于 0 GC）
    private readonly GridCoord[] _neighborBuffer = new GridCoord[6];
    
    public int MaxNeighborCount => 6;
    
    public HexagonTopology(Vector3 origin, float cellSize)
    {
        _origin = origin;
        _cellSize = cellSize;
        
        // 尖顶六边形的尺寸计算
        _hexWidth = _cellSize * 2f;
        _hexHeight = _cellSize * Mathf.Sqrt(3f);
    }
    
    /// <summary>
    /// 将世界坐标转换为六边形网格坐标
    /// </summary>
    public GridCoord WorldToGrid(Vector3 worldPosition)
    {
        var offset = worldPosition - _origin;
        
        float q = offset.x / (_hexWidth * 0.75f);
        float r = (offset.z / _hexHeight) - (offset.x / (_hexWidth * 1.5f));
        
        return HexRound(q, r);
    }
    
    /// <summary>
    /// 六边形取整算法
    /// </summary>
    private GridCoord HexRound(float q, float r)
    {
        float x = q;
        float y = r;
        float z = -q - r;
        
        int rx = Mathf.RoundToInt(x);
        int ry = Mathf.RoundToInt(y);
        int rz = Mathf.RoundToInt(z);
        
        float xDiff = Mathf.Abs(rx - x);
        float yDiff = Mathf.Abs(ry - y);
        float zDiff = Mathf.Abs(rz - z);
        
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
        
        return new GridCoord(rx, ry);
    }
    
    /// <summary>
    /// 将六边形网格坐标转换为世界坐标
    /// </summary>
    public Vector3 GridToWorld(GridCoord gridCoord)
    {
        float x = _origin.x + gridCoord.U * _hexWidth * 0.75f;
        float z = _origin.z + gridCoord.V * _hexHeight + (gridCoord.U % 2 == 0 ? 0 : _hexHeight * 0.5f);
        
        return new Vector3(x, _origin.y, z);
    }
    
    /// <summary>
    /// 计算六边形距离
    /// </summary>
    public float GetDistance(GridCoord a, GridCoord b)
    {
        int dq = a.U - b.U;
        int dr = a.V - b.V;
        return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(dq + dr)) / 2f;
    }
    
    /// <summary>
    /// 获取6方向邻居坐标（0 GC 版本）
    /// </summary>
    public void GetNeighborsNonAlloc(GridCoord center, ref GridCoord[] result, out int count)
    {
        count = 0;
        
        // 尖顶六边形的6个方向偏移量
        _neighborBuffer[0] = new GridCoord(center.U + 1, center.V);
        _neighborBuffer[1] = new GridCoord(center.U - 1, center.V);
        _neighborBuffer[2] = new GridCoord(center.U, center.V + 1);
        _neighborBuffer[3] = new GridCoord(center.U, center.V - 1);
        _neighborBuffer[4] = new GridCoord(center.U + 1, center.V + 1);
        _neighborBuffer[5] = new GridCoord(center.U - 1, center.V - 1);
        
        for (int i = 0; i < 6 && count < result.Length; i++)
        {
            result[count++] = _neighborBuffer[i];
        }
    }
    
    /// <summary>
    /// 获取6方向邻居坐标
    /// </summary>
    public List<GridCoord> GetNeighbors(GridCoord center)
    {
        return new List<GridCoord>
        {
            new GridCoord(center.U + 1, center.V),
            new GridCoord(center.U - 1, center.V),
            new GridCoord(center.U, center.V + 1),
            new GridCoord(center.U, center.V - 1),
            new GridCoord(center.U + 1, center.V + 1),
            new GridCoord(center.U - 1, center.V - 1)
        };
    }
    
    /// <summary>
    /// 获取半径范围内的所有坐标（0 GC 版本）
    /// </summary>
    public void GetCellsInRadiusNonAlloc(GridCoord center, int radius, ref GridCoord[] result, out int count)
    {
        count = 0;
        
        for (int u = -radius; u <= radius; u++)
        {
            for (int v = Mathf.Max(-radius, -u - radius); v <= Mathf.Min(radius, -u + radius); v++)
            {
                GridCoord pos = new GridCoord(u + center.U, v + center.V);
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
    /// 获取直线方向上的坐标（0 GC 版本）
    /// </summary>
    public void GetLineNonAlloc(GridCoord start, Vector2 direction, int length, ref GridCoord[] result, out int count)
    {
        count = 0;
        
        for (int i = 0; i < length && count < result.Length; i++)
        {
            float worldX = start.U * _hexWidth * 0.75f + direction.x * i * _hexWidth * 0.5f;
            float worldZ = start.V * _hexHeight + direction.y * i * _hexHeight * 0.5f;
            
            Vector3 worldPos = new Vector3(worldX, 0, worldZ);
            GridCoord coord = WorldToGrid(worldPos);
            
            result[count++] = coord;
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
            float worldX = start.U * _hexWidth * 0.75f + direction.x * i * _hexWidth * 0.5f;
            float worldZ = start.V * _hexHeight + direction.y * i * _hexHeight * 0.5f;
            
            Vector3 worldPos = new Vector3(worldX, 0, worldZ);
            GridCoord coord = WorldToGrid(worldPos);
            
            line.Add(coord);
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
            case 3:
                if (count < result.Length) result[count++] = center;
                if (count < result.Length) result[count++] = new GridCoord(center.U + 1, center.V);
                if (count < result.Length) result[count++] = new GridCoord(center.U, center.V + 1);
                break;
            case 7:
                if (count < result.Length) result[count++] = center;
                if (count < result.Length) result[count++] = new GridCoord(center.U + 1, center.V);
                if (count < result.Length) result[count++] = new GridCoord(center.U - 1, center.V);
                if (count < result.Length) result[count++] = new GridCoord(center.U, center.V + 1);
                if (count < result.Length) result[count++] = new GridCoord(center.U, center.V - 1);
                if (count < result.Length) result[count++] = new GridCoord(center.U + 1, center.V + 1);
                if (count < result.Length) result[count++] = new GridCoord(center.U - 1, center.V - 1);
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
            case 3:
                cells.Add(center);
                cells.Add(new GridCoord(center.U + 1, center.V));
                cells.Add(new GridCoord(center.U, center.V + 1));
                break;
            case 7:
                cells.Add(center);
                cells.Add(new GridCoord(center.U + 1, center.V));
                cells.Add(new GridCoord(center.U - 1, center.V));
                cells.Add(new GridCoord(center.U, center.V + 1));
                cells.Add(new GridCoord(center.U, center.V - 1));
                cells.Add(new GridCoord(center.U + 1, center.V + 1));
                cells.Add(new GridCoord(center.U - 1, center.V - 1));
                break;
            default:
                cells.Add(center);
                break;
        }
        
        return cells;
    }
}