using System.Collections;
using UnityEngine;

public class PlayVFXNode : SkillNode
{
    public enum VFXStage
    {
        Auto,
        Cast,
        Impact,
        Beam,
        Reaction,
        Terrain,
        Finisher
    }

    public enum DirectionMode
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

    public StringBinding vfxKey = new()
    {
        Source = StringBinding.SourceType.SkillConfigField,
        SkillConfigFieldName = nameof(SkillConfig.ImpactVFXKey)
    };

    public StringBinding styleKey = new()
    {
        Source = StringBinding.SourceType.SkillConfigField,
        SkillConfigFieldName = nameof(SkillConfig.VFXProfileKey)
    };

    public VFXStage stage = VFXStage.Auto;
    public DirectionMode directionMode = DirectionMode.CasterToTarget;
    public TransformBinding parentBinding = TransformBinding.World;
    public FloatBinding scaleMultiplier = new() { Source = FloatBinding.SourceType.Literal, LiteralValue = 1f };
    public FloatBinding widthMultiplier = new() { Source = FloatBinding.SourceType.Literal, LiteralValue = 1f };
    public FloatBinding lengthOverride = new() { Source = FloatBinding.SourceType.Literal, LiteralValue = 0f };
    public FloatBinding durationOverride = new() { Source = FloatBinding.SourceType.SkillConfig, SkillField = SkillFloatField.VFXDuration };
    public FloatBinding intensityMultiplier = new() { Source = FloatBinding.SourceType.Literal, LiteralValue = 1f };
    public Vector3 customDirection = Vector3.forward;

    public override IEnumerator Execute(SkillContext ctx)
    {
        var manager = VFXManager.EnsureInstance();
        if (manager == null) yield break;

        var anchor = ctx.Target != null ? ctx.Target : ctx.Caster;
        if (anchor == null) yield break;

        var resolvedVfxKey = ResolveVfxKey(ctx);
        if (string.IsNullOrWhiteSpace(resolvedVfxKey)) yield break;

        var request = new VFXRequest
        {
            VFXKey = resolvedVfxKey,
            StyleKey = styleKey.Resolve(ctx),
            Position = anchor.position,
            Direction = ResolveDirection(ctx),
            Parent = ResolveParent(ctx),
            ScaleMultiplier = scaleMultiplier.Resolve(ctx),
            WidthMultiplier = widthMultiplier.Resolve(ctx),
            Length = lengthOverride.Resolve(ctx),
            Duration = durationOverride.Resolve(ctx),
            Intensity = intensityMultiplier.Resolve(ctx)
        };

        manager.Play(request);
        yield break;
    }

    private string ResolveVfxKey(SkillContext ctx)
    {
        if (ctx == null || ctx.Config == null || stage == VFXStage.Auto)
        {
            return vfxKey.Resolve(ctx);
        }

        switch (stage)
        {
            case VFXStage.Cast:
                return ctx.Config.CastVFXKey;
            case VFXStage.Impact:
                return ctx.Config.ImpactVFXKey;
            case VFXStage.Beam:
                return ctx.Config.BeamVFXKey;
            case VFXStage.Reaction:
                return ctx.Config.ReactionVFXKey;
            case VFXStage.Terrain:
                return ctx.Config.TerrainVFXKey;
            case VFXStage.Finisher:
                return ctx.Config.FinisherVFXKey;
            default:
                return vfxKey.Resolve(ctx);
        }
    }

    private Vector3 ResolveDirection(SkillContext ctx)
    {
        switch (directionMode)
        {
            case DirectionMode.TargetForward:
                return ctx.Target != null ? ctx.Target.forward : Vector3.forward;
            case DirectionMode.CustomDirection:
                return customDirection.normalized;
            case DirectionMode.CasterToTarget:
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
