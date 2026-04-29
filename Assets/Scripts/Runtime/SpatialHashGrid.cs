using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public interface ISpatialEntity
{
    int EntityId { get; }
    Vector3 Position { get; }
    int TeamId { get; }
    bool IsActive { get; }
    int EntityType { get; }
}

public struct AIEntityNativeData
{
    public float3 Position;
    public float3 Forward;
    public int EntityId;
    public int TeamId;
    public int EntityType;
    public float DetectionRange;
    public float AttackRange;
    public float FieldOfView;
    public int ResultIndex;
}

[DefaultExecutionOrder(-200)]
public class SpatialHashGrid : MonoBehaviour
{
    [SerializeField] private float _cellSize = 10f;
    [SerializeField] private float _dirtyThreshold = 0.5f;

    private const int InitialCellCapacity = 16;

    private readonly Dictionary<int, List<ISpatialEntity>> _cells = new(256);
    private readonly Dictionary<int, int> _entityCellMap = new(512);
    private readonly Dictionary<int, Vector3> _entityLastPositions = new(512);
    private readonly Dictionary<int, AIEntityNativeData> _entityMeta = new(512);
    private readonly HashSet<int> _dirtyEntities = new(256);

    /// <summary>entityId -> ISpatialEntity lookup (for job result resolution)</summary>
    private readonly Dictionary<int, ISpatialEntity> _entityLookup = new(512);

    private bool _nativeArrayDirty = true;
    private NativeArray<AIEntityNativeData> _cachedNativeEntities;
    private readonly List<ISpatialEntity> _queryResults = new(64);
    private int _nextEntityId = 1;

    public static SpatialHashGrid Instance { get; private set; }
    public float CellSize => _cellSize;
    public int EntityCount => _entityCellMap.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void LateUpdate() => FlushDirtyEntities();

    private void OnDestroy()
    {
        if (_cachedNativeEntities.IsCreated) _cachedNativeEntities.Dispose();
        Clear();
        if (Instance == this) Instance = null;
    }

    // ===== Register / Unregister =====

    public int Register(ISpatialEntity entity)
    {
        if (entity == null) return -1;
        var entityId = _nextEntityId++;
        AddToCell(entityId, entity.Position, entity);
        _entityLastPositions[entityId] = entity.Position;
        _entityLookup[entityId] = entity;
        _nativeArrayDirty = true;
        return entityId;
    }

    public int RegisterWithMeta(ISpatialEntity entity, AIEntityNativeData meta)
    {
        var entityId = Register(entity);
        if (entityId >= 0)
        {
            meta.EntityId = entityId;
            meta.ResultIndex = entityId;
            _entityMeta[entityId] = meta;
        }
        return entityId;
    }

    public void UpdateMeta(int entityId, AIEntityNativeData meta)
    {
        if (_entityMeta.ContainsKey(entityId))
        {
            meta.EntityId = entityId;
            meta.ResultIndex = entityId;
            _entityMeta[entityId] = meta;
            _nativeArrayDirty = true;
        }
    }

    public void Unregister(int entityId)
    {
        RemoveFromCell(entityId);
        _entityLastPositions.Remove(entityId);
        _dirtyEntities.Remove(entityId);
        _entityMeta.Remove(entityId);
        _entityLookup.Remove(entityId);
        _nativeArrayDirty = true;
    }

    /// <summary>Get ISpatialEntity by entityId (O(1) lookup).</summary>
    public ISpatialEntity GetEntity(int entityId)
    {
        _entityLookup.TryGetValue(entityId, out var entity);
        return entity;
    }

    /// <summary>Get Transform of entity by entityId.</summary>
    public Transform GetEntityTransform(int entityId)
    {
        var entity = GetEntity(entityId);
        return (entity as MonoBehaviour)?.transform;
    }

    // ===== Position Update =====

    public void UpdatePosition(int entityId, Vector3 newPosition)
    {
        if (!_entityCellMap.TryGetValue(entityId, out var currentCellKey)) return;

        if (_entityLastPositions.TryGetValue(entityId, out var lastPos))
        {
            var delta = newPosition - lastPos;
            if (delta.sqrMagnitude < _dirtyThreshold * _dirtyThreshold) return;
        }

        _entityLastPositions[entityId] = newPosition;
        _dirtyEntities.Add(entityId);

        // Sync position to job metadata
        if (_entityMeta.TryGetValue(entityId, out var meta))
        {
            meta.Position = newPosition;
            _entityMeta[entityId] = meta;
        }

        var newCellKey = ComputeCellKey(newPosition);
        if (newCellKey != currentCellKey)
        {
            if (_cells.TryGetValue(currentCellKey, out var oldCell))
                oldCell.RemoveAll(e => e.EntityId == entityId);
            AddToCell(entityId, newPosition, null);
        }

        _nativeArrayDirty = true;
    }

    public void FlushDirtyEntities()
    {
        if (_dirtyEntities.Count == 0) return;
        foreach (var entityId in _dirtyEntities)
        {
            if (!_entityCellMap.TryGetValue(entityId, out var currentCellKey)) continue;
            if (!_entityLastPositions.TryGetValue(entityId, out var pos)) continue;
            var newCellKey = ComputeCellKey(pos);
            if (newCellKey == currentCellKey) continue;
            if (_cells.TryGetValue(currentCellKey, out var oldCell))
                oldCell.RemoveAll(e => e.EntityId == entityId);
            AddToCell(entityId, pos, null);
        }
        _dirtyEntities.Clear();
    }

    // ===== Queries =====

    public void QueryRange(Vector3 center, float radius, List<ISpatialEntity> results,
        int teamFilter = -1, int excludeEntityId = -1)
    {
        results.Clear();
        var r2 = radius * radius;
        var minX = Mathf.FloorToInt((center.x - radius) / _cellSize);
        var maxX = Mathf.FloorToInt((center.x + radius) / _cellSize);
        var minZ = Mathf.FloorToInt((center.z - radius) / _cellSize);
        var maxZ = Mathf.FloorToInt((center.z + radius) / _cellSize);

        for (var x = minX; x <= maxX; x++)
        for (var z = minZ; z <= maxZ; z++)
        {
            if (!_cells.TryGetValue(HashCell(x, z), out var cell)) continue;
            foreach (var e in cell)
            {
                if (!e.IsActive) continue;
                if (excludeEntityId >= 0 && e.EntityId == excludeEntityId) continue;
                if (teamFilter >= 0 && e.TeamId != teamFilter) continue;
                var dx = e.Position.x - center.x;
                var dz = e.Position.z - center.z;
                if (dx * dx + dz * dz <= r2) results.Add(e);
            }
        }
    }

    public ISpatialEntity QueryNearest(Vector3 center, float radius,
        int teamFilter = -1, int excludeEntityId = -1)
    {
        _queryResults.Clear();
        QueryRange(center, radius, _queryResults, teamFilter, excludeEntityId);
        ISpatialEntity nearest = null;
        var best = float.MaxValue;
        foreach (var e in _queryResults)
        {
            var dx = e.Position.x - center.x;
            var dz = e.Position.z - center.z;
            var d2 = dx * dx + dz * dz;
            if (d2 < best) { best = d2; nearest = e; }
        }
        return nearest;
    }

    public int QueryCount(Vector3 center, float radius, int teamFilter = -1)
    {
        _queryResults.Clear();
        QueryRange(center, radius, _queryResults, teamFilter);
        return _queryResults.Count;
    }

    public bool HasAny(Vector3 center, float radius, int teamFilter = -1)
    {
        var r2 = radius * radius;
        var minX = Mathf.FloorToInt((center.x - radius) / _cellSize);
        var maxX = Mathf.FloorToInt((center.x + radius) / _cellSize);
        var minZ = Mathf.FloorToInt((center.z - radius) / _cellSize);
        var maxZ = Mathf.FloorToInt((center.z + radius) / _cellSize);
        for (var x = minX; x <= maxX; x++)
        for (var z = minZ; z <= maxZ; z++)
        {
            if (!_cells.TryGetValue(HashCell(x, z), out var cell)) continue;
            foreach (var e in cell)
            {
                if (!e.IsActive) continue;
                if (teamFilter >= 0 && e.TeamId != teamFilter) continue;
                var dx = e.Position.x - center.x;
                var dz = e.Position.z - center.z;
                if (dx * dx + dz * dz <= r2) return true;
            }
        }
        return false;
    }

    // ===== NativeArray Export =====

    public NativeArray<AIEntityNativeData> GetNativeEntityArray(Allocator allocator)
    {
        if (_nativeArrayDirty || !_cachedNativeEntities.IsCreated)
        {
            if (_cachedNativeEntities.IsCreated) _cachedNativeEntities.Dispose();
            var count = _entityMeta.Count;
            _cachedNativeEntities = new NativeArray<AIEntityNativeData>(count, Allocator.Persistent);
            var index = 0;
            foreach (var kvp in _entityMeta)
            {
                if (index >= count) break;
                var data = kvp.Value;
                data.ResultIndex = index;
                _cachedNativeEntities[index] = data;
                index++;
            }
            _nativeArrayDirty = false;
        }
        var copy = new NativeArray<AIEntityNativeData>(_cachedNativeEntities.Length, allocator);
        NativeArray<AIEntityNativeData>.Copy(_cachedNativeEntities, copy);
        return copy;
    }

    public int GetActiveEntityCount()
    {
        var c = 0;
        foreach (var kvp in _entityMeta)
            if (_entityCellMap.ContainsKey(kvp.Key)) c++;
        return c;
    }

    public (int entities, int cells, int dirty) GetStats()
        => (EntityCount, _cells.Count, _dirtyEntities.Count);

    public void Clear()
    {
        foreach (var c in _cells.Values) c.Clear();
        _cells.Clear();
        _entityCellMap.Clear();
        _entityLastPositions.Clear();
        _dirtyEntities.Clear();
        _entityMeta.Clear();
        _entityLookup.Clear();
        _nextEntityId = 1;
        _nativeArrayDirty = true;
    }

    private int ComputeCellKey(Vector3 p)
        => HashCell(Mathf.FloorToInt(p.x / _cellSize), Mathf.FloorToInt(p.z / _cellSize));

    private static int HashCell(int x, int z)
        => unchecked((x * 73856093) ^ (z * 19349663));

    private void AddToCell(int entityId, Vector3 position, ISpatialEntity entity)
    {
        var key = ComputeCellKey(position);
        if (!_cells.TryGetValue(key, out var cell))
        {
            cell = new List<ISpatialEntity>(InitialCellCapacity);
            _cells[key] = cell;
        }
        if (entity != null) cell.Add(entity);
        _entityCellMap[entityId] = key;
    }

    private void RemoveFromCell(int entityId)
    {
        if (!_entityCellMap.TryGetValue(entityId, out var key)) return;
        if (_cells.TryGetValue(key, out var cell))
            cell.RemoveAll(e => e.EntityId == entityId);
        _entityCellMap.Remove(entityId);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        foreach (var kvp in _cells)
        {
            var key = kvp.Key;
            var x = (key / 73856093) * _cellSize + _cellSize * 0.5f;
            var z = (key % 73856093) / 19349663.0f;
            Gizmos.DrawWireCube(new Vector3(x, 0, z), new Vector3(_cellSize, 0.1f, _cellSize));
        }
    }
#endif
}
