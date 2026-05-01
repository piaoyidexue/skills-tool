using System;

// ============================================================
//  AudioConfig —— 音频配置数据类
//  由 ConfigLoader 从 Audio.csv 解析而来。
//  数据驱动：运行时通过 audio_id 查找配置，
//  不在代码中硬编码音频资源路径。
// ============================================================

/// <summary>
///     音频分类枚举 —— 决定混音路由和并发策略。
/// </summary>
public enum AudioCategory
{
    /// <summary>背景音乐（唯一，支持淡入淡出切换）</summary>
    BGM = 0,

    /// <summary>游戏音效（战斗、环境等，受并发限制）</summary>
    SFX = 1,

    /// <summary>UI 音效（按钮点击等，不受并发限制）</summary>
    UI = 2,

    /// <summary>语音/对白（独占，播放时降低 SFX 音量）</summary>
    Voice = 3
}

/// <summary>
///     音频配置 —— 由 Audio.csv 驱动的纯数据类。
///     每条配置描述一个音频资源的元信息。
/// </summary>
[Serializable]
public class AudioConfig
{
    /// <summary>音频唯一 ID</summary>
    public int AudioId;

    /// <summary>音频资源路径（Resources 下的相对路径）</summary>
    public string ResourcePath;

    /// <summary>音频分类</summary>
    public AudioCategory Category;

    /// <summary>音量权重（0~1，与 AudioManager 分类主音量相乘）</summary>
    public float VolumeWeight = 1f;

    /// <summary>是否 3D 空间音效（需要位置信息）</summary>
    public bool Is3D;

    /// <summary>最大衰减距离（仅 Is3D=true 时有效）</summary>
    public float MaxDistance = 50f;

    /// <summary>是否循环（BGM 通常为 true）</summary>
    public bool Loop;

    /// <summary>同 ID 并发上限（0 = 使用全局默认值）</summary>
    public int MaxConcurrent;

    /// <summary>优先级（0=最高，数值越大优先级越低）</summary>
    public int Priority;

    /// <summary>淡入时间（秒，BGM 专用）</summary>
    public float FadeInDuration;

    /// <summary>淡出时间（秒，BGM 专用）</summary>
    public float FadeOutDuration;

    /// <summary>备注/调试描述</summary>
    public string Description;
}
