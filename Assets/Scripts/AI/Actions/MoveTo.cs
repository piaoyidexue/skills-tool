using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace SkillAI
{
    /// <summary>
    ///     移动到目标 —— AI 向指定位置/目标移动，到达后返回 Success。
    ///     企业级 AI 的基础移动行为。
    /// </summary>
    [Name("★ Move To")]
    [Category("Composites/AI/Actions")]
    [Description("AI移动到目标位置。支持黑板变量作为目标源。\n移动模式支持：NavMesh、Transform、Waypoint")]
    [Color("42A5F5")]
    [ParadoxNotion.Design.Icon("Action")]
    public class MoveTo : AIActionNode
    {
        /// <summary>目标源模式</summary>
        public enum TargetMode
        {
            [Description("黑板目标 Transform")] BlackboardTarget,
            [Description("黑板目标位置 Vector3")] BlackboardPosition,
            [Description("指定固定位置")] FixedPosition,
        }

        [Tooltip("目标源模式")]
        public TargetMode mode = TargetMode.BlackboardTarget;

        [Tooltip("目标 Transform 黑板键名")]
        public string targetKey = AIBBKey.Target;

        [Tooltip("目标位置黑板键名")]
        public string positionKey = AIBBKey.TargetPosition;

        [Tooltip("固定目标位置")]
        public Vector3 fixedPosition;

        [Tooltip("到达距离阈值")]
        public BBParameter<float> arriveDistance = 1.5f;

        [Tooltip("移动速度系数（0-1）")]
        public BBParameter<float> speedMultiplier = 1f;

        [Tooltip("超时时间（秒），0=永不超时")]
        public BBParameter<float> timeout;

        [Tooltip("移动时是否面向目标")]
        public bool faceTarget = true;

        [Tooltip("面向旋转速度")]
        public float rotationSpeed = 10f;

        private float _startTime;
        private UnityEngine.AI.NavMeshAgent _navAgent;

        protected override void OnActionInit(Component agent, IBlackboard blackboard)
        {
            _startTime = Time.time;
            _navAgent = agent.GetComponent<UnityEngine.AI.NavMeshAgent>();
        }

        protected override Status OnExecuteOnce(Component agent, IBlackboard blackboard)
        {
            // 超时检查
            if (timeout.value > 0 && Time.time - _startTime > timeout.value)
                return Status.Failure;

            // 获取目标位置
            Vector3 targetPos;
            switch (mode)
            {
                case TargetMode.BlackboardTarget:
                    var target = blackboard.GetVariableValue<Transform>(targetKey);
                    if (target == null) return Status.Failure;
                    targetPos = target.position;
                    break;
                case TargetMode.BlackboardPosition:
                    targetPos = blackboard.GetVariableValue<Vector3>(positionKey);
                    break;
                case TargetMode.FixedPosition:
                default:
                    targetPos = fixedPosition;
                    break;
            }

            // 检查到达
            var currentPos = agent.transform.position;
            currentPos.y = 0;
            targetPos.y = 0;
            var distance = Vector3.Distance(currentPos, targetPos);
            blackboard.SetVariableValue(AIBBKey.DistanceToTarget, distance);

            if (distance <= arriveDistance.value)
            {
                blackboard.SetVariableValue(AIBBKey.HasReachedDestination, true);
                if (_navAgent != null && _navAgent.isActiveAndEnabled && _navAgent.isOnNavMesh)
                    _navAgent.isStopped = true;
                return Status.Success;
            }

            blackboard.SetVariableValue(AIBBKey.HasReachedDestination, false);

            // 执行移动
            if (_navAgent != null && _navAgent.isActiveAndEnabled && _navAgent.isOnNavMesh)
            {
                _navAgent.speed = blackboard.GetVariableValue<float>(AIBBKey.MoveSpeed) * speedMultiplier.value;
                _navAgent.SetDestination(targetPos);
                _navAgent.isStopped = false;
            }
            else
            {
                // 简易移动（无 NavMesh）
                var moveSpeed = blackboard.GetVariableValue<float>(AIBBKey.MoveSpeed) * speedMultiplier.value;
                var dir = (targetPos - agent.transform.position).normalized;
                agent.transform.position += dir * moveSpeed * Time.deltaTime;
            }

            // 面向目标
            if (faceTarget)
            {
                var lookDir = targetPos - agent.transform.position;
                lookDir.y = 0;
                if (lookDir != Vector3.zero)
                {
                    var targetRot = Quaternion.LookRotation(lookDir);
                    agent.transform.rotation = Quaternion.Slerp(agent.transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
                }
            }

            blackboard.SetVariableValue(AIBBKey.IsMoving, true);
            return Status.Running;
        }

        protected override void OnActionReset()
        {
            if (_navAgent != null && _navAgent.isActiveAndEnabled && _navAgent.isOnNavMesh)
                _navAgent.isStopped = true;
            blackboard?.SetVariableValue(AIBBKey.IsMoving, false);
        }

        protected override void OnActionPause()
        {
            if (_navAgent != null && _navAgent.isActiveAndEnabled && _navAgent.isOnNavMesh)
                _navAgent.isStopped = true;
        }
    }
}
