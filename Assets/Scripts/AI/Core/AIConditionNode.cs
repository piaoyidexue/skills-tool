using NodeCanvas.BehaviourTrees;
using NodeCanvas.Framework;
using ParadoxNotion;
using ParadoxNotion.Design;
using UnityEngine;

namespace SkillAI
{
    /// <summary>
    ///     AI 条件节点基类 —— 继承 BTNode，用于行为树中的条件判断分支。
    ///     条件节点只有一个子节点（装饰器模式），条件通过时执行子节点。
    /// </summary>
    [Category("★ AI Conditions")]
    public abstract class AIConditionNode : BTNode
    {
        public sealed override int maxOutConnections => 1;
        public sealed override Alignment2x2 iconAlignment => Alignment2x2.Default;
        public sealed override Alignment2x2 commentsAlignment => Alignment2x2.Right;

#if UNITY_EDITOR
        public override string description => GetType().RTGetAttribute<DescriptionAttribute>(true)?.description ?? name;
#endif

        /// <summary>检查条件是否满足</summary>
        protected abstract bool CheckCondition(Component agent, IBlackboard blackboard);

        /// <summary>条件检查前初始化（每帧可能调用）</summary>
        protected virtual void OnConditionEnable(Component agent, IBlackboard blackboard) { }
        /// <summary>条件检查结束清理</summary>
        protected virtual void OnConditionDisable() { }

        sealed protected override Status OnExecute(Component agent, IBlackboard blackboard)
        {
            if (status == Status.Resting)
                OnConditionEnable(agent, blackboard);

            if (!CheckCondition(agent, blackboard))
                return Status.Failure;

            // 条件满足，执行子节点
            if (outConnections.Count == 0)
                return Status.Success;

            return outConnections[0].Execute(agent, blackboard);
        }

        sealed protected override void OnReset()
        {
            OnConditionDisable();
        }
    }
}
