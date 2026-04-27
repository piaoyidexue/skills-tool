using NodeCanvas.BehaviourTrees;
using NodeCanvas.Framework;
using UnityEngine;

namespace SkillAI
{
    /// <summary>
    ///     AI 控制器 —— MonoBehaviour 组件，挂载到 AI 角色上驱动行为树运行。
    ///     继承 BehaviourTreeOwner，获得完整的 GraphOwner 功能（序列化、启动、暂停等）。
    /// </summary>
    [AddComponentMenu("Skill System/AI Controller")]
    public class AIController : BehaviourTreeOwner
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

        [Header("运行时状态")]
        [SerializeField] private AIStateType currentState = AIStateType.Idle;
        [SerializeField] private AIAlertLevel alertLevel = AIAlertLevel.None;

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

        protected virtual void Awake()
        {
            controller = GetComponentInParent<AIController>();
        }

        /// <summary>执行感知扫描（由行为树 Action 或 AIController Update 驱动）</summary>
        public virtual void Scan()
        {
            var newTarget = DetectTarget();
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
        }
    }
}
