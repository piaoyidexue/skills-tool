using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace SkillAI
{
    /// <summary>
    ///     逃跑 —— AI 远离当前目标到安全距离。
    /// </summary>
    [Name("★ Flee")]
    [Category("Composites/AI/Actions")]
    [Description("AI远离目标逃跑。到达安全距离后返回Success。")]
    [Color("FF7043")]
    [ParadoxNotion.Design.Icon("Action")]
    public class Flee : AIActionNode
    {
        [Tooltip("目标黑板键名")]
        public string targetKey = AIBBKey.Target;

        [Tooltip("安全距离")]
        public BBParameter<float> safeDistance = 15f;

        [Tooltip("逃跑速度系数")]
        public BBParameter<float> fleeSpeedMultiplier = 1.5f;

        private UnityEngine.AI.NavMeshAgent _navAgent;

        protected override void OnActionInit(Component agent, IBlackboard blackboard)
        {
            _navAgent = agent.GetComponent<UnityEngine.AI.NavMeshAgent>();
        }

        protected override Status OnExecuteOnce(Component agent, IBlackboard blackboard)
        {
            var target = blackboard.GetVariableValue<Transform>(targetKey);
            if (target == null) return Status.Success; // 没有目标，无需逃跑

            var currentPos = agent.transform.position;
            var targetPos = target.position;
            var dist = Vector3.Distance(currentPos, targetPos);
            blackboard.SetVariableValue(AIBBKey.DistanceToTarget, dist);

            // 已在安全距离外
            if (dist >= safeDistance.value)
                return Status.Success;

            // 计算逃跑方向（远离目标）
            var fleeDir = (currentPos - targetPos).normalized;
            var destination = currentPos + fleeDir * safeDistance.value;

            if (_navAgent != null && _navAgent.isActiveAndEnabled && _navAgent.isOnNavMesh)
            {
                _navAgent.speed = blackboard.GetVariableValue<float>(AIBBKey.MoveSpeed) * fleeSpeedMultiplier.value;
                _navAgent.SetDestination(destination);
                _navAgent.isStopped = false;
            }
            else
            {
                agent.transform.position += fleeDir * blackboard.GetVariableValue<float>(AIBBKey.MoveSpeed) * fleeSpeedMultiplier.value * Time.deltaTime;
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
    }
}
