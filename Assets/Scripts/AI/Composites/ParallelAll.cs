using NodeCanvas.BehaviourTrees;
using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace SkillAI
{
    /// <summary>
    ///     并行全部节点 —— 同时执行所有子节点。
    ///     可以配置为"全部成功才算成功"或"任一失败即失败"模式。
    ///     用于需要同时执行多个行为的场景（如边移动边射击）。
    /// </summary>
    [Name("★ Parallel All")]
    [Category("Composites/AI")]
    [Description("同时执行所有子节点。\n" +
                 "- Require All: 所有子节点成功才返回Success\n" +
                 "- Fail On Any: 任一子节点失败立即返回Failure\n" +
                 "典型场景：边移动边开火、同时巡逻和检测")]
    [Color("2196F3")]
    [ParadoxNotion.Design.Icon("Parallel")]
    public class ParallelAll : BTComposite
    {
        [Tooltip("是否要求所有子节点都成功")]
        public bool requireAllSuccess = true;

        [Tooltip("任一子节点失败时是否立即终止所有并行任务")]
        public bool failOnAny;

        private bool[] _childStatusChecked;

        protected override Status OnExecute(Component agent, IBlackboard blackboard)
        {
            if (_childStatusChecked == null || _childStatusChecked.Length != outConnections.Count)
                _childStatusChecked = new bool[outConnections.Count];

            var anyRunning = false;
            var anyFailed = false;
            var allSuccess = true;

            for (var i = 0; i < outConnections.Count; i++)
            {
                var childStatus = outConnections[i].Execute(agent, blackboard);

                switch (childStatus)
                {
                    case Status.Running:
                        anyRunning = true;
                        allSuccess = false;
                        break;
                    case Status.Failure:
                        anyFailed = true;
                        allSuccess = false;
                        _childStatusChecked[i] = true;
                        if (failOnAny)
                        {
                            // 终止所有子节点
                            for (var j = 0; j < outConnections.Count; j++)
                            {
                                if (j != i) outConnections[j].Reset();
                            }
                            return Status.Failure;
                        }
                        break;
                    case Status.Success:
                        _childStatusChecked[i] = true;
                        break;
                }
            }

            if (anyRunning)
                return Status.Running;

            if (failOnAny && anyFailed)
                return Status.Failure;

            if (requireAllSuccess)
                return allSuccess ? Status.Success : Status.Failure;

            return anyFailed ? Status.Failure : Status.Success;
        }

        protected override void OnReset()
        {
            _childStatusChecked = new bool[outConnections.Count];
        }
    }
}
