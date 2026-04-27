using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace SkillAI
{
    /// <summary>
    ///     释放技能 —— AI 使用指定技能（与技能系统集成）。
    /// </summary>
    [Name("★ Cast Skill")]
    [Category("Composites/AI/Actions")]
    [Description("AI释放指定技能。需要技能系统支持。")]
    [Color("7E57C2")]
    [ParadoxNotion.Design.Icon("Action")]
    public class CastSkill : AIActionNode
    {
        [Tooltip("技能ID")]
        public string skillId;

        [Tooltip("目标黑板键名")]
        public string targetKey = AIBBKey.Target;

        [Tooltip("技能释放距离")]
        public BBParameter<float> castRange = 10f;

        private bool _casted;

        protected override void OnActionInit(Component agent, IBlackboard blackboard)
        {
            _casted = false;
        }

        protected override Status OnExecuteOnce(Component agent, IBlackboard blackboard)
        {
            if (string.IsNullOrEmpty(skillId))
                return Status.Failure;

            var target = blackboard.GetVariableValue<Transform>(targetKey);
            var dist = target != null
                ? Vector3.Distance(agent.transform.position, target.position)
                : 0f;

            // 距离检查
            if (target != null && dist > castRange.value)
            {
                blackboard.SetVariableValue(AIBBKey.TargetPosition, target.position);
                return Status.Failure; // 需要靠近
            }

            if (_casted) return Status.Success;

            // 面向目标
            if (target != null)
            {
                var lookDir = target.position - agent.transform.position;
                lookDir.y = 0;
                if (lookDir != Vector3.zero)
                    agent.transform.rotation = Quaternion.LookRotation(lookDir);
            }

            // 写入黑板通知技能系统
            blackboard.SetVariableValue(AIBBKey.CurrentSkillId, skillId);
            blackboard.SetVariableValue(AIBBKey.LastUsedSkillTime, Time.time);
            _casted = true;

            return Status.Success;
        }

        protected override void OnActionReset()
        {
            _casted = false;
        }
    }
}
