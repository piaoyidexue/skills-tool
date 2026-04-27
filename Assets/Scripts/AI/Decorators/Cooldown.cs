using NodeCanvas.BehaviourTrees;
using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace SkillAI
{
    /// <summary>
    ///     冷却装饰器 —— 执行子节点后进入冷却期，冷却期内直接返回 Failure。
    ///     用于控制技能释放频率、行动间隔等。
    /// </summary>
    [Name("★ Cooldown")]
    [Category("Decorators/AI")]
    [Description("子节点执行后进入冷却。冷却期内跳过子节点直接返回 Failure。\n典型场景：技能CD、攻击间隔")]
    [Color("FF9800")]
    [ParadoxNotion.Design.Icon("Timeout")]
    public class Cooldown : BTDecorator
    {
        [Tooltip("冷却时间（秒）")]
        public BBParameter<float> cooldownTime = 2f;

        [Tooltip("冷却期满后是否自动重置子节点状态")]
        public bool resetChildOnCooldownEnd = true;

        private float _lastExecutionTime = float.MinValue;
        private bool _inCooldown;

        protected override Status OnExecute(Component agent, IBlackboard blackboard)
        {
            var timeSinceLast = Time.time - _lastExecutionTime;
            var cd = cooldownTime.value;

            if (timeSinceLast < cd)
            {
                _inCooldown = true;
                return Status.Failure;
            }

            _inCooldown = false;

            if (decoratedConnection == null)
                return Status.Success;

            if (resetChildOnCooldownEnd && timeSinceLast >= cd && status == Status.Resting)
            {
                decoratedConnection.Reset();
            }

            var result = decoratedConnection.Execute(agent, blackboard);

            if (result == Status.Success || result == Status.Failure)
            {
                _lastExecutionTime = Time.time;
            }

            return result;
        }

        protected override void OnReset()
        {
            _lastExecutionTime = float.MinValue;
            _inCooldown = false;
        }

#if UNITY_EDITOR
        protected override void OnNodeGUI()
        {
            if (_inCooldown)
            {
                var remaining = cooldownTime.value - (Time.time - _lastExecutionTime);
                if (remaining > 0)
                    GUILayout.Label($"<b>⏳ CD: {remaining:F1}s</b>");
            }
            else
            {
                GUILayout.Label($"<b>⏱ 就绪 ({cooldownTime.value}s)</b>");
            }
        }
#endif
    }
}
