using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
///     AI sensor job system -- uses Burst + Job System for parallel FOV/distance detection.
///     Collects all active AI sensor queries each frame, dispatches to worker threads,
///     eliminating main-thread serial O(N^2) overhead.
/// </summary>
[DefaultExecutionOrder(100)]
public class AISensorJobSystem : MonoBehaviour
{
    [SerializeField] private bool _useJobs = true;
    [SerializeField] private int _batchSize = 64;

    private NativeArray<AIEntityNativeData> _entities;
    private NativeArray<SensorQueryInput> _queries;
    private NativeArray<SensorQueryResult> _results;
    private NativeArray<SensorDistanceResult> _distanceResults;

    private JobHandle _activeJobHandle;
    private bool _jobScheduled;

    private readonly System.Collections.Generic.List<SensorQueryRequest> _pendingQueries = new(128);

    public static AISensorJobSystem Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        CompleteJobs();
        DisposeArrays();
        if (Instance == this) Instance = null;
    }

    private void LateUpdate()
    {
        if (!_useJobs) return;

        CompleteJobs();
        CollectQueries();

        if (_pendingQueries.Count == 0) return;

        var grid = SpatialHashGrid.Instance;
        if (grid == null) return;

        _entities = grid.GetNativeEntityArray(Allocator.TempJob);

        _queries = new NativeArray<SensorQueryInput>(_pendingQueries.Count, Allocator.TempJob);
        _results = new NativeArray<SensorQueryResult>(_pendingQueries.Count, Allocator.TempJob);
        _distanceResults = new NativeArray<SensorDistanceResult>(_pendingQueries.Count, Allocator.TempJob);

        for (var i = 0; i < _pendingQueries.Count; i++)
        {
            var req = _pendingQueries[i];
            _queries[i] = new SensorQueryInput
            {
                Position = req.Position,
                Forward = req.Forward,
                DetectionRange = req.DetectionRange,
                AttackRange = req.AttackRange,
                FieldOfView = req.FieldOfView,
                TeamId = req.TeamId,
                ExcludeEntityId = req.ExcludeEntityId,
                QueryIndex = i
            };
        }

        var distanceJob = new FOVDistanceJob
        {
            Entities = _entities,
            Queries = _queries,
            Results = _results,
            DistanceResults = _distanceResults
        };

        _activeJobHandle = distanceJob.Schedule(_pendingQueries.Count, _batchSize);
        JobHandle.ScheduleBatchedJobs();
        _jobScheduled = true;
    }

    public void RegisterQuery(SensorQueryRequest request)
    {
        _pendingQueries.Add(request);
    }

    private void CompleteJobs()
    {
        if (!_jobScheduled) return;

        _activeJobHandle.Complete();

        for (var i = 0; i < _results.Length; i++)
        {
            var result = _results[i];
            var req = _pendingQueries[i];
            req.Callback?.Invoke(result);
        }

        DisposeArrays();
        _pendingQueries.Clear();
        _jobScheduled = false;
    }

    private void DisposeArrays()
    {
        if (_entities.IsCreated) _entities.Dispose();
        if (_queries.IsCreated) _queries.Dispose();
        if (_results.IsCreated) _results.Dispose();
        if (_distanceResults.IsCreated) _distanceResults.Dispose();
    }

    private void CollectQueries() { }

    public void ManualTick() => LateUpdate();
}

// ===== Job Data Structs =====

public struct SensorQueryInput
{
    public float3 Position;
    public float3 Forward;
    public float DetectionRange;
    public float AttackRange;
    public float FieldOfView;
    public int TeamId;
    public int ExcludeEntityId;
    public int QueryIndex;
}

public struct SensorQueryResult
{
    public int NearestEnemyIndex;
    public int NearestEnemyEntityId;
    public float NearestEnemyDistance;
    public int EnemyCount;
    public int AllyCount;
    public bool HasTargetInAttackRange;
}

public struct SensorDistanceResult
{
    public int EntityIndex;
    public float Distance;
}

public struct SensorQueryRequest
{
    public Vector3 Position;
    public Vector3 Forward;
    public float DetectionRange;
    public float AttackRange;
    public float FieldOfView;
    public int TeamId;
    public int ExcludeEntityId;
    public Action<SensorQueryResult> Callback;
}

// ===== Burst Jobs =====

[BurstCompile(OptimizeFor = OptimizeFor.Performance)]
public struct FOVDistanceJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<AIEntityNativeData> Entities;
    [ReadOnly] public NativeArray<SensorQueryInput> Queries;

    [WriteOnly] public NativeArray<SensorQueryResult> Results;
    [WriteOnly] public NativeArray<SensorDistanceResult> DistanceResults;

    public void Execute(int index)
    {
        var query = Queries[index];
        var result = new SensorQueryResult
        {
            NearestEnemyIndex = -1,
            NearestEnemyEntityId = -1,
            NearestEnemyDistance = float.MaxValue,
            EnemyCount = 0,
            AllyCount = 0,
            HasTargetInAttackRange = false
        };

        var detectionRangeSq = query.DetectionRange * query.DetectionRange;
        var attackRangeSq = query.AttackRange * query.AttackRange;
        var halfFov = query.FieldOfView * 0.5f;
        var fullFov = query.FieldOfView >= 359f;

        for (var i = 0; i < Entities.Length; i++)
        {
            var entity = Entities[i];
            if (entity.EntityId == query.ExcludeEntityId) continue;

            var dx = entity.Position.x - query.Position.x;
            var dz = entity.Position.z - query.Position.z;
            var distSq = dx * dx + dz * dz;

            if (distSq > detectionRangeSq) continue;

            if (!fullFov)
            {
                var dirX = entity.Position.x - query.Position.x;
                var dirZ = entity.Position.z - query.Position.z;
                var invLen = math.rsqrt(dirX * dirX + dirZ * dirZ + 0.0001f);
                dirX *= invLen;
                dirZ *= invLen;
                var dot = dirX * query.Forward.x + dirZ * query.Forward.z;
                var cosHalfFov = math.cos(math.radians(halfFov));
                if (dot < cosHalfFov) continue;
            }

            if (entity.TeamId == query.TeamId)
            {
                result.AllyCount++;
            }
            else
            {
                result.EnemyCount++;
                if (distSq < result.NearestEnemyDistance)
                {
                    result.NearestEnemyDistance = distSq;
                    result.NearestEnemyIndex = i;
                    result.NearestEnemyEntityId = entity.EntityId;
                }
                if (distSq <= attackRangeSq)
                    result.HasTargetInAttackRange = true;
            }
        }

        if (result.NearestEnemyDistance < float.MaxValue)
            result.NearestEnemyDistance = math.sqrt(result.NearestEnemyDistance);

        Results[index] = result;
    }
}

/// <summary>
///     Burst FOV + distance sorted job.
///     Sorts candidate targets by distance using stackalloc Span (no unsafe required).
/// </summary>
[BurstCompile(OptimizeFor = OptimizeFor.Performance)]
public struct FOVDistanceSortedJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<AIEntityNativeData> Entities;
    [ReadOnly] public NativeArray<SensorQueryInput> Queries;

    [WriteOnly] public NativeArray<SensorQueryResult> Results;

    private const int MaxCandidates = 64;

    public void Execute(int index)
    {
        var query = Queries[index];
        var result = new SensorQueryResult
        {
            NearestEnemyIndex = -1,
            NearestEnemyEntityId = -1,
            NearestEnemyDistance = float.MaxValue,
            EnemyCount = 0,
            AllyCount = 0,
            HasTargetInAttackRange = false
        };

        var detectionRangeSq = query.DetectionRange * query.DetectionRange;
        var attackRangeSq = query.AttackRange * query.AttackRange;
        var halfFov = query.FieldOfView * 0.5f;
        var fullFov = query.FieldOfView >= 359f;

        // Span<T> + stackalloc -- no unsafe required
        Span<int> candidateIndices = stackalloc int[MaxCandidates];
        Span<float> candidateDistSq = stackalloc float[MaxCandidates];
        var candidateCount = 0;

        for (var i = 0; i < Entities.Length; i++)
        {
            var entity = Entities[i];
            if (entity.EntityId == query.ExcludeEntityId) continue;

            var dx = entity.Position.x - query.Position.x;
            var dz = entity.Position.z - query.Position.z;
            var distSq = dx * dx + dz * dz;
            if (distSq > detectionRangeSq) continue;

            if (!fullFov)
            {
                var dirX = entity.Position.x - query.Position.x;
                var dirZ = entity.Position.z - query.Position.z;
                var invLen = math.rsqrt(dirX * dirX + dirZ * dirZ + 0.0001f);
                dirX *= invLen;
                dirZ *= invLen;
                var dot = dirX * query.Forward.x + dirZ * query.Forward.z;
                if (dot < math.cos(math.radians(halfFov))) continue;
            }

            if (entity.TeamId == query.TeamId)
            {
                result.AllyCount++;
            }
            else
            {
                result.EnemyCount++;
                if (distSq <= attackRangeSq)
                    result.HasTargetInAttackRange = true;

                if (candidateCount < MaxCandidates)
                {
                    candidateIndices[candidateCount] = i;
                    candidateDistSq[candidateCount] = distSq;
                    candidateCount++;
                }
            }
        }

        // Bubble sort (tiny N, faster than generic sort)
        for (var i = 0; i < candidateCount - 1; i++)
        {
            for (var j = i + 1; j < candidateCount; j++)
            {
                if (candidateDistSq[i] > candidateDistSq[j])
                {
                    var tmpIdx = candidateIndices[i];
                    var tmpDist = candidateDistSq[i];
                    candidateIndices[i] = candidateIndices[j];
                    candidateDistSq[i] = candidateDistSq[j];
                    candidateIndices[j] = tmpIdx;
                    candidateDistSq[j] = tmpDist;
                }
            }
        }

        if (candidateCount > 0)
        {
            result.NearestEnemyIndex = candidateIndices[0];
            result.NearestEnemyEntityId = Entities[candidateIndices[0]].EntityId;
            result.NearestEnemyDistance = math.sqrt(candidateDistSq[0]);
        }

        Results[index] = result;
    }
}
