using UnityEngine;

/// <summary>
///     终结技二段表现节点 —— 先吸能（EnergyAbsorb），再爆发（Finisher）。
///     Stage 1: 播放吸能特效，能量从周围向目标中心塌缩。
///     Stage 2: 吸能完成后立即播放终结爆发特效。
/// </summary>
public class FinisherStagedNode : SkillNode
{
    public enum StagedDirectionMode { CasterToTarget, TargetForward, CustomDirection }
    public enum TransformBinding { World, Target, Caster }

    public StringBinding absorbVfxKey = new()
    {
        Source = StringBinding.SourceType.Literal,
        LiteralValue = "EnergyAbsorb"
    };

    public StringBinding burstVfxKey = new()
    {
        Source = StringBinding.SourceType.SkillConfigField,
        SkillConfigFieldName = nameof(SkillConfig.FinisherVFXKey)
    };

    public FloatBinding absorbDuration = new()
    {
        Source = FloatBinding.SourceType.Literal,
        LiteralValue = 0.55f
    };

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

    // ---- Tick 状态 ----
    [System.NonSerialized] private int _stage; // 0=absorb, 1=burst
    [System.NonSerialized] private float _stageTimer;
    [System.NonSerialized] private float _absorbWaitTime;

    public override void OnEnter(SkillContext ctx)
    {
        _stage = 0;
        _stageTimer = 0f;
        _absorbWaitTime = absorbDuration.Resolve(ctx);
        if (_absorbWaitTime <= 0f) _absorbWaitTime = 0.55f;
    }

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        var manager = VFXManager.EnsureInstance();
        if (manager == null || ctx == null) return NodeTickResult.Success;

        var anchor = ResolveAnchor(ctx);
        var direction = ResolveDirection(ctx);
        var parent = ResolveParent(ctx);

        if (_stage == 0)
        {
            // Stage 1: Energy Absorb
            var absorbKey = absorbVfxKey.Resolve(ctx);
            if (!string.IsNullOrWhiteSpace(absorbKey) && _stageTimer == 0f)
            {
                var absorbRequest = new VFXRequest
                {
                    VFXKey = absorbKey,
                    StyleKey = ctx.Config?.VFXProfileKey,
                    Position = anchor.position,
                    Direction = direction,
                    Parent = parent,
                    ScaleMultiplier = scaleMultiplier.Resolve(ctx) * 1.35f,
                    Duration = _absorbWaitTime,
                    Intensity = 1.2f
                };
                manager.Play(absorbRequest);
            }

            _stageTimer += deltaTime;
            if (_stageTimer >= _absorbWaitTime)
            {
                _stage = 1;
                _stageTimer = 0f;
            }
            return NodeTickResult.Running;
        }

        // Stage 2: Burst
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

        return NodeTickResult.Success;
    }

    private Transform ResolveAnchor(SkillContext ctx)
    {
        switch (parentBinding)
        {
            case TransformBinding.Target: return ctx.Target != null ? ctx.Target : ctx.Caster;
            case TransformBinding.Caster: return ctx.Caster;
            default: return ctx.Caster != null ? ctx.Caster : ctx.Target;
        }
    }

    private Vector3 ResolveDirection(SkillContext ctx)
    {
        switch (directionMode)
        {
            case StagedDirectionMode.TargetForward: return ctx.Target != null ? ctx.Target.forward : Vector3.forward;
            case StagedDirectionMode.CustomDirection: return customDirection.normalized;
            default: return ctx.Caster != null && ctx.Target != null ? (ctx.Target.position - ctx.Caster.position).normalized : Vector3.forward;
        }
    }

    private Transform ResolveParent(SkillContext ctx)
    {
        switch (parentBinding)
        {
            case TransformBinding.Target: return ctx.Target;
            case TransformBinding.Caster: return ctx.Caster;
            default: return null;
        }
    }
}
