using System.Collections;
using UnityEngine;

/// <summary>
///     终结技二段表现节点 —— 先吸能（EnergyAbsorb），再爆发（Finisher）。
///     Stage 1: 播放吸能特效，能量从周围向目标中心塌缩。
///     Stage 2: 吸能完成后立即播放终结爆发特效。
///     用于"元素坍缩"、"晶廷裁决"、"雷断头台"等需要先蓄后爆的终结技。
/// </summary>
public class FinisherStagedNode : SkillNode
{
    public enum StagedDirectionMode
    {
        CasterToTarget,
        TargetForward,
        CustomDirection
    }

    public enum TransformBinding
    {
        World,
        Target,
        Caster
    }

    /// <summary>吸能阶段特效 Key（留空则用 SkillConfig.FinisherVFXKey 推导前缀 "_Absorb"）</summary>
    public StringBinding absorbVfxKey = new()
    {
        Source = StringBinding.SourceType.Literal,
        LiteralValue = "EnergyAbsorb"
    };

    /// <summary>爆发阶段特效 Key（留空则用 SkillConfig.FinisherVFXKey）</summary>
    public StringBinding burstVfxKey = new()
    {
        Source = StringBinding.SourceType.SkillConfigField,
        SkillConfigFieldName = nameof(SkillConfig.FinisherVFXKey)
    };

    /// <summary>吸能阶段时长覆盖</summary>
    public FloatBinding absorbDuration = new()
    {
        Source = FloatBinding.SourceType.Literal,
        LiteralValue = 0.55f
    };

    /// <summary>爆发阶段时长覆盖</summary>
    public FloatBinding burstDuration = new()
    {
        Source = FloatBinding.SourceType.SkillConfig,
        SkillField = SkillFloatField.VFXDuration
    };

    public FloatBinding scaleMultiplier = new()
    {
        Source = FloatBinding.SourceType.Literal,
        LiteralValue = 1f
    };

    public StagedDirectionMode directionMode = StagedDirectionMode.CasterToTarget;
    public Vector3 customDirection = Vector3.forward;
    public TransformBinding parentBinding = TransformBinding.Target;

    public override IEnumerator Execute(SkillContext ctx)
    {
        var manager = VFXManager.EnsureInstance();
        if (manager == null || ctx == null) yield break;

        var anchor = ResolveAnchor(ctx);
        var direction = ResolveDirection(ctx);
        var parent = ResolveParent(ctx);

        // --- Stage 1: Energy Absorb ---
        var absorbKey = absorbVfxKey.Resolve(ctx);
        if (!string.IsNullOrWhiteSpace(absorbKey))
        {
            var absorbRequest = new VFXRequest
            {
                VFXKey = absorbKey,
                StyleKey = ctx.Config?.VFXProfileKey,
                Position = anchor.position,
                Direction = direction,
                Parent = parent,
                ScaleMultiplier = scaleMultiplier.Resolve(ctx) * 1.35f,
                Duration = absorbDuration.Resolve(ctx),
                Intensity = 1.2f
            };

            manager.Play(absorbRequest);

            // wait for absorb to finish
            var waitTime = absorbRequest.Duration > 0f ? absorbRequest.Duration : 0.55f;
            yield return new WaitForSeconds(waitTime);
        }

        // --- Stage 2: Burst ---
        var burstKey = burstVfxKey.Resolve(ctx);
        if (!string.IsNullOrWhiteSpace(burstKey))
        {
            var burstRequest = new VFXRequest
            {
                VFXKey = burstKey,
                StyleKey = ctx.Config?.VFXProfileKey,
                Position = anchor.position,
                Direction = direction,
                Parent = parent,
                ScaleMultiplier = scaleMultiplier.Resolve(ctx) * 1.5f,
                Duration = burstDuration.Resolve(ctx),
                Intensity = 1.35f,
                Length = ctx.Config?.Radius ?? 4f
            };

            manager.Play(burstRequest);
        }

        yield break;
    }

    private Transform ResolveAnchor(SkillContext ctx)
    {
        switch (parentBinding)
        {
            case TransformBinding.Target:
                return ctx.Target != null ? ctx.Target : ctx.Caster;
            case TransformBinding.Caster:
                return ctx.Caster;
            default:
                return ctx.Caster != null ? ctx.Caster : ctx.Target;
        }
    }

    private Vector3 ResolveDirection(SkillContext ctx)
    {
        switch (directionMode)
        {
            case StagedDirectionMode.TargetForward:
                return ctx.Target != null ? ctx.Target.forward : Vector3.forward;
            case StagedDirectionMode.CustomDirection:
                return customDirection.normalized;
            case StagedDirectionMode.CasterToTarget:
            default:
                if (ctx.Caster != null && ctx.Target != null)
                    return (ctx.Target.position - ctx.Caster.position).normalized;
                return Vector3.forward;
        }
    }

    private Transform ResolveParent(SkillContext ctx)
    {
        switch (parentBinding)
        {
            case TransformBinding.Target:
                return ctx.Target;
            case TransformBinding.Caster:
                return ctx.Caster;
            default:
                return null;
        }
    }
}
