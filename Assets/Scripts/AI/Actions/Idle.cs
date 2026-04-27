using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace SkillAI
{
    /// <summary>
    ///     待机 —— AI 原地等待指定时间。
    /// </summary>
    [Name("★ Idle")]
    [Category("Composites/AI/Actions")]
    [Description("AI原地待机指定时间后返回Success。")]
    [Color("78909C")]
    [ParadoxNotion.Design.Icon("Action")]
    public class Idle : AIActionNode
    {
        [Tooltip("待机时间（秒）")]
        public BBParameter<float> duration = 1f;

        [Tooltip("待机时是否随机播放动画")]
        public bool playRandomIdleAnimation;

        private float _startTime;
        private Animator _animator;

        protected override void OnActionInit(Component agent, IBlackboard blackboard)
        {
            _startTime = Time.time;
            if (playRandomIdleAnimation)
                _animator = agent.GetComponentInChildren<Animator>();
        }

        protected override Status OnExecuteOnce(Component agent, IBlackboard blackboard)
        {
            if (Time.time - _startTime >= duration.value)
                return Status.Success;

            blackboard.SetVariableValue(AIBBKey.IsMoving, false);
            return Status.Running;
        }
    }
}
