using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  TimelineSkillRunner —— 时间轴驱动的技能执行器
//  职责：按 SkillData 时间轴推进，触发 SkillStep → SkillEffectData → EffectSystem
//  遇到动态步骤时触发 OnDynamicStep 事件，由外部处理回退。
// ============================================================

/// <summary>
///     时间轴技能执行器 —— 0 GC，无协程，纯数据驱动。
///     适用于 CompileMode = FullTimeline 或 Hybrid 的 SkillData。
/// </summary>
public class TimelineSkillRunner
{
    // ---- 执行状态 ----
    private enum TimelineState
    {
        Idle,
        Running,
        Paused,
        WaitingForDynamic, // 等待外部处理动态节点
        Completed,
        Interrupted
    }

    // ---- 运行时数据 ----
    private SkillData _skillData;
    private SkillContext _context;
    private TimelineState _state = TimelineState.Idle;

    private float _elapsedTime;
    private int _lastExecutedStepIndex = -1;
    private bool _debugEnabled;

    // ---- 动态步骤回退 ----
    private SkillStep _pendingDynamicStep;
    private float _resumeTimeAfterDynamic;

    // ---- 回调 ----
    public event Action OnCompleted;
    public event Action OnInterrupted;
    public event Action<string> OnStepTriggered; // stepId

    /// <summary>
    ///     遇到动态步骤时触发。
    ///     参数：动态步骤本身、恢复时间（处理完动态段后应从该时间继续）。
    ///     外部处理完毕后调用 ResumeAfterDynamic() 继续时间轴。
    /// </summary>
    public event Action<SkillStep, float> OnDynamicStep;

    // ---- 公开属性 ----
    public bool IsRunning => _state == TimelineState.Running || _state == TimelineState.WaitingForDynamic;
    public bool IsCompleted => _state == TimelineState.Completed;
    public bool IsInterrupted => _state == TimelineState.Interrupted;
    public float ElapsedTime => _elapsedTime;
    public float TotalDuration => _skillData?.TotalDuration ?? 0f;
    public float Progress => TotalDuration > 0f ? Mathf.Clamp01(_elapsedTime / TotalDuration) : 0f;
    public SkillData CurrentSkillData => _skillData;

    // ============================================================
    //  生命周期
    // ============================================================

    /// <summary>
    ///     启动时间轴执行。
    /// </summary>
    public void Start(SkillData skillData, SkillContext context)
    {
        if (skillData == null)
        {
            Debug.LogError("[TimelineSkillRunner] SkillData is null");
            return;
        }

        if (!skillData.Validate(out var error))
        {
            Debug.LogError($"[TimelineSkillRunner] SkillData validation failed: {error}");
            return;
        }

        _skillData = skillData;
        _context = context;
        _elapsedTime = 0f;
        _lastExecutedStepIndex = -1;
        _pendingDynamicStep = null;
        _resumeTimeAfterDynamic = 0f;
        _debugEnabled = context?.DebugEnabled ?? false;
        _state = TimelineState.Running;

        if (_debugEnabled)
        {
            Debug.Log($"[TimelineSkillRunner] Started: {skillData.SkillName}, Mode={skillData.CompileMode}, " +
                      $"Steps={skillData.Steps.Count}, Duration={skillData.TotalDuration:F2}s");
        }

        // 立即执行 t=0 的所有步骤
        Tick(0f);
    }

    /// <summary>
    ///     每帧 Tick 推进时间轴。
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (_state != TimelineState.Running) return;
        if (_skillData == null || _context == null) return;

        // 检查中断
        if (_context.IsInterrupted)
        {
            Interrupt();
            return;
        }

        _elapsedTime += deltaTime;

        // 推进步骤
        while (true)
        {
            var nextIndex = _skillData.GetNextStepIndex(_elapsedTime, _lastExecutedStepIndex);
            if (nextIndex < 0) break;

            var step = _skillData.Steps[nextIndex];
            _lastExecutedStepIndex = nextIndex;

            if (step.IsDynamic)
            {
                // 遇到动态步骤 —— 暂停时间轴，通知外部处理
                EnterDynamicMode(step);
                return; // 暂停 Tick，等待外部 ResumeAfterDynamic
            }

            ExecuteStep(step);
        }

        // 检查完成
        if (_elapsedTime >= _skillData.TotalDuration && _lastExecutedStepIndex >= _skillData.Steps.Count - 1)
        {
            Complete();
        }
    }

    /// <summary>
    ///     暂停时间轴（调试用）。
    /// </summary>
    public void Pause()
    {
        if (_state == TimelineState.Running)
            _state = TimelineState.Paused;
    }

    /// <summary>
    ///     继续时间轴（调试用）。
    /// </summary>
    public void Resume()
    {
        if (_state == TimelineState.Paused)
            _state = TimelineState.Running;
    }

    /// <summary>
    ///     外部处理完动态节点后调用，继续时间轴。
    /// </summary>
    public void ResumeAfterDynamic()
    {
        if (_state != TimelineState.WaitingForDynamic)
        {
            Debug.LogWarning("[TimelineSkillRunner] ResumeAfterDynamic called but not in WaitingForDynamic state");
            return;
        }

        _elapsedTime = _resumeTimeAfterDynamic;
        _pendingDynamicStep = null;
        _state = TimelineState.Running;

        if (_debugEnabled)
            Debug.Log($"[TimelineSkillRunner] Resumed after dynamic step at t={_elapsedTime:F2}s");

        // 立即检查后续步骤
        Tick(0f);
    }

    /// <summary>
    ///     中断执行。
    /// </summary>
    public void Interrupt()
    {
        if (_state == TimelineState.Idle || _state == TimelineState.Completed || _state == TimelineState.Interrupted)
            return;

        _state = TimelineState.Interrupted;

        if (_debugEnabled)
            Debug.Log($"[TimelineSkillRunner] Interrupted at t={_elapsedTime:F2}s");

        OnInterrupted?.Invoke();
    }

    /// <summary>
    ///     重置执行器。
    /// </summary>
    public void Reset()
    {
        _skillData = null;
        _context = null;
        _state = TimelineState.Idle;
        _elapsedTime = 0f;
        _lastExecutedStepIndex = -1;
        _pendingDynamicStep = null;
        _resumeTimeAfterDynamic = 0f;
    }

    // ============================================================
    //  步骤执行
    // ============================================================

    private void ExecuteStep(SkillStep step)
    {
        if (step?.Effects == null) return;

        OnStepTriggered?.Invoke(step.StepId);

        if (_debugEnabled)
            Debug.Log($"[TimelineSkillRunner] Step [{step.TriggerTime:F2}s] {step.Description} — {step.Effects.Count} effects");

        foreach (var effect in step.Effects)
        {
            ExecuteEffect(effect, step);
        }
    }

    private void ExecuteEffect(SkillEffectData effect, SkillStep step)
    {
        switch (effect.EffectType)
        {
            case SkillEffectType.Damage:
            case SkillEffectType.Heal:
            case SkillEffectType.ApplyBuff:
            case SkillEffectType.RemoveBuff:
            case SkillEffectType.ModifyAttribute:
                // 战斗效果 → 通过 EffectSystem 统一入口
                EffectSystemDispatcher.Apply(effect, _context);
                break;

            case SkillEffectType.PlayVFX:
            case SkillEffectType.PlaySFX:
            case SkillEffectType.SpawnProjectile:
            case SkillEffectType.ShakeCamera:
                // 表现效果 → 通过 PresentationDispatcher
                PresentationDispatcher.Apply(effect, _context);
                break;

            case SkillEffectType.PlayAnimation:
                // 动画效果
                AnimationDispatcher.Apply(effect, _context);
                break;

            case SkillEffectType.SetBlackboard:
                // 黑板操作
                if (_context?.Blackboard != null && !string.IsNullOrEmpty(effect.BlackboardKey))
                {
                    _context.Blackboard.SetValue(effect.BlackboardKey, effect.BlackboardValue);
                }
                break;

            case SkillEffectType.EQSQuery:
                // EQS 查询
                EQSQueryDispatcher.Apply(effect, _context);
                break;

            case SkillEffectType.PaintTerrain:
                // 地形效果
                TerrainDispatcher.Apply(effect, _context);
                break;

            case SkillEffectType.Custom:
                // 自定义扩展
                CustomEffectDispatcher.Apply(effect, _context);
                break;

            case SkillEffectType.None:
            default:
                if (_debugEnabled)
                    Debug.LogWarning($"[TimelineSkillRunner] Unknown effect type: {effect.EffectType}");
                break;
        }
    }

    // ============================================================
    //  动态模式
    // ============================================================

    private void EnterDynamicMode(SkillStep step)
    {
        _state = TimelineState.WaitingForDynamic;
        _pendingDynamicStep = step;
        _resumeTimeAfterDynamic = _elapsedTime;

        if (_debugEnabled)
            Debug.Log($"[TimelineSkillRunner] Entering dynamic mode at t={_elapsedTime:F2}s, step={step.StepId}");

        OnDynamicStep?.Invoke(step, _resumeTimeAfterDynamic);
    }

    private void Complete()
    {
        _state = TimelineState.Completed;

        if (_debugEnabled)
            Debug.Log($"[TimelineSkillRunner] Completed: {_skillData.SkillName}");

        OnCompleted?.Invoke();
    }
}

// ============================================================
//  Effect 分发器 —— 将 SkillEffectData 映射到现有子系统
// ============================================================

/// <summary>
///     战斗效果分发器 —— 映射到 GAS / DamagePipeline。
/// </summary>
public static class EffectSystemDispatcher
{
    public static void Apply(SkillEffectData effect, SkillContext ctx)
    {
        var target = ResolveTarget(effect.TargetMode, ctx);
        if (target == null && effect.TargetMode != SkillEffectTargetMode.Caster) return;

        // 构建 EffectContext（复用现有 GAS 上下文）
        var effectCtx = EffectContext.Create(
            ctx.Caster,
            target,
            target != null ? target.position : ctx.Caster.position,
            abilityLevel: effect.AbilityLevel > 0 ? effect.AbilityLevel : 1,
            sourceSkillId: ctx.SkillID
        );

        switch (effect.EffectType)
        {
            case SkillEffectType.Damage:
                DamagePipeline.CalculateAndApply(
                    effect.BaseValue * effect.ValueMultiplier,
                    target,
                    ctx.Caster,
                    effect.TagsToApply?.ToArray());
                break;

            case SkillEffectType.Heal:
                var health = target?.GetComponent<HealthComponent>();
                health?.Heal(effect.BaseValue * effect.ValueMultiplier);
                break;

            case SkillEffectType.ApplyBuff:
                if (!string.IsNullOrEmpty(effect.BuffKey) && int.TryParse(effect.BuffKey, out var buffId))
                {
                    // 从配置加载 GE 数据（BuffKey 存储为 EffectId 字符串）
                    var geData = ConfigLoader.GetGameplayEffectData(buffId);
                    if (geData != null)
                    {
                        EffectSystem.ApplyEffect(effectCtx, geData);
                    }
                }
                break;

            case SkillEffectType.RemoveBuff:
                var host = target?.GetComponent<GEHost>();
                if (host != null && effect.TagsToRemove != null)
                {
                    foreach (var tag in effect.TagsToRemove)
                        host.RemoveInnateTag(tag);
                }
                break;

            case SkillEffectType.ModifyAttribute:
                // 通过 GE 系统应用属性修改
                var attrData = new GameplayEffectData
                {
                    EffectName = $"AttrMod_{effect.BlackboardKey}",
                    Duration = effect.Duration,
                    DurationPolicy = effect.Duration > 0 ? GEDurationPolicy.HasDuration : GEDurationPolicy.Instant
                };
                EffectSystem.ApplyEffect(effectCtx, attrData);
                break;
        }
    }

    private static Transform ResolveTarget(SkillEffectTargetMode mode, SkillContext ctx)
    {
        return mode switch
        {
            SkillEffectTargetMode.Caster or SkillEffectTargetMode.Self => ctx.Caster,
            SkillEffectTargetMode.PrimaryTarget => ctx.Target,
            _ => ctx.Target
        };
    }
}

/// <summary>
///     表现效果分发器 —— 映射到 VFXManager / Projectile。
/// </summary>
public static class PresentationDispatcher
{
    public static void Apply(SkillEffectData effect, SkillContext ctx)
    {
        switch (effect.EffectType)
        {
            case SkillEffectType.PlayVFX:
                if (!string.IsNullOrEmpty(effect.VFXKey))
                {
                    var vfxTarget = ResolveTransform(effect.TargetMode, ctx);
                    var request = new VFXRequest
                    {
                        VFXKey = effect.VFXKey,
                        StyleKey = effect.StyleKey,
                        Position = vfxTarget != null ? vfxTarget.position + effect.VFXOffset : ctx.Caster.position + effect.VFXOffset,
                        Direction = effect.VFXDirection,
                        Parent = effect.AttachToTarget ? vfxTarget : null,
                        ScaleMultiplier = effect.VFXScale,
                        WidthMultiplier = effect.VFXWidthMultiplier,
                        Length = effect.VFXLength,
                        Duration = effect.Duration,
                        Intensity = effect.BaseValue
                    };
                    VFXManager.EnsureInstance()?.Play(request);
                }
                break;

            case SkillEffectType.SpawnProjectile:
                // 投射物发射逻辑（复用现有 Projectile 系统）
                // 实际实现需根据 ProjectileKey 从对象池获取
                break;

            case SkillEffectType.ShakeCamera:
                // 相机震动（需接入相机系统）
                break;
        }
    }

    private static Transform ResolveTransform(SkillEffectTargetMode mode, SkillContext ctx)
    {
        return mode switch
        {
            SkillEffectTargetMode.Caster or SkillEffectTargetMode.Self => ctx.Caster,
            SkillEffectTargetMode.PrimaryTarget => ctx.Target,
            _ => ctx.Target
        };
    }
}

/// <summary>
///     动画效果分发器。
/// </summary>
public static class AnimationDispatcher
{
    public static void Apply(SkillEffectData effect, SkillContext ctx)
    {
        if (ctx?.Caster == null) return;

        var animator = ctx.Caster.GetComponent<Animator>();
        if (animator == null) return;

        if (!string.IsNullOrEmpty(effect.AnimationTrigger))
        {
            animator.SetTrigger(effect.AnimationTrigger);
        }
        else if (!string.IsNullOrEmpty(effect.AnimationState))
        {
            animator.CrossFade(effect.AnimationState, effect.AnimationCrossFade);
        }
    }
}

/// <summary>
///     EQS 查询分发器。
/// </summary>
public static class EQSQueryDispatcher
{
    public static void Apply(SkillEffectData effect, SkillContext ctx)
    {
        if (string.IsNullOrEmpty(effect.EQSQueryKey)) return;

        // 从配置加载 EQS 查询配置并执行
        // 结果可通过黑板传递
    }
}

/// <summary>
///     地形效果分发器。
/// </summary>
public static class TerrainDispatcher
{
    public static void Apply(SkillEffectData effect, SkillContext ctx)
    {
        // 接入 TerrainEffectSystem
    }
}

/// <summary>
///     自定义效果扩展点。
/// </summary>
public static class CustomEffectDispatcher
{
    public static event Action<SkillEffectData, SkillContext> OnCustomEffect;

    public static void Apply(SkillEffectData effect, SkillContext ctx)
    {
        OnCustomEffect?.Invoke(effect, ctx);
    }
}
