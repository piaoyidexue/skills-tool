using System.Collections;
using UnityEngine;

/// <summary>
///     施法前腰节点 —— 播放前腰 VFX，中断检查。
///     【重要】前腰的等待计时由 SkillCaster.CastPipeline 统一管理，
///     本节点仅负责 VFX 播放与中断标志传播，不独立等待。
/// </summary>
public class PreCastNode : SkillNode
{
    public FloatBinding castTime = new()
    {
        Source = FloatBinding.SourceType.SkillConfig,
        SkillField = SkillFloatField.CastTime
    };

    public StringBinding preCastVfxKey = new()
    {
        Source = StringBinding.SourceType.Literal,
        LiteralValue = string.Empty
    };

    public bool continueOnInterrupt;

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        var duration = castTime.Resolve(ctx);

        // 播放前腰 VFX
        var vfxKey = preCastVfxKey.Resolve(ctx);
        if (!string.IsNullOrWhiteSpace(vfxKey) && duration > 0f)
        {
            var manager = VFXManager.EnsureInstance();
            if (manager != null && ctx.Caster != null)
            {
                manager.Play(new VFXRequest
                {
                    VFXKey = vfxKey,
                    StyleKey = ctx.Config?.VFXProfileKey,
                    Position = ctx.Caster.position,
                    Parent = ctx.Caster,
                    Duration = duration
                });
            }
        }

        if (ctx.IsInterrupted)
        {
            ExecuteInterruptFallback(ctx);
            return continueOnInterrupt ? NodeTickResult.Success : NodeTickResult.Failure;
        }

        return NodeTickResult.Success;
    }

    public override IEnumerator Execute(SkillContext ctx)
    {
        var duration = castTime.Resolve(ctx);

        var vfxKey = preCastVfxKey.Resolve(ctx);
        if (!string.IsNullOrWhiteSpace(vfxKey) && duration > 0f)
        {
            var manager = VFXManager.EnsureInstance();
            if (manager != null && ctx.Caster != null)
            {
                manager.Play(new VFXRequest
                {
                    VFXKey = vfxKey,
                    StyleKey = ctx.Config?.VFXProfileKey,
                    Position = ctx.Caster.position,
                    Parent = ctx.Caster,
                    Duration = duration
                });
            }
        }

        if (ctx.IsInterrupted)
        {
            ExecuteInterruptFallback(ctx);
            yield break;
        }

        yield break;
    }

    public override SkillNode ResolveNextNode(SkillContext ctx)
    {
        if (ctx.IsInterrupted && !continueOnInterrupt)
            return null;

        return base.ResolveNextNode(ctx);
    }

    private void ExecuteInterruptFallback(SkillContext ctx)
    {
        Debug.Log($"[PreCastNode] Interrupted during pre-cast for skill {ctx.SkillID}");
    }
}
