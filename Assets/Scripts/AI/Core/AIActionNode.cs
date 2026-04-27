using NodeCanvas.BehaviourTrees;
using NodeCanvas.Framework;
using ParadoxNotion;
using ParadoxNotion.Design;
using UnityEngine;

namespace SkillAI
{
    /// <summary>
    ///     AI 行为节点基类 —— 继承 BTNode，不依赖 Task 系统，直接实现行为逻辑。
    ///     企业级 AI 行为树中的原子操作节点。
    /// </summary>
    [Category("★ AI Actions")]
    public abstract class AIActionNode : BTNode
    {
        public sealed override int maxOutConnections => 0;
        public sealed override Alignment2x2 iconAlignment => Alignment2x2.Default;
        public sealed override Alignment2x2 commentsAlignment => Alignment2x2.Bottom;

        /// <summary>当前 agent（Component 引用，在 OnExecute 时更新）</summary>
        protected Component agent { get; private set; }
        /// <summary>当前黑板引用（在 OnExecute 时更新）</summary>
        protected IBlackboard blackboard { get; private set; }

#if UNITY_EDITOR
        public override string description => GetType().RTGetAttribute<DescriptionAttribute>(true)?.description ?? name;
#endif

        /// <summary>执行行为，子类必须实现。返回 Success/Failure/Running</summary>
        protected abstract Status OnExecuteOnce(Component agent, IBlackboard blackboard);

        /// <summary>行为初始化（每次从 Resting 进入执行时调用一次）</summary>
        protected virtual void OnActionInit(Component agent, IBlackboard blackboard) { }
        /// <summary>行为重置时调用</summary>
        protected virtual void OnActionReset() { }
        /// <summary>行为暂停时调用</summary>
        protected virtual void OnActionPause() { }

        sealed protected override Status OnExecute(Component agent, IBlackboard blackboard)
        {
            this.agent = agent;
            this.blackboard = blackboard;
            if (status == Status.Resting)
                OnActionInit(agent, blackboard);
            return OnExecuteOnce(agent, blackboard);
        }

        sealed protected override void OnReset()
        {
            OnActionReset();
        }

        public sealed override void OnGraphPaused()
        {
            OnActionPause();
        }
    }
}
