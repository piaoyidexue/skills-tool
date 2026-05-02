using UnityEngine;

/// <summary>
/// 地形效果应用组件 - 用于怪物和玩家检测脚下地形并应用相应效果
/// 性能优化：O(1)数组查询，无需物理引擎开销
/// </summary>
public class TerrainEffectApplication : MonoBehaviour
{
    [Header("地形效果配置")]
    public float tickInterval = 0.2f; // 每0.2秒检测一次
    
    [Header("效果配置")]
    public float burnDamagePerSecond = 5f;
    public float slowMultiplier = 0.5f;
    
    private LogicalGridManager _gridManager;
    private float _nextTickTime;
    
    private void Awake()
    {
        _gridManager = LogicalGridManager.Instance;
        if (_gridManager == null)
        {
            Debug.LogWarning($"[TerrainEffectApplication] {name}: LogicalGridManager not found");
        }
    }
    
    private void Update()
    {
        if (_gridManager == null || Time.time < _nextTickTime)
            return;
        
        _nextTickTime = Time.time + tickInterval;
        
        // 将世界坐标转换为网格坐标
        var gridCoord = _gridManager.Topology.WorldToGrid(transform.position);
        
        // 查询该网格的地形类型
        var cellData = _gridManager.GetCellData(gridCoord);
        if (!cellData.HasValue)
            return;
        
        // 应用对应的效果
        ApplyTerrainEffect(cellData.Value.TerrainType, cellData.Value.TerrainDuration);
    }
    
    private void ApplyTerrainEffect(TerrainEffect terrainType, float terrainDuration)
    {
        // 获取GEHost组件（假设怪物有GEHost组件）
        var geHost = GetComponent<GEHost>();
        if (geHost == null)
            return;
        
        switch (terrainType)
        {
            case TerrainEffect.Burnt:
                // 应用灼烧效果（每秒造成伤害）
                var burnStatus = new StatusRuntime
                {
                    Type = StatusType.Burn,
                    Value = burnDamagePerSecond,
                    Duration = terrainDuration,
                    Instigator = transform
                };
                geHost.ApplyStatus(burnStatus);
                break;
                
            case TerrainEffect.Ice:
                // 应用减速效果
                var slowStatus = new StatusRuntime
                {
                    Type = StatusType.Slow,
                    Value = slowMultiplier,
                    Duration = terrainDuration,
                    Instigator = transform
                };
                geHost.ApplyStatus(slowStatus);
                break;
                
            case TerrainEffect.Rock:
                // 应用金属效果（暴击提升等）
                // Note: Metal status type may need to be added to StatusType enum
                // For now, we'll use a custom approach or skip
                break;
                
            case TerrainEffect.Swamp:
                // 应用泥沼效果（移动速度降低）
                // Note: Swamp status type may need to be added to StatusType enum
                // For now, we'll use a custom approach or skip
                break;
                
            default:
                // 移除所有地形相关效果
                // GEHost doesn't have RemoveGameplayEffectsByTag method
                // We'll clear all statuses instead
                ClearAllStatuses(geHost);
                break;
        }
    }
    
    private void ClearAllStatuses(GEHost geHost)
    {
        // Get all active statuses and consume them
        var statuses = geHost.GetActiveStatuses();
        foreach (var status in statuses)
        {
            geHost.ConsumeStatus(status.Type, out _);
        }
    }
}
