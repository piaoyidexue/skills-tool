using System.Collections;
using UnityEngine;

/// <summary>
///     释放后摇节点 —— 播放后摇 VFX。
///     【重要】后摇的等待计时由 SkillCaster.CastPipeline 统一管理，
///     本节点仅负责 VFX 播放与状态标记，不独立等待。
/// </summary>
public class PostCastNode : SkillNode
{
    /// <summary>后摇时长（秒），留空从 SkillConfig.PostCastTime 读取（仅用于 VFX 持续时长推断）</summary>
    public FloatBinding postCastTime = new()
    {
        Source = FloatBinding.SourceType.SkillConfig,
        SkillField = SkillFloatField.PostCastTime
    };

    /// <summary>后摇期间播放的特效 Key</summary>
    public StringBinding postCastVfxKey = new()
    {
        Source = StringBinding.SourceType.Literal,
        LiteralValue = string.Empty
    };

    /// <summary>后摇期间是否允许移动（通常 false）</summary>
    public bool allowMoveDuringPostCast;

    public override IEnumerator Execute(SkillContext ctx)
    {
        var duration = postCastTime.Resolve(ctx);

        if (duration <= 0f)
        {
            yield break;
        }

        // 播放后摇 VFX（如果有）
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
