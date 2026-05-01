using UnityEngine;

// ============================================================
//  AudioDispatcher —— 技能时间轴的音频分发器
//  将 SkillEffectType.PlaySFX 映射到 AudioManager 的播放接口。
//  与 PresentationDispatcher 并列，遵循相同的分发器模式。
// ============================================================

/// <summary>
///     音频效果分发器 —— 映射到 AudioManager。
///     在 TimelineSkillRunner.ExecuteStep 中被调用，
///     处理 SkillEffectType.PlaySFX 类型的效果。
/// </summary>
public static class AudioDispatcher
{
    /// <summary>
    ///     应用音效效果。
    ///     从 SkillEffectData 读取 SFXKey（音频 ID）和位置信息，
    ///     分发给 AudioManager.PlaySFX 或 PlayUI。
    /// </summary>
    public static void Apply(SkillEffectData effect, SkillContext ctx)
    {
        if (string.IsNullOrEmpty(effect.SFXKey)) return;

        // SFXKey 可能是字符串形式的 audio_id
        if (!int.TryParse(effect.SFXKey, out var audioId)) return;

        var manager = AudioManager.Instance;
        if (manager == null) return;

        // 查询配置判断是否为 3D 音效
        var config = ConfigLoader.GetAudioConfig(audioId);

        if (config != null && config.Is3D)
        {
            // 3D 空间音效：根据 TargetMode 计算位置
            var position = ResolvePosition(effect, ctx);
            manager.PlaySFX(audioId, position);
        }
        else if (config != null && config.Category == AudioCategory.UI)
        {
            // UI 音效
            manager.PlayUI(audioId);
        }
        else
        {
            // 2D SFX
            manager.PlaySFX(audioId);
        }
    }

    /// <summary>
    ///     根据效果的目标模式解析播放位置。
    /// </summary>
    private static Vector3 ResolvePosition(SkillEffectData effect, SkillContext ctx)
    {
        var target = effect.TargetMode switch
        {
            SkillEffectTargetMode.Caster or SkillEffectTargetMode.Self => ctx?.Caster,
            SkillEffectTargetMode.PrimaryTarget => ctx?.Target,
            _ => ctx?.Target
        };

        if (target != null)
        {
            return target.position + effect.VFXOffset;
        }

        return ctx != null ? ctx.Caster.position + effect.VFXOffset : Vector3.zero;
    }
}
