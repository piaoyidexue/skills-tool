using System.Collections;
using UnityEngine;

/// <summary>
///     施法前腰节点 —— 播放前腰 VFX，中断检查。
///     【重要】前腰的等待计时由 SkillCaster.CastPipeline 统一管理，
///     本节点仅负责 VFX 播放与中断标志传播，不独立等待。
/// </summary>
public class PreCastNode : SkillNode
{
    /// <summary>前腰时长（秒），留空从 SkillConfig.CastTime 读取（仅用于 VFX 持续时长推断）</summary>
    public FloatBinding castTime = new()
    {
        Source = FloatBinding.SourceType.SkillConfig,
        SkillField = SkillFloatField.CastTime
    };

    /// <summary>前腰期间播放的特效 Key</summary>
    public StringBinding preCastVfxKey = new()
    {
        Source = StringBinding.SourceType.Literal,
        LiteralValue = string.Empty
    };

    /// <summary>是否在被打断时仍然执行后继节点（通常为 false）</summary>
    public bool continueOnInterrupt;

    public override IEnumerator Execute(SkillContext ctx)
    {
        var duration = castTime.Resolve(ctx);

        // 播放前腰 VFX（如果配置）
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

        // 如果已被中断（CastPipeline 在 wait 期间设了标志），直接终止
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
