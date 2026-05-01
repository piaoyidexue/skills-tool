using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

// ============================================================
//  AudioManager —— 全局音频管理器
//  CSV 配置驱动 + 对象池复用 + 同频剔除（Voice Stealing）
//
//  设计准则：
//  - 数据驱动：音频资源路径和参数从 Audio.csv 读取
//  - 对象池：AudioSource 按需创建/回收，避免频繁 Instantiate/Destroy
//  - 同频剔除：同帧同 ID 音效超过阈值时拒绝或顶替最老请求
//  - 模块解耦：通过 AudioDispatcher 与技能系统对接
// ============================================================

/// <summary>
///     音频播放请求 —— 描述一次播放的完整参数。
/// </summary>
public struct AudioPlayRequest
{
    /// <summary>音频配置 ID（Audio.csv 中的 audio_id）</summary>
    public int AudioId;

    /// <summary>3D 播放位置（Is3D=true 时有效）</summary>
    public Vector3 Position;

    /// <summary>音量倍率覆盖（<=0 使用配置默认值）</summary>
    public float VolumeOverride;

    /// <summary>播放完成后回调</summary>
    public Action OnComplete;
}

/// <summary>
///     全局音频管理器 —— 单例。
///     提供 BGM（淡入淡出切换）、3D SFX（位置音效）、UI 音效三类核心 API。
///     内置 AudioSource 对象池和同频剔除逻辑。
/// </summary>
public class AudioManager : MonoBehaviour
{
    // ──────────── 单例 ────────────

    public static AudioManager Instance { get; private set; }

    // ──────────── 配置 ────────────

    [Header("=== 对象池 ===")]
    [Tooltip("初始 AudioSource 池大小")]
    [SerializeField] private int _poolSize = 8;

    [Tooltip("池最大容量")]
    [SerializeField] private int _poolMaxSize = 32;

    [Header("=== 并发控制 ===")]
    [Tooltip("同 ID SFX 每帧并发上限默认值（0=CSV 配置优先）")]
    [SerializeField] private int _defaultMaxConcurrent = 4;

    [Header("=== 分类音量 ===")]
    [Tooltip("BGM 主音量")]
    [SerializeField] private float _bgmVolume = 0.8f;

    [Tooltip("SFX 主音量")]
    [SerializeField] private float _sfxVolume = 1.0f;

    [Tooltip("UI 音量")]
    [SerializeField] private float _uiVolume = 0.7f;

    [Header("=== 调试 ===")]
    [SerializeField] private bool _showDebugInfo;

    // ──────────── 运行时数据 ────────────

    /// <summary>AudioSource 对象池</summary>
    private ObjectPool<AudioSource> _sourcePool;

    /// <summary>当前活跃的 AudioSource 列表（播放中）</summary>
    private readonly List<ActiveAudioEntry> _activeSources = new(16);

    /// <summary>
    ///     同频剔除计数器。
    ///     Key: AudioId，Value: 本帧已播放次数。
    ///     每帧末尾清零。
    /// </summary>
    private readonly Dictionary<int, int> _frameConcurrentCount = new(32);

    /// <summary>BGM 专用 AudioSource</summary>
    private AudioSource _bgmSource;

    /// <summary>当前播放的 BGM 配置 ID</summary>
    private int _currentBgmId = -1;

    /// <summary>BGM 淡入淡出过渡状态</summary>
    private float _bgmFadeTimer;
    private float _bgmFadeDuration;
    private bool _bgmFadingOut;
    private int _pendingBgmId = -1;

    // ──────────── 活跃条目 ────────────

    private struct ActiveAudioEntry
    {
        public AudioSource Source;
        public int AudioId;
        public AudioCategory Category;
        public float RemainingTime;
        public Action OnComplete;
    }

    // ──────────── 生命周期 ────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializePool();
        EnsureBgmSource();
    }

    private void Update()
    {
        UpdateActiveSources();
        UpdateBgmFade();
    }

    private void LateUpdate()
    {
        // 每帧末尾清零并发计数
        _frameConcurrentCount.Clear();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ──────────── 对象池 ────────────

    private void InitializePool()
    {
        _sourcePool = new ObjectPool<AudioSource>(
            createFunc: () =>
            {
                var go = new GameObject("AudioSource_Pooled");
                go.transform.SetParent(transform, false);
                go.SetActive(false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 1f; // 默认 3D
                src.rolloffMode = AudioRolloffMode.Linear;
                return src;
            },
            actionOnGet: src =>
            {
                src.gameObject.SetActive(true);
                src.transform.SetParent(null, false);
            },
            actionOnRelease: src =>
            {
                src.Stop();
                src.clip = null;
                src.loop = false;
                src.transform.SetParent(transform, false);
                src.gameObject.SetActive(false);
            },
            actionOnDestroy: src => Destroy(src.gameObject),
            collectionCheck: false,
            defaultCapacity: _poolSize,
            maxSize: _poolMaxSize
        );
    }

    private void EnsureBgmSource()
    {
        if (_bgmSource != null) return;

        var go = new GameObject("BGM_Source");
        go.transform.SetParent(transform, false);
        _bgmSource = go.AddComponent<AudioSource>();
        _bgmSource.playOnAwake = false;
        _bgmSource.loop = true;
        _bgmSource.spatialBlend = 0f; // 2D
        _bgmSource.volume = 0f;
    }

    // ──────────── 核心 API：BGM ────────────

    /// <summary>
    ///     播放 BGM（支持淡入淡出切换）。
    ///     若已有 BGM 播放中，先淡出当前 BGM，再淡入新的。
    /// </summary>
    /// <param name="audioId">目标 BGM 的 AudioId</param>
    public void PlayBGM(int audioId)
    {
        if (audioId == _currentBgmId) return;

        var config = ConfigLoader.GetAudioConfig(audioId);
        if (config == null || config.Category != AudioCategory.BGM)
        {
            Debug.LogWarning($"[AudioManager] Invalid BGM audio ID: {audioId}");
            return;
        }

        if (_currentBgmId < 0 || _bgmSource == null || !_bgmSource.isPlaying)
        {
            // 直接播放
            StartBgmImmediate(config);
        }
        else
        {
            // 淡出当前 BGM → 淡入新 BGM
            _pendingBgmId = audioId;
            _bgmFadingOut = true;
            _bgmFadeDuration = config.FadeOutDuration > 0 ? config.FadeOutDuration : 1f;
            _bgmFadeTimer = 0f;
        }
    }

    /// <summary>
    ///     停止 BGM（带淡出）。
    /// </summary>
    public void StopBGM(float fadeOutDuration = 1f)
    {
        if (_currentBgmId < 0 || _bgmSource == null) return;

        _pendingBgmId = -1;
        _bgmFadingOut = true;
        _bgmFadeDuration = fadeOutDuration;
        _bgmFadeTimer = 0f;
    }

    private void StartBgmImmediate(AudioConfig config)
    {
        var clip = Resources.Load<AudioClip>(config.ResourcePath);
        if (clip == null)
        {
            Debug.LogWarning($"[AudioManager] BGM clip not found: {config.ResourcePath}");
            return;
        }

        _bgmSource.clip = clip;
        _bgmSource.volume = 0f;
        _bgmSource.Play();

        // 淡入
        _currentBgmId = config.AudioId;
        _bgmFadingOut = false;
        _bgmFadeDuration = config.FadeInDuration > 0 ? config.FadeInDuration : 2f;
        _bgmFadeTimer = 0f;
    }

    // ──────────── 核心 API：SFX ────────────

    /// <summary>
    ///     播放 3D 空间音效。
    ///     受同频剔除限制：同帧同 ID 超过阈值则拒绝新请求或顶替最老请求。
    /// </summary>
    /// <param name="audioId">音效 AudioId</param>
    /// <param name="position">3D 位置</param>
    /// <returns>是否成功播放</returns>
    public bool PlaySFX(int audioId, Vector3 position)
    {
        var request = new AudioPlayRequest
        {
            AudioId = audioId,
            Position = position
        };
        return PlaySFXInternal(request);
    }

    /// <summary>
    ///     播放 2D 音效（无位置信息）。
    /// </summary>
    public bool PlaySFX(int audioId)
    {
        var request = new AudioPlayRequest
        {
            AudioId = audioId
        };
        return PlaySFXInternal(request);
    }

    /// <summary>
    ///     通过完整请求播放音效。
    /// </summary>
    public bool PlaySFX(in AudioPlayRequest request)
    {
        return PlaySFXInternal(request);
    }

    private bool PlaySFXInternal(in AudioPlayRequest request)
    {
        var config = ConfigLoader.GetAudioConfig(request.AudioId);
        if (config == null)
        {
            Debug.LogWarning($"[AudioManager] Audio config not found: {request.AudioId}");
            return false;
        }

        // 同频剔除检查（仅 SFX 类别）
        if (config.Category == AudioCategory.SFX)
        {
            var maxConcurrent = config.MaxConcurrent > 0 ? config.MaxConcurrent : _defaultMaxConcurrent;
            if (!_frameConcurrentCount.TryGetValue(request.AudioId, out var count))
                count = 0;

            if (count >= maxConcurrent)
            {
                // Voice Stealing：顶替最老的同类请求
                if (!TryStealOldest(request.AudioId))
                {
                    return false; // 无法顶替，拒绝
                }
            }

            _frameConcurrentCount[request.AudioId] = count + 1;
        }

        // 从对象池获取 AudioSource
        var source = _sourcePool.Get();
        if (source == null) return false;

        // 异步加载 AudioClip
        LoadAndPlayAsync(source, config, request);
        return true;
    }

    // ──────────── 核心 API：UI 音 ────────────

    /// <summary>
    ///     播放 UI 音效（2D，不受并发限制）。
    /// </summary>
    public void PlayUI(int audioId)
    {
        var config = ConfigLoader.GetAudioConfig(audioId);
        if (config == null)
        {
            Debug.LogWarning($"[AudioManager] UI audio config not found: {audioId}");
            return;
        }

        var request = new AudioPlayRequest
        {
            AudioId = audioId
        };

        var source = _sourcePool.Get();
        if (source == null) return;

        LoadAndPlayAsync(source, config, request);
    }

    // ──────────── 异步加载 + 播放 ────────────

    private async void LoadAndPlayAsync(AudioSource source, AudioConfig config, AudioPlayRequest request)
    {
        var clip = await Resources.LoadAsync<AudioClip>(config.ResourcePath) as AudioClip;

        if (clip == null)
        {
            Debug.LogWarning($"[AudioManager] AudioClip not found: {config.ResourcePath}");
            _sourcePool.Release(source);
            return;
        }

        if (source == null || !source.gameObject.activeSelf)
        {
            _sourcePool.Release(source);
            return;
        }

        // 配置 AudioSource
        source.clip = clip;
        source.loop = config.Loop;

        // 音量 = 分类主音量 × 配置权重 × 请求倍率
        var categoryVolume = GetCategoryVolume(config.Category);
        var volumeWeight = config.VolumeWeight;
        var volumeOverride = request.VolumeOverride > 0 ? request.VolumeOverride : 1f;
        source.volume = categoryVolume * volumeWeight * volumeOverride;

        // 3D 设置
        if (config.Is3D)
        {
            source.spatialBlend = 1f;
            source.transform.position = request.Position;
            source.maxDistance = config.MaxDistance > 0 ? config.MaxDistance : 50f;
        }
        else
        {
            source.spatialBlend = 0f;
        }

        source.Play();

        // 加入活跃列表
        var duration = clip.length;
        var entry = new ActiveAudioEntry
        {
            Source = source,
            AudioId = config.AudioId,
            Category = config.Category,
            RemainingTime = duration,
            OnComplete = request.OnComplete
        };
        _activeSources.Add(entry);
    }

    // ──────────── 更新逻辑 ────────────

    private void UpdateActiveSources()
    {
        for (var i = _activeSources.Count - 1; i >= 0; i--)
        {
            var entry = _activeSources[i];
            entry.RemainingTime -= Time.deltaTime;

            if (entry.RemainingTime <= 0f || entry.Source == null || !entry.Source.isPlaying)
            {
                // 回收到对象池
                if (entry.Source != null)
                    _sourcePool.Release(entry.Source);

                entry.OnComplete?.Invoke();
                _activeSources.RemoveAt(i);
            }
            else
            {
                _activeSources[i] = entry;
            }
        }
    }

    private void UpdateBgmFade()
    {
        if (_currentBgmId < 0 || _bgmSource == null) return;
        if (_bgmFadeDuration <= 0f) return;

        _bgmFadeTimer += Time.deltaTime;
        var t = Mathf.Clamp01(_bgmFadeTimer / _bgmFadeDuration);

        if (_bgmFadingOut)
        {
            // 淡出
            var config = ConfigLoader.GetAudioConfig(_currentBgmId);
            var targetVolume = config != null ? _bgmVolume * config.VolumeWeight : _bgmVolume;
            _bgmSource.volume = targetVolume * (1f - t);

            if (t >= 1f)
            {
                _bgmSource.Stop();
                _bgmSource.clip = null;

                if (_pendingBgmId >= 0)
                {
                    // 淡出完成 → 淡入新的 BGM
                    var pendingConfig = ConfigLoader.GetAudioConfig(_pendingBgmId);
                    _pendingBgmId = -1;
                    if (pendingConfig != null)
                        StartBgmImmediate(pendingConfig);
                    else
                        _currentBgmId = -1;
                }
                else
                {
                    _currentBgmId = -1;
                }
            }
        }
        else
        {
            // 淡入
            var config = ConfigLoader.GetAudioConfig(_currentBgmId);
            var targetVolume = config != null ? _bgmVolume * config.VolumeWeight : _bgmVolume;
            _bgmSource.volume = targetVolume * t;

            if (t >= 1f)
            {
                _bgmFadeDuration = 0f; // 淡入完成
            }
        }
    }

    // ──────────── 同频剔除（Voice Stealing） ────────────

    /// <summary>
    ///     尝试顶替最老的同类音效。
    ///     找到活跃列表中同 AudioId 且最早播放的条目，停止并回收它。
    /// </summary>
    private bool TryStealOldest(int audioId)
    {
        for (var i = 0; i < _activeSources.Count; i++)
        {
            if (_activeSources[i].AudioId != audioId) continue;
            if (_activeSources[i].Category != AudioCategory.SFX) continue;

            // 找到最老的，停止它
            var old = _activeSources[i];
            if (old.Source != null) old.Source.Stop();
            _sourcePool.Release(old.Source);
            old.OnComplete?.Invoke();
            _activeSources.RemoveAt(i);
            return true;
        }

        return false;
    }

    // ──────────── 辅助 ────────────

    private float GetCategoryVolume(AudioCategory category)
    {
        return category switch
        {
            AudioCategory.BGM => _bgmVolume,
            AudioCategory.SFX => _sfxVolume,
            AudioCategory.UI => _uiVolume,
            AudioCategory.Voice => _sfxVolume,
            _ => 1f
        };
    }

    /// <summary>
    ///     设置分类主音量。
    /// </summary>
    public void SetCategoryVolume(AudioCategory category, float volume)
    {
        switch (category)
        {
            case AudioCategory.BGM: _bgmVolume = volume; break;
            case AudioCategory.SFX: _sfxVolume = volume; break;
            case AudioCategory.UI: _uiVolume = volume; break;
            case AudioCategory.Voice: break; // Voice 暂无独立音量
        }
    }

    /// <summary>
    ///     停止所有音效（BGM 除外）。
    /// </summary>
    public void StopAllSFX()
    {
        for (var i = _activeSources.Count - 1; i >= 0; i--)
        {
            var entry = _activeSources[i];
            if (entry.Source != null) _sourcePool.Release(entry.Source);
        }
        _activeSources.Clear();
    }

    /// <summary>
    ///     获取当前活跃音源数量。
    /// </summary>
    public int ActiveSourceCount => _activeSources.Count;

    // ──────────── 调试面板 ────────────

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (!_showDebugInfo) return;

        GUILayout.BeginArea(new Rect(730, 520, 350, 200));
        GUILayout.Label("<b>Audio Manager</b>", new GUIStyle(GUI.skin.label) { richText = true });
        GUILayout.Label($"  BGM: {_currentBgmId}  Active SFX: {_activeSources.Count}");
        GUILayout.Label($"  BGM Vol: {_bgmVolume:F2}  SFX Vol: {_sfxVolume:F2}  UI Vol: {_uiVolume:F2}");

        if (_frameConcurrentCount.Count > 0)
        {
            foreach (var kvp in _frameConcurrentCount)
                GUILayout.Label($"    ID:{kvp.Key} x{kvp.Value}");
        }

        GUILayout.EndArea();
    }
#endif
}
