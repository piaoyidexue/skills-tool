using System.Collections;
using UnityEngine;

/// <summary>
///     释放后摇节点 —— 播放后摇 VFX。
///     【重要】后摇的等待计时由 SkillCaster.CastPipeline 统一管理，
///     本节点仅负责 VFX 播放与状态标记，不独立等待。
/// </summary>
public class PostCastNode : SkillNode
{
    public FloatBinding postCastTime = new()
    {
        Source = FloatBinding.SourceType.SkillConfig,
        SkillField = SkillFloatField.PostCastTime
    };

    public StringBinding postCastVfxKey = new()
    {
        Source = StringBinding.SourceType.Literal,
        LiteralValue = string.Empty
    };

    public bool allowMoveDuringPostCast;

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        var duration = postCastTime.Resolve(ctx);

        if (duration <= 0f) return NodeTickResult.Success;

        var vfxKey = postCastVfxKey.Resolve(ctx);
        if (!string.IsNullOrWhiteSpace(vfxKey))
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

        return NodeTickResult.Success;
    }

    public override IEnumerator Execute(SkillContext ctx)
    {
        var duration = postCastTime.Resolve(ctx);

        if (duration <= 0f) yield break;

        var vfxKey = postCastVfxKey.Resolve(ctx);
        if (!string.IsNullOrWhiteSpace(vfxKey))
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

        yield break;
    }
}
