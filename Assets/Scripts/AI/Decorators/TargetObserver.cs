using NodeCanvas.BehaviourTrees;
using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace SkillAI
{
    /// <summary>
    ///     目标观察装饰器 —— 持续检查目标是否有效。
    ///     当目标丢失（死亡/超出范围）时终止子节点，返回 Failure。
    ///     企业级 AI 的核心装饰器，确保行为只在目标有效时执行。
    /// </summary>
    [Name("★ Target Observer")]
    [Category("Decorators/AI")]
    [Description("目标有效时才执行子节点。目标丢失则终止并返回Failure。\n典型场景：追击时目标消失则停止")]
    [Color("E91E63")]
    [ParadoxNotion.Design.Icon("Condition")]
    public class TargetObserver : BTDecorator
    {
        [Tooltip("最大观察距离（0=不限制）")]
        public BBParameter<float> maxObserveDistance = 30f;

        [Tooltip("是否检查视线遮挡")]
        public bool checkLineOfSight;

        [Tooltip("目标死亡是否算作丢失")]
        public bool targetMustBeAlive = true;

        [Header("黑板键名")]
        [Tooltip("目标引用的黑板键")]
        public string targetKey = AIBBKey.Target;

        protected override Status OnExecute(Component agent, IBlackboard blackboard)
        {
            var target = blackboard.GetVariableValue<Transform>(targetKey);

            // 无目标
            if (target == null)
                return Status.Failure;

            // 目标死亡检查
            if (targetMustBeAlive)
            {
                // 尝试通过常见方式检查死亡（可扩展为接口）
                var hpVar = blackboard.GetVariable<float>(AIBBKey.TargetHealthPercent);
                if (hpVar != null && hpVar.value <= 0)
                {
                    blackboard.SetVariableValue(targetKey, (Transform)null);
                    return Status.Failure;
                }
            }

            // 距离检查
            if (maxObserveDistance.value > 0)
            {
                var dist = Vector3.Distance(agent.transform.position, target.position);
                if (dist > maxObserveDistance.value)
                {
                    // 目标超出观察范围，记录最后位置
                    blackboard.SetVariableValue(AIBBKey.LastKnownPosition, target.position);
                    blackboard.SetVariableValue(targetKey, (Transform)null);
                    return Status.Failure;
                }
            }

            // 视线检查
            if (checkLineOfSight)
            {
                var dir = target.position - agent.transform.position;
                if (Physics.Raycast(agent.transform.position, dir.normalized, dir.magnitude, LayerMask.GetMask("Default", "Obstacle")))
                {
                    return Status.Failure;
                }
            }

            // 更新距离
            blackboard.SetVariableValue(AIBBKey.DistanceToTarget,
                Vector3.Distance(agent.transform.position, target.position));

            if (decoratedConnection == null)
                return Status.Success;

            return decoratedConnection.Execute(agent, blackboard);
        }
    }
}
