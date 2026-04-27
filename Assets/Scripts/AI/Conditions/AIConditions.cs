using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace SkillAI
{
    /// <summary>
    ///     是否有目标 —— 检查黑板中目标是否有效。
    /// </summary>
    [Name("★ Has Target")]
    [Category("Composites/AI/Conditions")]
    [Description("检查当前是否有有效目标。")]
    [Color("26A69A")]
    [ParadoxNotion.Design.Icon("Condition")]
    public class HasTarget : AIConditionNode
    {
        [Tooltip("目标黑板键名")]
        public string targetKey = AIBBKey.Target;

        [Tooltip("是否要求目标存活")]
        public bool requireAlive = true;

        protected override bool CheckCondition(Component agent, IBlackboard blackboard)
        {
            var target = blackboard.GetVariableValue<Transform>(targetKey);
            if (target == null) return false;

            if (requireAlive)
            {
                var hp = blackboard.GetVariableValue<float>(AIBBKey.TargetHealthPercent);
                if (hp <= 0) return false;
            }

            return true;
        }
    }

    /// <summary>
    ///     目标在范围内 —— 检查目标是否在指定范围内。
    /// </summary>
    [Name("★ Is Target In Range")]
    [Category("Composites/AI/Conditions")]
    [Description("检查目标是否在指定距离范围内。")]
    [Color("26A69A")]
    [ParadoxNotion.Design.Icon("Condition")]
    public class IsTargetInRange : AIConditionNode
    {
        [Tooltip("目标黑板键名")]
        public string targetKey = AIBBKey.Target;

        [Tooltip("检查范围")]
        public BBParameter<float> range = 5f;

        [Tooltip("黑板范围键（优先使用）")]
        public string rangeKey;

        protected override bool CheckCondition(Component agent, IBlackboard blackboard)
        {
            var target = blackboard.GetVariableValue<Transform>(targetKey);
            if (target == null) return false;

            var dist = Vector3.Distance(agent.transform.position, target.position);
            blackboard.SetVariableValue(AIBBKey.DistanceToTarget, dist);

            var checkRange = !string.IsNullOrEmpty(rangeKey)
                ? blackboard.GetVariableValue<float>(rangeKey)
                : range.value;

            return dist <= checkRange;
        }
    }

    /// <summary>
    ///     生命值低于阈值 —— 检查自身生命值百分比。
    /// </summary>
    [Name("★ Is Health Below")]
    [Category("Composites/AI/Conditions")]
    [Description("检查自身生命值是否低于指定百分比阈值。")]
    [Color("26A69A")]
    [ParadoxNotion.Design.Icon("Condition")]
    public class IsHealthBelow : AIConditionNode
    {
        [Tooltip("生命值百分比阈值（0-1）")]
        public BBParameter<float> threshold = 0.3f;

        [Tooltip("血量黑板键名")]
        public string healthKey = AIBBKey.HealthPercent;

        protected override bool CheckCondition(Component agent, IBlackboard blackboard)
        {
            var hp = blackboard.GetVariableValue<float>(healthKey);
            return hp <= threshold.value;
        }
    }

    /// <summary>
    ///     检测到敌人 —— 检查传感器是否检测到敌方单位。
    /// </summary>
    [Name("★ Has Enemy Detected")]
    [Category("Composites/AI/Conditions")]
    [Description("检查是否检测到敌方单位。")]
    [Color("26A69A")]
    [ParadoxNotion.Design.Icon("Condition")]
    public class HasEnemyDetected : AIConditionNode
    {
        [Tooltip("目标黑板键名")]
        public string targetKey = AIBBKey.Target;

        protected override bool CheckCondition(Component agent, IBlackboard blackboard)
        {
            var target = blackboard.GetVariableValue<Transform>(targetKey);
            blackboard.SetVariableValue(AIBBKey.HasTarget, target != null);
            return target != null;
        }
    }

    /// <summary>
    ///     黑板布尔条件 —— 检查黑板中的布尔变量。
    /// </summary>
    [Name("★ Blackboard Bool")]
    [Category("Composites/AI/Conditions")]
    [Description("检查黑板中的布尔变量值。")]
    [Color("26A69A")]
    [ParadoxNotion.Design.Icon("Condition")]
    public class BlackboardBool : AIConditionNode
    {
        [Tooltip("黑板键名")]
        public string key;

        [Tooltip("期望值")]
        public bool expectedValue = true;

        [Tooltip("条件不满足时自动重置子节点")]
        public bool resetChildOnFail;

        protected override bool CheckCondition(Component agent, IBlackboard blackboard)
        {
            var value = blackboard.GetVariableValue<bool>(key);
            return value == expectedValue;
        }
    }

    /// <summary>
    ///     冷却就绪 —— 检查黑板中记录的冷却时间是否已过。
    /// </summary>
    [Name("★ Cooldown Ready")]
    [Category("Composites/AI/Conditions")]
    [Description("检查指定技能的冷却是否就绪。")]
    [Color("26A69A")]
    [ParadoxNotion.Design.Icon("Condition")]
    public class CooldownReady : AIConditionNode
    {
        [Tooltip("上次执行时间的黑板键名")]
        public string lastUsedKey = AIBBKey.LastUsedSkillTime;

        [Tooltip("冷却时间（秒）")]
        public BBParameter<float> cooldown = 2f;

        protected override bool CheckCondition(Component agent, IBlackboard blackboard)
        {
            var lastTime = blackboard.GetVariableValue<float>(lastUsedKey);
            var elapsed = Time.time - lastTime;
            var ready = elapsed >= cooldown.value;
            blackboard.SetVariableValue(AIBBKey.SkillCooldownReady, ready);
            return ready;
        }
    }

    /// <summary>
    ///     比较黑板浮点值 —— 比较两个黑板浮点变量。
    /// </summary>
    [Name("★ Compare Float")]
    [Category("Composites/AI/Conditions")]
    [Description("比较黑板中的浮点变量（大于/小于/等于）。")]
    [Color("26A69A")]
    [ParadoxNotion.Design.Icon("Condition")]
    public class CompareFloat : AIConditionNode
    {
        public enum CompareMode { Greater, Less, Equal, GreaterOrEqual, LessOrEqual }

        [Tooltip("比较模式")]
        public CompareMode mode = CompareMode.Greater;

        [Tooltip("左边值的黑板键名")]
        public string keyA = AIBBKey.HealthPercent;

        [Tooltip("右边值的黑板键名（留空则使用固定值）")]
        public string keyB;

        [Tooltip("固定比较值（keyB为空时使用）")]
        public BBParameter<float> fixedValue;

        protected override bool CheckCondition(Component agent, IBlackboard blackboard)
        {
            var a = blackboard.GetVariableValue<float>(keyA);
            var b = !string.IsNullOrEmpty(keyB)
                ? blackboard.GetVariableValue<float>(keyB)
                : fixedValue.value;

            return mode switch
            {
                CompareMode.Greater => a > b,
                CompareMode.Less => a < b,
                CompareMode.Equal => Mathf.Approximately(a, b),
                CompareMode.GreaterOrEqual => a >= b,
                CompareMode.LessOrEqual => a <= b,
                _ => false
            };
        }
    }
}
