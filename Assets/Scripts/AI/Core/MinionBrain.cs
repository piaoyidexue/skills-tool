using SkillAI;
using UnityEngine;

public enum MinionState
{
    Idle,
    MoveToWaypoint,
    Attack,
    Dead
}

/// <summary>
///     Minion AI -- lightweight pure C# FSM.
///     Replaces NodeCanvas BT for tower-defense mobs (0 GC, 0 serialization overhead).
///     Uses SpatialHashGrid for O(1) target detection.
///     Optionally registers with AISensorJobSystem for Burst-parallel FOV/distance checks.
///
///     AI Tier Strategy:
///     - Minion: MinionBrain FSM (this class)
///     - Elite/Boss/Tower: AIController + NodeCanvas BT
/// </summary>
public class MinionBrain : MonoBehaviour, ISpatialEntity
{
    [Header("Tier")]
    [SerializeField] private AITier _aiTier = AITier.Minion;

    [Header("FSM Config")]
    [SerializeField] private MinionState _startState = MinionState.MoveToWaypoint;
    [SerializeField] private int _teamId = 2;
    [SerializeField] private float _attackRange = 2f;
    [SerializeField] private float _detectionRange = 8f;
    [SerializeField] private float _fieldOfView = 360f;
    [SerializeField] private float _attackCooldown = 1.5f;
    [SerializeField] private float _damage = 10f;

    [Header("Job System")]
    [Tooltip("Use Burst Job for parallel detection (reduces main-thread overhead)")]
    [SerializeField] private bool _useJobSystem = true;

    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 3f;

    // ---- Navigation Data (Injected via SpawnContext) ----
    private AITargetMode _targetMode = AITargetMode.Waypoint;
    private Transform[] _pathNodes;
    private int _currentWaypointIndex;
    private Vector3 _patrolCenter;
    private float _patrolRadius;
    private bool _isPatrolling = true;

    // ---- Squad ID ----
    private int _squadId;
    public int SquadId { get => _squadId; set => _squadId = value; }

    // ---- FSM Runtime ----
    private MinionState _currentState;
    private ISpatialEntity _target;
    private float _lastAttackTime = -999f;
    private int _spatialEntityId = -1;

    // ---- Job Result Cache ----
    private bool _pendingJobResult;
    private int _nearestEnemyEntityId = -1;
    private float _nearestEnemyDistance = float.MaxValue;
    private int _enemyCount;

    // ---- ISpatialEntity ----
    int ISpatialEntity.EntityId => _spatialEntityId;
    Vector3 ISpatialEntity.Position => transform.position;
    int ISpatialEntity.TeamId => _teamId;
    bool ISpatialEntity.IsActive => isActiveAndEnabled;
    int ISpatialEntity.EntityType => (int)_aiTier;

    public int TeamId { get => _teamId; set => _teamId = value; }

    /// <summary>
    ///     注入导航数据。
    ///     由生成器在怪物出生时调用。
    /// </summary>
    public void InjectNavigation(MonsterSpawnContext context)
    {
        _targetMode = context.TargetMode;
        _squadId = context.SquadID;

        switch (context.TargetMode)
        {
            case AITargetMode.Waypoint:
                _pathNodes = context.PathNodes;
                _currentWaypointIndex = 0;
                break;
            case AITargetMode.FreeRoam:
                _patrolCenter = context.PatrolCenter;
                _patrolRadius = context.PatrolRadius;
                _isPatrolling = true;
                break;
        }
    }

    private void Start()
    {
        _currentState = _startState;
        var grid = SpatialHashGrid.Instance;
        if (grid != null)
        {
            var meta = new AIEntityNativeData
            {
                Position = transform.position,
                Forward = transform.forward,
                TeamId = _teamId,
                EntityType = (int)_aiTier,
                DetectionRange = _detectionRange,
                AttackRange = _attackRange,
                FieldOfView = _fieldOfView
            };
            _spatialEntityId = grid.RegisterWithMeta(this, meta);
        }
    }

    private void Update()
    {
        if (_spatialEntityId >= 0)
            SpatialHashGrid.Instance?.UpdatePosition(_spatialEntityId, transform.position);

        if (_useJobSystem && Time.frameCount % 4 == 0)
            RegisterJobQuery();

        switch (_currentState)
        {
            case MinionState.Idle:
                break;
            case MinionState.MoveToWaypoint:
                MoveTowardTarget();
                TryDetectEnemy();
                break;
            case MinionState.Attack:
                TryAttack();
                break;
        }
    }

    private void RegisterJobQuery()
    {
        var jobSys = AISensorJobSystem.Instance;
        if (jobSys == null) return;
        var selfId = _spatialEntityId;
        jobSys.RegisterQuery(new SensorQueryRequest
        {
            Position = transform.position,
            Forward = transform.forward,
            DetectionRange = _detectionRange,
            AttackRange = _attackRange,
            FieldOfView = _fieldOfView,
            TeamId = _teamId,
            ExcludeEntityId = selfId,
            Callback = OnJobResult
        });
        _pendingJobResult = true;
    }

    private void OnJobResult(SensorQueryResult result)
    {
        _nearestEnemyEntityId = result.NearestEnemyEntityId;
        _nearestEnemyDistance = result.NearestEnemyDistance;
        _enemyCount = result.EnemyCount;
        _pendingJobResult = false;
    }

    private void MoveTowardTarget()
    {
        Vector3 targetPos;
        switch (_targetMode)
        {
            case AITargetMode.Waypoint:
                if (_pathNodes == null || _pathNodes.Length == 0) return;
                targetPos = _pathNodes[_currentWaypointIndex].position;
                break;
            case AITargetMode.FreeRoam:
                if (!_isPatrolling) return;
                targetPos = _patrolCenter + new Vector3(
                    UnityEngine.Random.Range(-_patrolRadius, _patrolRadius),
                    0,
                    UnityEngine.Random.Range(-_patrolRadius, _patrolRadius)
                );
                _isPatrolling = false;
                break;
            default:
                return;
        }

        var dir = (targetPos - transform.position).normalized;
        transform.position += dir * _moveSpeed * Time.deltaTime;
        transform.forward = dir;

        if (Vector3.Distance(transform.position, targetPos) < 0.5f)
        {
            if (_targetMode == AITargetMode.Waypoint)
                _currentWaypointIndex = (_currentWaypointIndex + 1) % _pathNodes.Length;
            else if (_targetMode == AITargetMode.FreeRoam)
                _isPatrolling = true;
        }
    }

    private void TryDetectEnemy()
    {
        if (_currentState != MinionState.MoveToWaypoint) return;

        if (_useJobSystem && !_pendingJobResult && _nearestEnemyEntityId >= 0)
        {
            _target = LookupEntity(_nearestEnemyEntityId);
            if (_target != null) { _currentState = MinionState.Attack; return; }
        }

        var grid = SpatialHashGrid.Instance;
        if (grid == null) return;
        _target = grid.QueryNearest(transform.position, _detectionRange,
            teamFilter: _teamId == 1 ? 2 : 1,
            excludeEntityId: _spatialEntityId);
        if (_target != null) _currentState = MinionState.Attack;
    }

    private void TryAttack()
    {
        if (_target == null || !_target.IsActive)
        {
            _target = null;
            _currentState = MinionState.MoveToWaypoint;
            return;
        }

        var targetPos = _target.Position;
        var dist = Vector3.Distance(transform.position, targetPos);
        var dir = (targetPos - transform.position).normalized;
        transform.forward = dir;

        if (dist <= _attackRange && Time.time - _lastAttackTime >= _attackCooldown)
        {
            _lastAttackTime = Time.time;
            var tf = (_target as MonoBehaviour)?.transform;
            if (tf != null) DamagePipeline.CalculateAndApply(_damage, tf, transform);
        }
        else if (dist > _attackRange)
        {
            transform.position += dir * _moveSpeed * Time.deltaTime;
        }
    }

    private ISpatialEntity LookupEntity(int entityId)
    {
        return SpatialHashGrid.Instance?.GetEntity(entityId);
    }

    public void TakeDamage(float amount)
    {
        _currentState = MinionState.Dead;
        SpatialHashGrid.Instance?.Unregister(_spatialEntityId);
        _spatialEntityId = -1;
        Destroy(gameObject, 0.5f);
    }

    private void OnDestroy()
    {
        if (_spatialEntityId >= 0)
        {
            SpatialHashGrid.Instance?.Unregister(_spatialEntityId);
            _spatialEntityId = -1;
        }
    }
}