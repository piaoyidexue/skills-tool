using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace SkillAI
{
    /// <summary>
    ///     播放动画 —— AI 播放指定动画。
    /// </summary>
    [Name("★ Play Animation")]
    [Category("Composites/AI/Actions")]
    [Description("AI播放指定动画（Trigger/Bool）。")]
    [Color("5C6BC0")]
    [ParadoxNotion.Design.Icon("Action")]
    public class PlayAnimation : AIActionNode
    {
        public enum ParamType { Trigger, Bool, Float }

        [Tooltip("参数类型")]
        public ParamType paramType = ParamType.Trigger;

        [Tooltip("参数名称")]
        public string paramName = "Action";

        [Tooltip("Bool 值时设置的值")]
        public bool boolValue = true;

        [Tooltip("Float 值时设置的值")]
        public float floatValue;

        private Animator _animator;

        protected override void OnActionInit(Component agent, IBlackboard blackboard)
        {
            _animator = agent.GetComponent<Animator>();
            if (_animator == null)
                _animator = agent.GetComponentInChildren<Animator>();
        }

        protected override Status OnExecuteOnce(Component agent, IBlackboard blackboard)
        {
            if (_animator == null || string.IsNullOrEmpty(paramName))
                return Status.Failure;

            switch (paramType)
            {
                case ParamType.Trigger:
                    _animator.SetTrigger(paramName);
                    break;
                case ParamType.Bool:
                    _animator.SetBool(paramName, boolValue);
                    break;
                case ParamType.Float:
                    _animator.SetFloat(paramName, floatValue);
                    break;
            }

            return Status.Success;
        }
    }
}
