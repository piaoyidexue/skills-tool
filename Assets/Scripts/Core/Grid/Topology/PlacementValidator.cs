using System;
using UnityEngine;

/// <summary>
/// 建造管理与校验管线 - 提供给UI和玩家输入使用的查询接口
/// 支持正方形和六边形网格拓扑
/// </summary>
public class PlacementValidator : MonoBehaviour
{
    private static PlacementValidator _instance;
    public static PlacementValidator Instance => _instance;
    
    private LogicalGridManager _gridManager;
    
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
        
        _gridManager = LogicalGridManager.Instance;
        
        if (_gridManager == null)
        {
            Debug.LogError("[PlacementValidator] LogicalGridManager not found!");
        }
    }
    
    /// <summary>
    /// 查询是否可以在指定网格坐标建造防御塔
    /// </summary>
    /// <param name="gridCoord">网格坐标</param>
    /// <returns>是否可以建造</returns>
    public bool CanBuildTowerAt(GridCoord gridCoord)
    {
        if (_gridManager == null) return false;
        
        return _gridManager.CanBuildTowerAt(gridCoord);
    }
    
    /// <summary>
    /// 查询是否可以在指定世界坐标建造防御塔
    /// </summary>
    /// <param name="worldPosition">世界坐标</param>
    /// <returns>是否可以建造</returns>
    public bool CanBuildTowerAtWorld(Vector3 worldPosition)
    {
        if (_gridManager == null) return false;
        
        var gridCoord = _gridManager.Topology.WorldToGrid(worldPosition);
        return _gridManager.CanBuildTowerAt(gridCoord);
    }
    
    /// <summary>
    /// 获取建造预览状态（用于UI渲染）
    /// </summary>
    /// <param name="gridCoord">网格坐标</param>
    /// <returns>预览状态枚举</returns>
    public BuildPreviewStatus GetBuildPreviewStatus(GridCoord gridCoord)
    {
        if (_gridManager == null) return BuildPreviewStatus.Error;
        
        if (!LogicalGridManager.Instance.IsValidGridPosition(gridCoord))
        {
            return BuildPreviewStatus.OutOfBounds;
        }
        
        var cellData = _gridManager.GetCellData(gridCoord);
        if (!cellData.HasValue)
        {
            return BuildPreviewStatus.Error;
        }
        
        if (!cellData.Value.IsBuildable())
        {
            return BuildPreviewStatus.InvalidTerrain;
        }
        
        if (cellData.Value.OccupiedBy != -1)
        {
            return BuildPreviewStatus.AlreadyOccupied;
        }
        
        if (_gridManager.CanBuildTowerAt(gridCoord))
        {
            return BuildPreviewStatus.WouldBlockPath;
        }
        
        return BuildPreviewStatus.Valid;
    }
    
    /// <summary>
    /// 获取建造预览状态（世界坐标版本）
    /// </summary>
    /// <param name="worldPosition">世界坐标</param>
    /// <returns>预览状态枚举</returns>
    public BuildPreviewStatus GetBuildPreviewStatusWorld(Vector3 worldPosition)
    {
        var gridCoord = _gridManager.Topology.WorldToGrid(worldPosition);
        return GetBuildPreviewStatus(gridCoord);
    }
    
    /// <summary>
    /// 执行建造操作
    /// </summary>
    /// <param name="gridCoord">网格坐标</param>
    /// <param name="entityId">实体ID</param>
    /// <returns>是否成功建造</returns>
    public bool BuildTowerAt(GridCoord gridCoord, int entityId)
    {
        if (_gridManager == null) return false;
        
        return _gridManager.BuildTowerAt(gridCoord, entityId);
    }
    
    /// <summary>
    /// 执行建造操作（世界坐标版本）
    /// </summary>
    /// <param name="worldPosition">世界坐标</param>
    /// <param name="entityId">实体ID</param>
    /// <returns>是否成功建造</returns>
    public bool BuildTowerAtWorld(Vector3 worldPosition, int entityId)
    {
        var gridCoord = _gridManager.Topology.WorldToGrid(worldPosition);
        return BuildTowerAt(gridCoord, entityId);
    }
}

/// <summary>
/// 建造预览状态枚举
/// </summary>
public enum BuildPreviewStatus
{
    /// <summary>有效位置</summary>
    Valid,
    
    /// <summary>超出边界</summary>
    OutOfBounds,
    
    /// <summary>无效地形</summary>
    InvalidTerrain,
    
    /// <summary>已被占用</summary>
    AlreadyOccupied,
    
    /// <summary>会阻塞路径</summary>
    WouldBlockPath,
    
    /// <summary>错误状态</summary>
    Error
}