using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  效果系统 (EffectSystem)
//  唯一合法的技能结算中枢。
//  所有伤害、回血、Buff 必须通过此系统投递，禁止直接调用 TakeDamage。
// ============================================================

/// <summary>
///     效果系统 —— 全局唯一的技能结算中枢。
///     所有效果必须通过此系统派发，禁止直接操作实体数据。
/// </summary>
public static class EffectSystem
{
    // ---- 全局事件 ----
    public static event Action<EffectSpec> OnEffectPreApply;
    public static event Action<EffectSpec> OnEffectPostApply;
    public static event Action<EffectSpec> OnEffectFailed;
    public static event Action<EffectSpec> OnDamageApplied;
    public static event Action<EffectSpec> OnHealingApplied;

    // ---- Modifier 管线 ----
    private static readonly List<IEffectModifier> Modifiers = new(16);
    private static readonly List<IReactionHandler> ReactionHandlers = new(8);

    // ---- 对象池 ----
    private static readonly Queue<EffectSpec> SpecPool = new();

    /// <summary>
    ///     注册 Modifier 到管线（按优先级排序）。
    /// </summary>
    public static void RegisterModifier(IEffectModifier modifier, int priority = 0)
    {
        if (modifier == null) return;
        Modifiers.Add(modifier);
        Modifiers.Sort((a, b) => b.Priority.CompareTo(a.Priority)); // 高优先级先执行
    }

    /// <summary>
    ///     注销 Modifier。
    /// </summary>
    public static void UnregisterModifier(IEffectModifier modifier)
    {
        if (modifier != null) Modifiers.Remove(modifier);
    }

    /// <summary>
    ///     注册反应处理器。
    /// </summary>
    public static void RegisterReactionHandler(IReactionHandler handler)
    {
        if (handler != null) ReactionHandlers.Add(handler);
    }

    /// <summary>
    ///     注销反应处理器。
    /// </summary>
    public static void UnregisterReactionHandler(IReactionHandler handler)
    {
        if (handler != null) ReactionHandlers.Remove(handler);
    }

    // ============================================================
    //  核心派发方法
    // ============================================================

    /// <summary>
    ///     应用游戏效果（主入口）。
    ///     流程：校验需求 → 构建 Spec → Modifier 管线 → 应用效果 → 释放 Spec。
    /// </summary>
    public static bool ApplyEffect(EffectContext context, GameplayEffectData data)
    {
        if (context.Target == null || data == null)
        {
            Debug.LogWarning("[EffectSystem] Invalid context or data");
            return false;
        }

        // 校验需求（免疫标签、必要标签等）
        if (!ValidateRequirements(context, data))
        {
            var spec = ObtainSpec(context, data);
            spec.MarkProcessed("RequirementNotMet");
            OnEffectFailed?.Invoke(spec);
            ReleaseSpec(spec);
            return false;
        }

        // 从对象池获取 Spec
        var effectSpec = ObtainSpec(context, data);

        // 前置事件
        OnEffectPreApply?.Invoke(effectSpec);

        // 运行 Modifier 管线
        RunModifierPipeline(effectSpec);

        // 处理元素反应
        RunReactionHandlers(effectSpec);

        // 应用数值效果
        ApplyEffectToTarget(effectSpec);

        // 后置事件
        OnEffectPostApply?.Invoke(effectSpec);

        // 释放 Spec 回对象池
        ReleaseSpec(effectSpec);
        return true;
    }

    /// <summary>
    ///     批量应用效果到多个目标。
    /// </summary>
    public static int ApplyEffectToTargets(EffectContext context, GameplayEffectData data,
        IReadOnlyList<Transform> targets)
    {
        if (targets == null || targets.Count == 0) return 0;

        var successCount = 0;
        foreach (var target in targets)
        {
            if (target == null) continue;

            // 为每个目标创建独立上下文
            var singleContext = context;
            singleContext.Target = target;
            singleContext.TargetHost = target.GetComponent<GEHost>();
            singleContext.TargetPoint = target.position;

            if (ApplyEffect(singleContext, data)) successCount++;
        }
        return successCount;
    }

    /// <summary>
    ///     预览效果结果（不实际应用）。
    /// </summary>
    public static float PreviewDamage(EffectContext context, GameplayEffectData data)
    {
        if (context.Target == null || data == null) return 0f;

        var spec = ObtainSpec(context, data);
        RunModifierPipeline(spec);
        var damage = spec.GetTotalDamage();
        ReleaseSpec(spec);
        return damage;
    }

    // ============================================================
    //  内部处理流程
    // ============================================================

    private static bool ValidateRequirements(EffectContext context, GameplayEffectData data)
    {
        // 检查目标免疫标签
        if (data.ImmuneTags != null && context.TargetHost != null)
        {
            foreach (var tag in data.ImmuneTags)
                if (context.TargetHost.HasTag(tag)) return false;
        }

        // 检查目标必需标签
        if (data.RequiredTargetTags != null && context.TargetHost != null)
        {
            foreach (var tag in data.RequiredTargetTags)
                if (!context.TargetHost.HasTag(tag)) return false;
        }

        // 检查施法者必需标签
        if (data.RequiredSourceTags != null && context.InstigatorHost != null)
        {
            foreach (var tag in data.RequiredSourceTags)
                if (!context.InstigatorHost.HasTag(tag)) return false;
        }

        return true;
    }

    private static void RunModifierPipeline(EffectSpec spec)
    {
        foreach (var modifier in Modifiers)
        {
            try
            {
                modifier.Modify(spec);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EffectSystem] Modifier {modifier.GetType().Name} failed: {ex.Message}");
            }
        }
    }

    private static void RunReactionHandlers(EffectSpec spec)
    {
        foreach (var handler in ReactionHandlers)
        {
            try
            {
                handler.HandleReaction(spec);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EffectSystem] ReactionHandler {handler.GetType().Name} failed: {ex.Message}");
            }
        }
    }

    private static void ApplyEffectToTarget(EffectSpec spec)
    {
        var target = spec.Context.Target;
        if (target == null) return;

        // 暴击判定（如果没有被 Modifier 修改）
        if (!spec.IsProcessed && spec.CalculatedDamage > 0)
        {
            spec.IsCriticalHit = UnityEngine.Random.value < spec.Data.CritChanceBonus;
        }

        // 应用伤害
        if (spec.CalculatedDamage > 0)
        {
            DamagePipeline.CalculateAndApply(
                spec.GetTotalDamage(),
                target,
                spec.Context.Instigator,
                spec.AppliedTags?.ToArray());
            OnDamageApplied?.Invoke(spec);
        }

        // 应用治疗
        if (spec.CalculatedHealing > 0)
        {
            ApplyHealing(spec);
        }

        // 应用 Buff/持续效果
        if (spec.Data.DurationPolicy != GEDurationPolicy.Instant && spec.Context.TargetHost != null)
        {
            ApplyBuffEffect(spec);
        }

        spec.MarkProcessed("Applied");
    }

    private static void ApplyHealing(EffectSpec spec)
    {
        var health = spec.Context.Target.GetComponent<HealthComponent>();
        if (health != null)
        {
            health.Heal(spec.GetTotalHealing());
            OnHealingApplied?.Invoke(spec);
        }
    }

    private static void ApplyBuffEffect(EffectSpec spec)
    {
        var geConfig = new GEConfig
        {
            GEId = spec.Data.EffectId,
            Name = spec.Data.EffectName,
            DurationPolicy = spec.Data.DurationPolicy,
            Duration = spec.Data.Duration,
            Period = spec.Data.Period,
            StackPolicy = spec.Data.StackPolicy,
            MaxStacks = spec.Data.MaxStacks
        };
        geConfig.GrantedTags.AddRange(spec.AppliedTags ?? new List<string>());

        // 添加 Modifier
        if (spec.CalculatedDamage > 0)
        {
            geConfig.Modifiers.Add(new GEModifier
            {
                Attribute = GEAttribute.DamageTakenMultiplier,
                Operation = GEModOp.Multiply,
                Magnitude = 1.1f // 示例：受到伤害增加 10%
            });
        }

        spec.Context.TargetHost?.ApplyEffect(geConfig, spec.Context.Instigator);
    }

    // ============================================================
    //  对象池管理
    // ============================================================

    private static EffectSpec ObtainSpec(EffectContext context, GameplayEffectData data)
    {
        return EffectSpec.Obtain(context, data);
    }

    private static void ReleaseSpec(EffectSpec spec)
    {
        if (spec != null) spec.Release();
    }
}

// ============================================================
//  Modifier 接口
// ============================================================

/// <summary>
///     效果修改器接口。
///     实现此接口并注册到 EffectSystem 以拦截和修改 EffectSpec。
/// </summary>
public interface IEffectModifier
{
    /// <summary>优先级（越高越先执行）</summary>
    int Priority { get; }

    /// <summary>
    ///     修改 EffectSpec。
    ///     可修改 CalculatedDamage、CalculatedHealing、AppliedTags 等。
    /// </summary>
    void Modify(EffectSpec spec);
}

// ============================================================
//  Reaction Handler 接口
// ============================================================

/// <summary>
///     元素反应处理器接口。
///     实现此接口并注册到 EffectSystem 以处理元素反应逻辑。
/// </summary>
public interface IReactionHandler
{
    /// <summary>优先级（越高越先执行）</summary>
    int Priority { get; }

    /// <summary>
    ///     处理元素反应。
    ///     可修改 Spec 数值、派发反应事件、添加额外标签等。
    /// </summary>
    void HandleReaction(EffectSpec spec);
}