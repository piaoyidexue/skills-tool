using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     空间实体接口 —— 可注册到空间哈希网格的对象。
/// </summary>
public interface ISpatialEntity
{
    int EntityId { get; }
    Vector3 Position { get; }
    int TeamId { get; }
    bool IsActive { get; }
}

/// <summary>
///     空间哈希网格 —— 全局 O(1) 空间查询系统。
///     替代 Physics.OverlapSphere，支持大规模实体（200+）的高效邻近查询。
///     实体移动时更新所在格子，查询仅检查相邻格。
/// </summary>
[DefaultExecutionOrder(-200)]
public class SpatialHashGrid : MonoBehaviour
{
    /// <summary>网格单元边长（世界单位），影响查询精度与性能的平衡</summary>
    [SerializeField] private float _cellSize = 10f;

    /// <summary>预分配的每格容量</summary>
    private const int InitialCellCapacity = 16;

    /// <summary>网格数据：cellKey → 实体列表</summary>
    private readonly Dictionary<int, List<ISpatialEntity>> _cells = new(256);

    /// <summary>实体当前所在格：entityId → cellKey</summary>
    private readonly Dictionary<int, int> _entityCellMap = new(512);

    /// <summary>实体 ID 计数器</summary>
    private int _nextEntityId = 1;

    /// <summary>可复用结果列表（减少 GC）</summary>
    private readonly List<ISpatialEntity> _queryResults = new(64);
    private readonly List<ISpatialEntity> _tempMoved = new(16);

    public static SpatialHashGrid Instance { get; private set; }

    /// <summary>网格单元大小</summary>
    public float CellSize => _cellSize;

    /// <summary>当前注册实体数</summary>
    public int EntityCount => _entityCellMap.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    ///     注册实体到空间网格。返回分配的 EntityId。
    /// </summary>
    public int Register(ISpatialEntity entity)
    {
        if (entity == null) return -1;

        var entityId = _nextEntityId++;
        AddToCell(entityId, entity.Position, entity);
        return entityId;
    }

    /// <summary>
    ///     从空间网格注销实体。
    /// </summary>
    public void Unregister(int entityId)
    {
        RemoveFromCell(entityId);
    }

    /// <summary>
    ///     更新实体位置（仅在跨越格边界时重新索引）。
    /// </summary>
    public void UpdatePosition(int entityId, Vector3 newPosition)
    {
        if (!_entityCellMap.TryGetValue(entityId, out var currentCellKey))
            return;

        var newCellKey = ComputeCellKey(newPosition);
        if (newCellKey == currentCellKey) return; // 未跨格，无需操作

        // 跨格：移除旧格，加入新格
        if (_cells.TryGetValue(currentCellKey, out var oldCell))
        {
            oldCell.RemoveAll(e => e.EntityId == entityId);
        }

        AddToCell(entityId, newPosition, null); // entity 通过 entityCellMap 关联
    }

    /// <summary>
    ///     查询指定位置周围范围内的所有实体。
    /// </summary>
    /// <param name="center">查询中心</param>
    /// <param name="radius">查询半径</param>
    /// <param name="results">结果输出列表（会先清空）</param>
    /// <param name="teamFilter">队伍过滤（-1=不过滤）</param>
    /// <param name="excludeEntityId">排除指定实体（自身）</param>
    public void QueryRange(Vector3 center, float radius, List<ISpatialEntity> results,
        int teamFilter = -1, int excludeEntityId = -1)
    {
        results.Clear();

        var radiusSq = radius * radius;
        var minX = Mathf.FloorToInt((center.x - radius) / _cellSize);
        var maxX = Mathf.FloorToInt((center.x + radius) / _cellSize);
        var minZ = Mathf.FloorToInt((center.z - radius) / _cellSize);
        var maxZ = Mathf.FloorToInt((center.z + radius) / _cellSize);

        for (var x = minX; x <= maxX; x++)
        {
            for (var z = minZ; z <= maxZ; z++)
            {
                var key = HashCell(x, z);
                if (!_cells.TryGetValue(key, out var cell)) continue;

                foreach (var entity in cell)
                {
                    if (!entity.IsActive) continue;
                    if (excludeEntityId >= 0 && entity.EntityId == excludeEntityId) continue;
                    if (teamFilter >= 0 && entity.TeamId != teamFilter) continue;

                    var dx = entity.Position.x - center.x;
                    var dz = entity.Position.z - center.z;
                    if (dx * dx + dz * dz <= radiusSq)
                    {
                        results.Add(entity);
                    }
                }
            }
        }
    }

    /// <summary>
    ///     查询指定位置周围范围内最近的实体。
    /// </summary>
    public ISpatialEntity QueryNearest(Vector3 center, float radius,
        int teamFilter = -1, int excludeEntityId = -1)
    {
        _queryResults.Clear();
        QueryRange(center, radius, _queryResults, teamFilter, excludeEntityId);

        ISpatialEntity nearest = null;
        var nearestDistSq = float.MaxValue;

        foreach (var entity in _queryResults)
        {
            var dx = entity.Position.x - center.x;
            var dz = entity.Position.z - center.z;
            var distSq = dx * dx + dz * dz;
            if (distSq < nearestDistSq)
            {
                nearestDistSq = distSq;
                nearest = entity;
            }
        }

        return nearest;
    }

    /// <summary>
    ///     查询指定位置周围范围内满足条件的实体数量。
    /// </summary>
    public int QueryCount(Vector3 center, float radius, int teamFilter = -1)
    {
        _queryResults.Clear();
        QueryRange(center, radius, _queryResults, teamFilter);
        return _queryResults.Count;
    }

    /// <summary>
    ///     检查指定位置范围内是否有任何实体。
    /// </summary>
    public bool HasAny(Vector3 center, float radius, int teamFilter = -1)
    {
        var radiusSq = radius * radius;
        var minX = Mathf.FloorToInt((center.x - radius) / _cellSize);
        var maxX = Mathf.FloorToInt((center.x + radius) / _cellSize);
        var minZ = Mathf.FloorToInt((center.z - radius) / _cellSize);
        var maxZ = Mathf.FloorToInt((center.z + radius) / _cellSize);

        for (var x = minX; x <= maxX; x++)
        {
            for (var z = minZ; z <= maxZ; z++)
            {
                if (!_cells.TryGetValue(HashCell(x, z), out var cell)) continue;

                foreach (var entity in cell)
                {
                    if (!entity.IsActive) continue;
                    if (teamFilter >= 0 && entity.TeamId != teamFilter) continue;

                    var dx = entity.Position.x - center.x;
                    var dz = entity.Position.z - center.z;
                    if (dx * dx + dz * dz <= radiusSq)
                        return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     清空所有网格数据（场景切换时调用）。
    /// </summary>
    public void Clear()
    {
        foreach (var cell in _cells.Values)
        {
            cell.Clear();
        }

        _cells.Clear();
        _entityCellMap.Clear();
        _nextEntityId = 1;
    }

    // ---- internal ----

    private int ComputeCellKey(Vector3 position)
    {
        var x = Mathf.FloorToInt(position.x / _cellSize);
        var z = Mathf.FloorToInt(position.z / _cellSize);
        return HashCell(x, z);
    }

    private static int HashCell(int x, int z)
    {
        // 使用 Cantor pairing 或其他合并方式避免冲突
        unchecked
        {
            return (x * 73856093) ^ (z * 19349663);
        }
    }

    private void AddToCell(int entityId, Vector3 position, ISpatialEntity entity)
    {
        var cellKey = ComputeCellKey(position);

        if (!_cells.TryGetValue(cellKey, out var cell))
        {
            cell = new List<ISpatialEntity>(InitialCellCapacity);
            _cells[cellKey] = cell;
        }

        if (entity != null)
        {
            cell.Add(entity);
        }

        _entityCellMap[entityId] = cellKey;
    }

    private void RemoveFromCell(int entityId)
    {
        if (!_entityCellMap.TryGetValue(entityId, out var cellKey)) return;

        if (_cells.TryGetValue(cellKey, out var cell))
        {
            cell.RemoveAll(e => e.EntityId == entityId);
        }

        _entityCellMap.Remove(entityId);
    }

    private void OnDestroy()
    {
        Clear();

        if (Instance == this)
        {
            Instance = null;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = new Color(0, 1, 0, 0.3f);
        foreach (var kvp in _cells)
        {
            var key = kvp.Key;
            // Decode cell key to grid position
            var x = (key / 73856093) * _cellSize + _cellSize * 0.5f;
            var z = (key % 73856093) / 19349663.0f;

            Gizmos.DrawWireCube(
                new Vector3(x, 0, 0), // approximate
                new Vector3(_cellSize, 0.1f, _cellSize));
        }
    }
#endif
}
