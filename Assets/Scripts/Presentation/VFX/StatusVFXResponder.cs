using UnityEngine;

// ============================================================
//  StatusVFXResponder —— 表现层 Tag 变更监听组件
//  订阅 TagEventBus，当实体获得/失去指定 Tag 时自动播放/停止 VFX。
//  通过 VFXManager 统一管理 VFX 生命周期。
//  架构红线：仅监听 Tag 信号，不反向干预逻辑层。
// ============================================================

/// <summary>
///     状态特效响应器 —— 监听 TagEventBus，根据 Tag 变化自动播放 VFX。
///     挂载到需要响应状态变化的实体上（如角色、怪物）。
///     VFX 播放通过 VFXManager 统一管理，不直接操作对象池。
/// </summary>
public class StatusVFXResponder : MonoBehaviour
{
    [System.Serializable]
    public class TagVFXMapping
    {
        [Tooltip("监听的 Tag 名称（如 status.burn, status.chill）")]
        public string tag;

        [Tooltip("VFXManager 中注册的特效 Key（对应 Effect.csv 中的 effect_key）")]
        public string vfxKey;

        [Tooltip("特效持续时间（<=0 则使用 Effect.csv 中的配置值）")]
        public float duration;

        [Tooltip("特效挂载点（为空则挂载到实体根节点）")]
        public Transform attachPoint;

        [Tooltip("缩放倍率")]
        public float scaleMultiplier = 1f;
    }

    [Header("Tag → VFX 映射")]
    [Tooltip("Tag 与 VFX 的映射列表")]
    public TagVFXMapping[] mappings = System.Array.Empty<TagVFXMapping>();

    // ---- 运行时状态 ----
    private readonly System.Collections.Generic.Dictionary<string, TagVFXMapping> _mappingLookup
        = new(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>记录每个 Tag 最近一次播放的时间戳，防止重复触发</summary>
    private readonly System.Collections.Generic.Dictionary<string, float> _lastPlayTime
        = new(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>防重复触发的最短间隔（秒）</summary>
    private const float MinPlayInterval = 0.1f;

    private GEHost _host;

    private void Awake()
    {
        _host = GetComponent<GEHost>();

        // 构建查找表
        foreach (var mapping in mappings)
        {
            if (!string.IsNullOrEmpty(mapping.tag))
                _mappingLookup[mapping.tag] = mapping;
        }
    }

    private void OnEnable()
    {
        // 订阅每个映射的 Tag（精准订阅，比全局过滤更高效）
        foreach (var mapping in mappings)
        {
            if (!string.IsNullOrEmpty(mapping.tag))
                TagEventBus.Subscribe(mapping.tag, OnSpecificTagChanged);
        }
    }

    private void OnDisable()
    {
        foreach (var mapping in mappings)
        {
            if (!string.IsNullOrEmpty(mapping.tag))
                TagEventBus.Unsubscribe(mapping.tag, OnSpecificTagChanged);
        }
    }

    /// <summary>
    ///     按 Tag 订阅的回调，直接命中目标 Tag 变更。
    /// </summary>
    private void OnSpecificTagChanged(TagChangeEvent evt)
    {
        // 仅响应当前实体的事件
        if (evt.Host != _host) return;

        switch (evt.ChangeType)
        {
            case TagChangeType.Added:
                PlayVFXForTag(evt.Tag);
                break;
            case TagChangeType.Removed:
                // VFXManager 的 VFX 有固定 duration，到期自动回收，无需手动停止
                // 如果需要立即停止，可在此扩展
                break;
        }
    }

    private void PlayVFXForTag(string tag)
    {
        if (!_mappingLookup.TryGetValue(tag, out var mapping)) return;
        if (string.IsNullOrEmpty(mapping.vfxKey)) return;

        // 防重复触发
        var now = Time.time;
        if (_lastPlayTime.TryGetValue(tag, out var lastTime) && now - lastTime < MinPlayInterval)
            return;

        _lastPlayTime[tag] = now;

        var attachTarget = mapping.attachPoint != null ? mapping.attachPoint : transform;

        var request = new VFXRequest
        {
            VFXKey = mapping.vfxKey,
            Position = attachTarget.position,
            Parent = attachTarget,
            Duration = mapping.duration,
            ScaleMultiplier = mapping.scaleMultiplier
        };

        VFXManager.EnsureInstance()?.Play(request);
    }
}
