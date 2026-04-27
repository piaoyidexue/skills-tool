using NodeCanvas.BehaviourTrees;
using NodeCanvas.Framework;
using ParadoxNotion;
using ParadoxNotion.Design;
using UnityEngine;

namespace SkillAI
{
    /// <summary>
    ///     优先级选择器 —— 按优先级从高到低依次执行子节点，
    ///     直到某个子节点返回 Success，则返回 Success。
    ///     如果所有子节点都返回 Failure，则返回 Failure。
    ///     支持动态重新评估（Dynamic 模式下每帧重新检查高优先级节点）。
    ///     企业级 AI 的默认行为选择模式。
    /// </summary>
    [Name("★ Priority Selector")]
    [Category("Composites/AI")]
    [Description("按优先级依次尝试子节点，优先响应对策。支持动态重新评估。" +
                 "\n典型场景：逃跑 > 治疗 > 攻击 > 巡逻 > 待机")]
    [Color("4CAF50")]
    [ParadoxNotion.Design.Icon("Selector")]
    public class PrioritizedSelector : BTComposite
    {
        [Tooltip("启用动态重评估：每帧从第一个子节点重新检查，确保高优先级行为抢先执行")]
        public bool dynamic;

        [Tooltip("随机打乱同优先级节点的执行顺序")]
        public bool randomOrder;

        private int lastRunningNodeIndex;

        protected override Status OnExecute(Component agent, IBlackboard blackboard)
        {
            var startIndex = dynamic ? 0 : lastRunningNodeIndex;

            for (var i = startIndex; i < outConnections.Count; i++)
            {
                status = outConnections[i].Execute(agent, blackboard);

                switch (status)
                {
                    case Status.Running:
                        if (dynamic && i < lastRunningNodeIndex)
                            ResetHigherPriorityChildren(i + 1, lastRunningNodeIndex);

                        lastRunningNodeIndex = i;
                        return Status.Running;

                    case Status.Success:
                        if (dynamic && i < lastRunningNodeIndex)
                            ResetHigherPriorityChildren(i + 1, lastRunningNodeIndex);

                        lastRunningNodeIndex = 0;
                        return Status.Success;

                    case Status.Failure:
                        continue;
                }
            }

            lastRunningNodeIndex = 0;
            return Status.Failure;
        }

        private void ResetHigherPriorityChildren(int from, int to)
        {
            for (var j = from; j <= to; j++)
                outConnections[j].Reset();
        }

        protected override void OnReset()
        {
            lastRunningNodeIndex = 0;
            if (randomOrder)
                outConnections = outConnections.Shuffle();
        }

        public override void OnChildDisconnected(int index)
        {
            if (index != 0 && index <= lastRunningNodeIndex)
                lastRunningNodeIndex--;
        }

        public override void OnGraphStarted() => OnReset();

#if UNITY_EDITOR
        protected override void OnNodeGUI()
        {
            if (dynamic) GUILayout.Label("<b>⚡ DYNAMIC</b>");
            if (randomOrder) GUILayout.Label("<b>🎲 RANDOM</b>");
        }
#endif
    }
}
