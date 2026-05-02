using System;
using UnityEngine;

/// <summary>
/// 逻辑网格数学基石 - 提供世界坐标与网格坐标的双向转换
/// </summary>
public static class GridMath
{
    /// <summary>
    /// 网格原点（世界坐标）
    /// </summary>
    public static Vector3 Origin = Vector3.zero;
    
    /// <summary>
    /// 单元格大小（世界单位）
    /// </summary>
    public static float CellSize = 2f;
    
    /// <summary>
    /// 将世界坐标转换为网格坐标（整数）
    /// </summary>
    /// <param name="worldPosition">世界坐标</param>
    /// <returns>网格坐标（Vector2Int）</returns>
    public static Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        // 计算相对于原点的偏移
        var offset = worldPosition - Origin;
        
        // 转换为网格坐标（向下取整，确保坐标在正确单元格内）
        int x = Mathf.FloorToInt(offset.x / CellSize);
        int z = Mathf.FloorToInt(offset.z / CellSize);
        
        return new Vector2Int(x, z);
    }
    
    /// <summary>
    /// 将网格坐标转换为世界坐标（单元格中心点）
    /// </summary>
    /// <param name="gridPosition">网格坐标</param>
    /// <returns>世界坐标（Vector3）</returns>
    public static Vector3 GridToWorld(Vector2Int gridPosition)
    {
        // 计算单元格中心的世界坐标
        float x = Origin.x + (gridPosition.x + 0.5f) * CellSize;
        float z = Origin.z + (gridPosition.y + 0.5f) * CellSize;
        
        return new Vector3(x, Origin.y, z);
    }
    
    /// <summary>
    /// 将世界坐标转换为网格坐标（四舍五入到最近单元格）
    /// </summary>
    /// <param name="worldPosition">世界坐标</param>
    /// <returns>网格坐标（Vector2Int）</returns>
    public static Vector2Int WorldToGridRounded(Vector3 worldPosition)
    {
        var offset = worldPosition - Origin;
        int x = Mathf.RoundToInt(offset.x / CellSize);
        int z = Mathf.RoundToInt(offset.z / CellSize);
        
        return new Vector2Int(x, z);
    }
    
    /// <summary>
    /// 获取网格坐标的相邻单元格（8方向）
    /// </summary>
    /// <param name="gridPosition">中心网格坐标</param>
    /// <returns>相邻单元格坐标数组</returns>
    public static Vector2Int[] GetAdjacentCells(Vector2Int gridPosition)
    {
        return new Vector2Int[]
        {
            new Vector2Int(gridPosition.x - 1, gridPosition.y - 1),
            new Vector2Int(gridPosition.x, gridPosition.y - 1),
            new Vector2Int(gridPosition.x + 1, gridPosition.y - 1),
            new Vector2Int(gridPosition.x - 1, gridPosition.y),
            new Vector2Int(gridPosition.x + 1, gridPosition.y),
            new Vector2Int(gridPosition.x - 1, gridPosition.y + 1),
            new Vector2Int(gridPosition.x, gridPosition.y + 1),
            new Vector2Int(gridPosition.x + 1, gridPosition.y + 1)
        };
    }
    
    /// <summary>
    /// 获取网格坐标的曼哈顿距离
    /// </summary>
    /// <param name="a">第一个网格坐标</param>
    /// <param name="b">第二个网格坐标</param>
    /// <returns>曼哈顿距离</returns>
    public static int ManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}

/// <summary>
/// 网格配置数据结构
/// </summary>
[Serializable]
public struct GridConfig
{
    /// <summary>
    /// 网格原点（世界坐标）
    /// </summary>
    public Vector3 Origin;
    
    /// <summary>
    /// 单元格大小（世界单位）
    /// </summary>
    public float CellSize;
    
    /// <summary>
    /// 网格尺寸（宽度和高度）
    /// </summary>
    public Vector2Int Size;
    
    /// <summary>
    /// 是否启用边界检查
    /// </summary>
    public bool EnableBoundsCheck;
    
    /// <summary>
    /// 初始化默认配置
    /// </summary>
    public void Initialize()
    {
        Origin = Vector3.zero;
        CellSize = 2f;
        Size = new Vector2Int(100, 100);
        EnableBoundsCheck = true;
    }
}