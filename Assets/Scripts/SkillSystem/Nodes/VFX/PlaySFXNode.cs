using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  PlaySFXNode —— 技能图音效播放节点
//  与 PlayVFXNode 并列，遵循相同的节点模式。
//  可被 SkillBuilder 编译为 SkillEffectData.PlaySFX 变体。
// ============================================================

/// <summary>
///     音效播放节点 —— 在技能时间轴上触发音效。
///     支持 2D/3D 音效、音量覆盖、延迟播放。
///     可编译：Compile() 输出 SkillEffectType.PlaySFX 的 SkillEffectData。
/// </summary>

[CreateAssetMenu(menuName = "VFX/Play SFX")]
public class PlaySFXNode : SkillNodeBase
{
    // ──────────── Inspector 字段 ────────────

    [Header("=== 音效配置 ===")]
    [Tooltip("音频配置 ID（对应 Audio.csv 中的 audio_id）")]
    public int AudioId = 2001;

    [Tooltip("音量倍率覆盖（<=0 使用配置默认值）")]
    public float VolumeOverride;

    [Tooltip("是否延迟播放（秒，0=立即播放）")]
    public float DelaySeconds;

    [Header("=== 3D 音效 ===")]
    [Tooltip("是否覆盖 CSV 中的 Is3D 设置")]
    public bool OverrideIs3D;

    [Tooltip("3D 音效时跟随目标（而非施法者）")]
    public bool FollowTarget = true;

    // ──────────── Tick 执行 ────────────

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        var manager = AudioManager.Instance;
        if (manager == null) return NodeTickResult.Success;

        var config = ConfigLoader.GetAudioConfig(AudioId);
        if (config == null)
        {
            Debug.LogWarning($"[PlaySFXNode] Audio config not found: {AudioId}");
            return NodeTickResult.Success;
        }

        var is3D = OverrideIs3D || config.Is3D;

        if (is3D)
        {
            var anchor = FollowTarget && ctx.Target != null ? ctx.Target : ctx.Caster;
            var position = anchor != null ? anchor.position : Vector3.zero;
            manager.PlaySFX(AudioId, position);
        }
        else if (config.Category == AudioCategory.UI)
        {
            manager.PlayUI(AudioId);
        }
        else
        {
            manager.PlaySFX(AudioId);
        }

        return NodeTickResult.Success;
    }

    // ──────────── 编译支持 ────────────

    public override bool CanCompile => true;

    public override List<SkillEffectData> Compile(SkillContext ctx = null)
    {
        var effect = new SkillEffectData
        {
            EffectType = SkillEffectType.PlaySFX,
            SFXKey = AudioId.ToString(),
            TargetMode = FollowTarget ? SkillEffectTargetMode.PrimaryTarget : SkillEffectTargetMode.Caster,
            Duration = DelaySeconds,
            BaseValue = VolumeOverride
        };

        return new List<SkillEffectData> { effect };
    }
}
