using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace SkillAI
{
    /// <summary>
    ///     攻击目标 —— AI 对当前目标执行攻击行为。
    ///     企业级 AI 战斗节点，支持近战/远程模式。
    /// </summary>
    [Name("★ Attack Target")]
    [Category("Composites/AI/Actions")]
    [Description("AI攻击当前目标。支持近战/远程模式。\n黑板Target必须已设置。")]
    [Color("EF5350")]
    [ParadoxNotion.Design.Icon("Action")]
    public class AttackTarget : AIActionNode
    {
        public enum AttackMode
        {
            [Description("近战攻击")] Melee,
            [Description("远程攻击")] Ranged,
        }

        [Tooltip("攻击模式")]
        public AttackMode mode = AttackMode.Melee;

        [Tooltip("目标黑板键名")]
        public string targetKey = AIBBKey.Target;

        [Tooltip("攻击范围（近战有效）")]
        public BBParameter<float> attackRange = 2.5f;

        [Tooltip("每次攻击伤害量")]
        public BBParameter<float> damage = 10f;

        [Tooltip("攻击动画触发名称")]
        public string attackAnimationTrigger = "Attack";

        [Tooltip("攻击冷却（秒），用于AttackSpeed的计算基准")]
        public BBParameter<float> attackSpeed = 1f;

        [Tooltip("技能ID（用于释放技能攻击）")]
        public string skillId;

        private float _lastAttackTime = float.MinValue;
        private UnityEngine.Animator _animator;
        private bool _animationTriggered;

        protected override void OnActionInit(Component agent, IBlackboard blackboard)
        {
            _animator = agent.GetComponent<Animator>();
            if (_animator == null)
                _animator = agent.GetComponentInChildren<Animator>();
            _animationTriggered = false;
        }

        protected override Status OnExecuteOnce(Component agent, IBlackboard blackboard)
        {
            var target = blackboard.GetVariableValue<Transform>(targetKey);
            if (target == null) return Status.Failure;

            var dist = Vector3.Distance(agent.transform.position, target.position);
            blackboard.SetVariableValue(AIBBKey.DistanceToTarget, dist);

            // 距离检查
            if (mode == AttackMode.Melee && dist > attackRange.value)
            {
                // 不在攻击范围内，需要靠近
                blackboard.SetVariableValue(AIBBKey.TargetPosition, target.position);
                return Status.Failure; // 让MoveTo处理移动
            }

            // 冷却检查
            if (Time.time - _lastAttackTime < 1f / attackSpeed.value)
                return Status.Running;

            // 面向目标
            var lookDir = target.position - agent.transform.position;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
                agent.transform.rotation = Quaternion.LookRotation(lookDir);

            // 执行攻击
            blackboard.SetVariableValue(AIBBKey.IsAttacking, true);
            _lastAttackTime = Time.time;

            // 触发动画
            if (_animator != null && !string.IsNullOrEmpty(attackAnimationTrigger) && !_animationTriggered)
            {
                _animator.SetTrigger(attackAnimationTrigger);
                _animationTriggered = true;
            }

            // 模拟伤害（实际项目中通过战斗系统处理）
            blackboard.SetVariableValue(AIBBKey.LastDamage, damage.value);

            // 检查是否应该释放技能
            if (!string.IsNullOrEmpty(skillId))
            {
                blackboard.SetVariableValue(AIBBKey.CurrentSkillId, skillId);
            }

            return Status.Success;
        }

        protected override void OnActionReset()
        {
            blackboard?.SetVariableValue(AIBBKey.IsAttacking, false);
            _animationTriggered = false;
        }
    }
}
