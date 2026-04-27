using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using System.Collections.Generic;

namespace SkillAI
{
    /// <summary>
    ///     巡逻 —— AI 按路径点循环移动，到达每个点后等待指定时间。
    ///     企业级 AI 的巡逻行为模板。
    /// </summary>
    [Name("★ Patrol Waypoints")]
    [Category("Composites/AI/Actions")]
    [Description("AI沿路径点巡逻。到达每个点后等待指定时间，完成后返回Success。\n支持循环/单次/往返模式。")]
    [Color("66BB6A")]
    [ParadoxNotion.Design.Icon("Action")]
    public class PatrolWaypoints : AIActionNode
    {
        public enum PatrolMode
        {
            [Description("循环（到头后回到起点）")] Loop,
            [Description("单次（到头后停止）")] Once,
            [Description("往返（来回巡逻）")] PingPong,
        }

        [Tooltip("巡逻模式")]
        public PatrolMode mode = PatrolMode.Loop;

        [Tooltip("路径点列表（Transform）")]
        public List<Transform> waypoints = new();

        [Tooltip("每点等待时间（秒）")]
        public BBParameter<float> waitTime = 1.5f;

        [Tooltip("最大巡逻次数（0=无限）")]
        public int maxCycles;

        private int _currentIndex;
        private int _direction = 1;
        private int _completedCycles;
        private float _waitStartedAt;
        private bool _isWaiting;

        protected override void OnActionInit(Component agent, IBlackboard blackboard)
        {
            if (_currentIndex >= waypoints.Count) _currentIndex = 0;
            _isWaiting = false;
        }

        protected override Status OnExecuteOnce(Component agent, IBlackboard blackboard)
        {
            if (waypoints.Count == 0)
                return Status.Failure;

            // 检查最大循环次数
            if (maxCycles > 0 && _completedCycles >= maxCycles)
                return Status.Success;

            var targetWP = waypoints[_currentIndex];
            if (targetWP == null)
            {
                AdvanceWaypoint();
                return Status.Running;
            }

            var dist = Vector3.Distance(agent.transform.position, targetWP.position);
            blackboard.SetVariableValue(AIBBKey.TargetPosition, targetWP.position);
            blackboard.SetVariableValue(AIBBKey.DistanceToTarget, dist);
            blackboard.SetVariableValue(AIBBKey.PatrolIndex, _currentIndex);

            // 到达当前点
            if (dist < 1.0f)
            {
                if (!_isWaiting)
                {
                    _waitStartedAt = Time.time;
                    _isWaiting = true;
                }

                blackboard.SetVariableValue(AIBBKey.PatrolWaitTime, waitTime.value - (Time.time - _waitStartedAt));

                // 等待时间未到
                if (Time.time - _waitStartedAt < waitTime.value)
                    return Status.Running;

                // 等待完成，前进到下一个点
                _isWaiting = false;
                AdvanceWaypoint();
            }

            blackboard.SetVariableValue(AIBBKey.IsMoving, !_isWaiting);
            return Status.Running;
        }

        private void AdvanceWaypoint()
        {
            switch (mode)
            {
                case PatrolMode.Once:
                    if (_currentIndex >= waypoints.Count - 1)
                    {
                        _completedCycles++;
                        return; // 保持在最后一点
                    }
                    _currentIndex++;
                    break;
                case PatrolMode.Loop:
                    _currentIndex++;
                    if (_currentIndex >= waypoints.Count)
                    {
                        _currentIndex = 0;
                        _completedCycles++;
                    }
                    break;
                case PatrolMode.PingPong:
                    _currentIndex += _direction;
                    if (_currentIndex >= waypoints.Count - 1)
                    {
                        _direction = -1;
                        _completedCycles++;
                    }
                    else if (_currentIndex <= 0)
                    {
                        _direction = 1;
                        _completedCycles++;
                    }
                    break;
            }
        }

        protected override void OnActionReset()
        {
            _currentIndex = 0;
            _direction = 1;
            _completedCycles = 0;
            _isWaiting = false;
        }
    }
}
