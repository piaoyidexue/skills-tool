using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  SkillEffectData —— 纯数据描述的单条效果
//  可被 SkillBuilder 从节点编译生成，也可被 TimelineSkillRunner 直接执行
//  禁止包含任何运行时逻辑，只描述"做什么"和"参数是什么"
// ============================================================

/// <summary>
///     技能效果类型枚举 —— 覆盖当前所有叶子节点的能力域。
///     新增效果类型无需修改运行时，只需在 EffectSystem 中注册处理器。
/// </summary>
public enum SkillEffectType
{
    None,

    // ---- 战斗数值 ----
    Damage,         // 造成伤害
    Heal,           // 治疗
    ApplyBuff,      // 施加 Buff/GE（持续性效果）
    RemoveBuff,     // 移除 Buff
    ModifyAttribute,// 修改属性（移速/攻速等）

    // ---- 视觉表现 ----
    PlayVFX,        // 播放特效
    PlaySFX,        // 播放音效
    SpawnProjectile,// 发射投射物
    ShakeCamera,    // 相机震动

    // ---- 目标查询 ----
    EQSQuery,       // 执行 EQS 查询，结果写入上下文

    // ---- 动画同步 ----
    PlayAnimation,  // 播放动画（Trigger/State）
    WaitAnimationEvent, // 等待动画事件（动态节点，编译时标记）

    // ---- 流程控制（编译期展开或运行时保留）----
    Branch,         // 条件分支（运行时解释）
    RollChance,     // 概率判定（运行时解释）
    SetBlackboard,  // 写入黑板值

    // ---- 地形 ----
    PaintTerrain,   // 地形标记

    // ---- 自定义 ----
    Custom          // 自定义扩展点
}

/// <summary>
///     目标选择模式 —— 效果作用于谁。
/// </summary>
public enum SkillEffectTargetMode
{
    Caster,         // 施法者自身
    PrimaryTarget,  // 技能主目标（SkillContext.Target）
    EQSResults,     // EQS 查询结果列表
    AoECenter,      // AoE 中心点
    Self            // 同 Caster，语义更明确
}

/// <summary>
///     纯数据效果的参数载体。
///     使用字段扁平化设计（而非 Dictionary）保证 0 GC 序列化性能。
/// </summary>
[Serializable]
public struct SkillEffectData
{
    // ---- 基础标识 ----
    public SkillEffectType EffectType;
    public string EffectId;           // 配置表 ID 或节点标识
    public string Description;        // 调试描述

    // ---- 目标 ----
    public SkillEffectTargetMode TargetMode;
    public string EQSQueryKey;        // 当 TargetMode = EQSResults 时使用
    public float AoERadius;           // 当需要范围时使用
    public int MaxTargets;            // 最大目标数（0=无限制）

    // ---- 数值参数（战斗类）----
    public float BaseValue;           // 基础值（伤害/治疗/数值）
    public float ValueMultiplier;     // 倍率
    public bool UseCrit;              // 是否可暴击
    public float CritChanceOverride;  // 覆盖暴击率（<0 使用默认值）

    // ---- GE/Buff 参数 ----
    public string BuffKey;            // Buff/GE 配置 Key
    public float Duration;            // 持续时间
    public int MaxStacks;             // 最大层数
    public int AbilityLevel;          // 技能等级（用于 GE 数值缩放）
    public List<string> TagsToApply;  // 授予的标签
    public List<string> TagsToRemove; // 移除的标签

    // ---- VFX/SFX 参数 ----
    public string VFXKey;             // 特效资源 Key
    public string SFXKey;             // 音效资源 Key
    public string StyleKey;           // 风格变体 Key（用于换色/换皮）
    public Vector3 VFXOffset;         // 特效位置偏移
    public float VFXScale;            // 特效缩放
    public bool AttachToTarget;       // 是否附着目标
    public Vector3 VFXDirection;      // 特效方向
    public float VFXLength;           // 特效长度覆盖
    public float VFXWidthMultiplier;  // 特效宽度倍率

    // ---- 投射物参数 ----
    public string ProjectileKey;      // 投射物预制体 Key
    public float ProjectileSpeed;     // 飞行速度
    public float ProjectileLifetime;  // 最大存活时间
    public bool ProjectileHoming;     // 是否追踪

    // ---- 动画参数 ----
    public string AnimationTrigger;   // Animator Trigger 名
    public string AnimationState;     // Animator State 名
    public float AnimationCrossFade;  // 过渡时间
    public string WaitEventName;      // 等待的动画事件名

    // ---- 黑板参数 ----
    public string BlackboardKey;      // 黑板 Key
    public string BlackboardValue;    // 黑板 Value（字符串形式，运行时转换）

    // ---- 相机参数 ----
    public float CameraShakeIntensity;
    public float CameraShakeDuration;

    // ---- 自定义扩展 ----
    public string CustomType;         // 当 EffectType = Custom 时的子类型
    public string CustomJson;         // 自定义参数的 JSON 序列化

    // ============================================================
    //  便捷工厂方法
    // ============================================================

    public static SkillEffectData CreateDamage(float baseValue, float multiplier = 1f,
        SkillEffectTargetMode target = SkillEffectTargetMode.PrimaryTarget)
    {
        return new SkillEffectData
        {
            EffectType = SkillEffectType.Damage,
            TargetMode = target,
            BaseValue = baseValue,
            ValueMultiplier = multiplier,
            UseCrit = true
        };
    }

    public static SkillEffectData CreateVFX(string vfxKey, SkillEffectTargetMode target = SkillEffectTargetMode.PrimaryTarget,
        string styleKey = "", float scale = 1f, bool attach = false,
        Vector3 direction = default, float length = 0f, float widthMultiplier = 1f)
    {
        return new SkillEffectData
        {
            EffectType = SkillEffectType.PlayVFX,
            TargetMode = target,
            VFXKey = vfxKey,
            StyleKey = styleKey,
            VFXScale = scale,
            AttachToTarget = attach,
            VFXDirection = direction,
            VFXLength = length,
            VFXWidthMultiplier = widthMultiplier
        };
    }

    public static SkillEffectData CreateApplyBuff(string buffKey, float duration,
        SkillEffectTargetMode target = SkillEffectTargetMode.PrimaryTarget, int abilityLevel = 1)
    {
        return new SkillEffectData
        {
            EffectType = SkillEffectType.ApplyBuff,
            TargetMode = target,
            BuffKey = buffKey,
            Duration = duration,
            AbilityLevel = abilityLevel
        };
    }

    public static SkillEffectData CreateProjectile(string projectileKey, float speed,
        SkillEffectTargetMode target = SkillEffectTargetMode.PrimaryTarget)
    {
        return new SkillEffectData
        {
            EffectType = SkillEffectType.SpawnProjectile,
            TargetMode = target,
            ProjectileKey = projectileKey,
            ProjectileSpeed = speed
        };
    }

    public static SkillEffectData CreateSetBlackboard(string key, string value)
    {
        return new SkillEffectData
        {
            EffectType = SkillEffectType.SetBlackboard,
            BlackboardKey = key,
            BlackboardValue = value
        };
    }
}
