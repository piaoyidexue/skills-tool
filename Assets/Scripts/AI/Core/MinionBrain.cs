using UnityEngine;

/// <summary>
///     杂兵轻量状态机 —— 塔防场景下替代 NodeCanvas 行为树的 0 GC 方案。
///     用于杂兵级别 AI（小兵、召唤物），Boss/精英仍保留完整行为树。
/// </summary>
public enum MinionState
{
    /// <summary>沿路径点移动</summary>
    MoveToWaypoint,
    /// <summary>攻击目标</summary>
    Attack,
    /// <summary>死亡</summary>
    Dead
}

/// <summary>
///     杂兵 AI —— 极小开销的纯 C# 状态机。
///     依赖空间哈希网格进行目标检测（非 OverlapSphere）。
///     典型用法：挂载在杂兵预制体上，替代 AIController。
/// </summary>
public class MinionBrain : MonoBehaviour, ISpatialEntity
{
    [Header("状态机配置")]
    [SerializeField] private MinionState _startState = MinionState.MoveToWaypoint;
    [SerializeField] private int _teamId = 2;
    [SerializeField] private float _attackRange = 2f;
    [SerializeField] private float _detectionRange = 8f;
    [SerializeField] private float _attackCooldown = 1.5f;
    [SerializeField] private float _damage = 10f;

    [Header("移动")]
    [SerializeField] private Transform[] _waypoints;
    [SerializeField] private float _moveSpeed = 3f;

    // ---- 状态机运行时 ----
    private MinionState _currentState;
    private int _currentWaypoint;
    private ISpatialEntity _target;
    private float _lastAttackTime = -999f;
    private int _spatialEntityId = -1;

    // ---- ISpatialEntity ----
    int ISpatialEntity.EntityId => _spatialEntityId;
    Vector3 ISpatialEntity.Position => transform.position;
    int ISpatialEntity.TeamId => _teamId;
    bool ISpatialEntity.IsActive => isActiveAndEnabled;

    private void Start()
    {
        _currentState = _startState;
        _spatialEntityId = SpatialHashGrid.Instance?.Register(this) ?? -1;
    }

    private void Update()
    {
        // 更新空间网格中的位置
        if (_spatialEntityId >= 0)
            SpatialHashGrid.Instance?.UpdatePosition(_spatialEntityId, transform.position);

        switch (_currentState)
        {
            case MinionState.MoveToWaypoint:
                MoveTowardWaypoint();
                TryDetectEnemy();
                break;

            case MinionState.Attack:
                TryAttack();
                break;
        }
    }

    private void MoveTowardWaypoint()
    {
        if (_waypoints == null || _waypoints.Length == 0) return;

        var target = _waypoints[_currentWaypoint];
        var direction = (target.position - transform.position).normalized;
        transform.position += direction * _moveSpeed * Time.deltaTime;
        transform.forward = direction;

        if (Vector3.Distance(transform.position, target.position) < 0.5f)
        {
            _currentWaypoint = (_currentWaypoint + 1) % _waypoints.Length;
        }
    }

    private void TryDetectEnemy()
    {
        if (_currentState != MinionState.MoveToWaypoint) return;

        var grid = SpatialHashGrid.Instance;
        if (grid == null) return;

        // 使用空间哈希查询最近敌人（替代 OverlapSphere）
        _target = grid.QueryNearest(transform.position, _detectionRange,
            teamFilter: 1, excludeEntityId: _spatialEntityId);

        if (_target != null)
        {
            _currentState = MinionState.Attack;
        }
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

        // 朝向目标
        var direction = (targetPos - transform.position).normalized;
        transform.forward = direction;

        if (dist <= _attackRange && Time.time - _lastAttackTime >= _attackCooldown)
        {
            _lastAttackTime = Time.time;
            // 施加伤害
            var targetTransform = (_target as MonoBehaviour)?.transform;
            if (targetTransform != null)
            {
                DamagePipeline.CalculateAndApply(_damage, targetTransform, transform);
            }
        }
        else if (dist > _attackRange)
        {
            // 追击
            transform.position += direction * _moveSpeed * Time.deltaTime;
        }
    }

    public void TakeDamage(float amount)
    {
        _currentState = MinionState.Dead;
        SpatialHashGrid.Instance?.Unregister(_spatialEntityId);
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
