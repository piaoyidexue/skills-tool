using System;
using UnityEngine;

namespace SkillAI
{
    /// <summary>
    ///     AI 黑板键常量定义。
    ///     统一管理 AI 行为树中使用的黑板变量名称，避免硬编码字符串。
    /// </summary>
    public static class AIBBKey
    {
        // ---- 目标相关 ----
        public const string Target = "Target";
        public const string TargetPosition = "TargetPosition";
        public const string LastKnownPosition = "LastKnownPosition";
        public const string IsTargetVisible = "IsTargetVisible";
        public const string DistanceToTarget = "DistanceToTarget";
        public const string HasTarget = "HasTarget";

        // ---- 战斗相关 ----
        public const string IsInCombat = "IsInCombat";
        public const string IsAttacking = "IsAttacking";
        public const string LastDamage = "LastDamage";
        public const string HealthPercent = "HealthPercent";
        public const string ManaPercent = "ManaPercent";
        public const string TargetHealthPercent = "TargetHealthPercent";

        // ---- 移动相关 ----
        public const string MoveSpeed = "MoveSpeed";
        public const string PatrolIndex = "PatrolIndex";
        public const string IsMoving = "IsMoving";
        public const string HasReachedDestination = "HasReachedDestination";
        public const string PatrolWaitTime = "PatrolWaitTime";

        // ---- 技能相关 ----
        public const string CurrentSkillId = "CurrentSkillId";
        public const string SkillCooldownReady = "SkillCooldownReady";
        public const string LastUsedSkillTime = "LastUsedSkillTime";

        // ---- 状态相关 ----
        public const string AIState = "AIState";
        public const string AlertLevel = "AlertLevel";
        public const string HasBuff = "HasBuff";
        public const string BuffType = "BuffType";

        // ---- 传感器 ----
        public const string DetectionRange = "DetectionRange";
        public const string AttackRange = "AttackRange";
        public const string FleeHealthThreshold = "FleeHealthThreshold";
        public const string EnemyCount = "EnemyCount";
        public const string AllyCount = "AllyCount";
    }

    /// <summary>
    ///     AI 状态枚举
    /// </summary>
    public enum AIStateType
    {
        Idle,       // 空闲
        Patrol,     // 巡逻
        Chase,      // 追击
        Combat,     // 战斗
        Flee,       // 逃跑
        Dead,       // 死亡
    }

    /// <summary>
    ///     AI 警戒等级
    /// </summary>
    public enum AIAlertLevel
    {
        None = 0,       // 无
        Suspicious = 1, // 可疑
        Alert = 2,      // 警戒
        Combat = 3,     // 战斗
    }
}
