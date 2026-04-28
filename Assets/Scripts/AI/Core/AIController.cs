using NodeCanvas.BehaviourTrees;
using NodeCanvas.Framework;
using UnityEngine;

namespace SkillAI
{
    /// <summary>
    ///     AI 控制器 —— MonoBehaviour 组件，挂载到 AI 角色上驱动行为树运行。
    ///     继承 BehaviourTreeOwner，获得完整的 GraphOwner 功能（序列化、启动、暂停等）。
    ///     支持空间哈希网格查询（替代 Physics.OverlapSphere）。
    /// </summary>
    [AddComponentMenu("Skill System/AI Controller")]
    public class AIController : BehaviourTreeOwner, ISpatialEntity
    {
        [Header("AI 配置")]
        [Tooltip("AI 类型标签")]
        [SerializeField] private AIType aiType = AIType.Combat;
        public AIType AITypeTag => aiType;

        [Tooltip("探测器组件引用")]
        [SerializeField] private AISensor sensor;
        public AISensor Sensor => sensor;

        [Tooltip("更新间隔（秒），0 表示每帧更新")]
        [SerializeField] [Range(0f, 1f)] private float tickInterval;
        public float TickInterval => tickInterval;

        [Header("队伍配置")]
        [Tooltip("队伍 ID（用于空间网格过滤）")]
        [SerializeField] private int _teamId = 1;
        public int TeamId { get => _teamId; set => _teamId = value; }

        [Header("运行时状态")]
        [SerializeField] private AIStateType currentState = AIStateType.Idle;
        [SerializeField] private AIAlertLevel alertLevel = AIAlertLevel.None;

        // ---- ISpatialEntity ----
        private int _spatialEntityId = -1;
        int ISpatialEntity.EntityId => _spatialEntityId;
        Vector3 ISpatialEntity.Position => transform.position;
        int ISpatialEntity.TeamId => _teamId;
        bool ISpatialEntity.IsActive => isActiveAndEnabled;

        public AIStateType CurrentAIState
        {
            get => currentState;
            set
            {
                if (currentState != value)
                {
                    var old = currentState;
                    currentState = value;
                    OnAIStateChanged(old, value);
                }
            }
        }

        public AIAlertLevel AlertLevel
        {
            get => alertLevel;
            set
            {
                if (alertLevel != value)
                {
                    alertLevel = value;
                    OnAlertLevelChanged(value);
                }
            }
        }

        /// <summary>当前分配的行为树</summary>
        public AIGraph AITree => graph as AIGraph;

        /// <summary>AI 状态变更事件</summary>
        public event System.Action<AIStateType, AIStateType> OnStateChanged;

        /// <summary>警戒等级变更事件</summary>
        public event System.Action<AIAlertLevel> OnAlertChanged;

        protected void Awake()
        {
            base.Awake();
            if (sensor == null)
                sensor = GetComponent<AISensor>();
            if (sensor == null)
                sensor = GetComponentInChildren<AISensor>();

            updateInterval = tickInterval;
        }

        protected void Start()
        {
            base.Start();
            // 注册到空间哈希网格
            var grid = SpatialHashGrid.Instance;
            if (grid != null)
            {
                _spatialEntityId = grid.Register(this);
            }
        }

        protected void OnDestroy()
        {
            if (_spatialEntityId >= 0)
            {
                SpatialHashGrid.Instance?.Unregister(_spatialEntityId);
                _spatialEntityId = -1;
            }
        }

        protected void Update()
        {
            // 更新空间哈希网格中的位置
            if (_spatialEntityId >= 0)
            {
                SpatialHashGrid.Instance?.UpdatePosition(_spatialEntityId, transform.position);
            }
        }

        /// <summary>通过运行时黑板写入变量</summary>
        public void SetBBValue<T>(string key, T value)
        {
            var bb = blackboard;
            if (bb == null) return;
            var variable = bb.GetVariable<T>(key);
            if (variable != null)
                variable.value = value;
        }

        /// <summary>通过运行时黑板读取变量</summary>
        public T GetBBValue<T>(string key)
        {
            var bb = blackboard;
            if (bb == null) return default;
            var variable = bb.GetVariable<T>(key);
            return variable != null ? variable.value : default;
        }

        private void OnAIStateChanged(AIStateType oldState, AIStateType newState)
        {
            SetBBValue(AIBBKey.AIState, (int)newState);
            OnStateChanged?.Invoke(oldState, newState);
        }

        private void OnAlertLevelChanged(AIAlertLevel level)
        {
            SetBBValue(AIBBKey.AlertLevel, (int)level);
            OnAlertChanged?.Invoke(level);
        }

        protected void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            if (sensor != null)
                sensor.DrawGizmos();
        }

        public override string ToString()
        {
            return $"[AIController] {name} | State: {currentState} | Alert: {alertLevel} | Tree: {(AITree != null ? AITree.TreeName : "None")}";
        }
    }

    /// <summary>
    ///     AI 传感器基类 —— 负责环境感知（视野、听觉等）。
    ///     挂载在 AI 角色或子对象上，由 AIController 驱动。
    ///     支持空间哈希网格查询和传统 Physics 查询两种模式。
    /// </summary>
    public abstract class AISensor : MonoBehaviour
    {
        [Header("传感器配置")]
        [Tooltip("检测范围（视野半径）")]
        [SerializeField] protected float detectionRange = 15f;
        public float DetectionRange => detectionRange;

        [Tooltip("攻击范围")]
        [SerializeField] protected float attackRange = 3f;
        public float AttackRange => attackRange;

        [Tooltip("视野角度（度）")]
        [SerializeField] [Range(0f, 360f)] protected float fieldOfView = 120f;

        [Tooltip("检测层级掩码")]
        [SerializeField] protected LayerMask detectionMask = -1;

        [Tooltip("障碍物层级掩码")]
        [SerializeField] protected LayerMask obstacleMask;

        [Tooltip("是否优先使用空间哈希网格查询（高性能模式）")]
        [SerializeField] protected bool _useSpatialHash = true;

        [Header("运行时")]

        [SerializeField] protected Transform currentTarget;
        public Transform CurrentTarget => currentTarget;

        /// <summary>目标变更事件</summary>
        public event System.Action<Transform, Transform> OnTargetChanged;

        /// <summary>检测到新目标事件</summary>
        public event System.Action<Transform> OnTargetDetected;

        /// <summary>丢失目标事件</summary>
        public event System.Action<Transform> OnTargetLost;

        protected AIController controller;

        /// <summary>空间查询结果缓存</summary>
        private readonly System.Collections.Generic.List<ISpatialEntity> _spatialResults = new(32);

        protected virtual void Awake()
        {
            controller = GetComponentInParent<AIController>();
        }

        /// <summary>执行感知扫描（由行为树 Action 或 AIController Update 驱动）</summary>
        public virtual void Scan()
        {
            Transform newTarget;

            if (_useSpatialHash)
            {
                newTarget = DetectTargetSpatial();
            }
            else
            {
                newTarget = DetectTarget();
            }

            if (newTarget != currentTarget)
            {
                var old = currentTarget;
                currentTarget = newTarget;

                OnTargetChanged?.Invoke(old, newTarget);
                if (controller != null)
                {
                    controller.SetBBValue(AIBBKey.Target, currentTarget);
                    controller.SetBBValue(AIBBKey.HasTarget, currentTarget != null);
                    if (currentTarget != null)
                        controller.SetBBValue(AIBBKey.TargetPosition, currentTarget.position);
                }

                if (newTarget != null)
                    OnTargetDetected?.Invoke(newTarget);
                else
                    OnTargetLost?.Invoke(old);
            }
        }

        /// <summary>
        ///     使用空间哈希网格检测目标（O(1) 查询，推荐）。
        /// </summary>
        protected virtual Transform DetectTargetSpatial()
        {
            var grid = SpatialHashGrid.Instance;
            if (grid == null) return DetectTarget(); // fallback

            _spatialResults.Clear();
            grid.QueryRange(transform.position, detectionRange, _spatialResults,
                teamFilter: -1, excludeEntityId: controller?.GetHashCode() ?? -1);

            Transform bestTarget = null;
            var bestScore = float.MaxValue;

            foreach (var entity in _spatialResults)
            {
                var entityTransform = (entity as MonoBehaviour)?.transform;
                if (entityTransform == null) continue;

                // FOV 检查
                if (fieldOfView < 360f && !IsInFieldOfView(entityTransform))
                    continue;

                // 视线检查
                if (obstacleMask != 0 && !HasLineOfSight(entityTransform))
                    continue;

                // 距离加权评分（越近越好）
                var dist = Vector3.Distance(transform.position, entityTransform.position);
                if (dist < bestScore)
                {
                    bestScore = dist;
                    bestTarget = entityTransform;
                }
            }

            return bestTarget;
        }

        /// <summary>检测目标，子类实现具体检测逻辑</summary>
        protected abstract Transform DetectTarget();

        /// <summary>是否有视线遮挡</summary>
        protected bool HasLineOfSight(Transform target)
        {
            if (target == null) return false;
            var direction = target.position - transform.position;
            var distance = direction.magnitude;
            if (Physics.Raycast(transform.position, direction.normalized, distance, obstacleMask))
                return false;
            return true;
        }

        /// <summary>目标是否在视野角度内</summary>
        protected bool IsInFieldOfView(Transform target)
        {
            if (target == null) return false;
            var direction = (target.position - transform.position).normalized;
            var angle = Vector3.Angle(transform.forward, direction);
            return angle <= fieldOfView * 0.5f;
        }

        /// <summary>目标是否在检测范围内</summary>
        protected bool IsInDetectionRange(Transform target)
        {
            if (target == null) return false;
            return Vector3.Distance(transform.position, target.position) <= detectionRange;
        }

        public virtual void DrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);

            if (currentTarget != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, currentTarget.position);
            }

            // 空间哈希模式提示
            if (_useSpatialHash)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(transform.position, detectionRange * 0.5f);
            }
        }
    }
}
