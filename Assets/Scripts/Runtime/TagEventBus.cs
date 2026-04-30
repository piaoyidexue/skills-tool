using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  Tag 事件总线 (TagEventBus)
//  全局单例，聚合所有 GEHost 的 Tag 变更事件。
//  表现层（VFX/动画/UI）订阅此总线即可监听任何实体的 Tag 变化，
//  无需逐个绑定 GEHost 事件。
//
//  架构红线：总线仅传递 Tag 变更信号，不包含业务逻辑。
//  表现层禁止通过总线反向干预逻辑层。
// ============================================================

/// <summary>
///     Tag 变更事件参数。
/// </summary>
public struct TagChangeEvent
{
    /// <summary>发生变更的 GEHost 所属实体</summary>
    public GEHost Host;

    /// <summary>变更的 Tag 名称（小写归一化）</summary>
    public string Tag;

    /// <summary>变更类型：Added 或 Removed</summary>
    public TagChangeType ChangeType;
}

/// <summary>
///     Tag 变更类型。
/// </summary>
public enum TagChangeType
{
    Added,
    Removed
}

/// <summary>
///     全局 Tag 事件总线。
///     GEHost 在 OnTagAdded/OnTagRemoved 时自动发布到此处。
///     表现层订阅 OnTagChanged 即可响应任何实体的 Tag 变化。
/// </summary>
public static class TagEventBus
{
    /// <summary>
    ///     全局 Tag 变更事件。
    ///     参数为 TagChangeEvent 结构体，包含 Host/Tag/ChangeType。
    /// </summary>
    public static event Action<TagChangeEvent> OnTagChanged;

    /// <summary>
    ///     按 Tag 名称过滤的订阅器。
    ///     Key: 小写 Tag 名称，Value: 该 Tag 变更时的回调列表。
    /// </summary>
    private static readonly Dictionary<string, List<Action<TagChangeEvent>>> TagListeners
        = new(StringComparer.OrdinalIgnoreCase);

    // ---- 注册/注销 GEHost ----

    /// <summary>
    ///     注册 GEHost 到总线（在 GEHost.Awake 中自动调用）。
    ///     绑定 OnTagAdded/OnTagRemoved 到总线的发布方法。
    /// </summary>
    public static void Register(GEHost host)
    {
        if (host == null) return;
        host.OnTagAdded += PublishTagAdded;
        host.OnTagRemoved += PublishTagRemoved;
    }

    /// <summary>
    ///     注销 GEHost（在 GEHost.OnDestroy 中自动调用）。
    /// </summary>
    public static void Unregister(GEHost host)
    {
        if (host == null) return;
        host.OnTagAdded -= PublishTagAdded;
        host.OnTagRemoved -= PublishTagRemoved;
    }

    // ---- 按 Tag 订阅 ----

    /// <summary>
    ///     订阅指定 Tag 的变更事件。
    ///     当任何实体获得或失去此 Tag 时触发回调。
    /// </summary>
    public static void Subscribe(string tag, Action<TagChangeEvent> callback)
    {
        if (string.IsNullOrWhiteSpace(tag) || callback == null) return;
        var key = tag.Trim().ToLowerInvariant();
        if (!TagListeners.TryGetValue(key, out var list))
        {
            list = new List<Action<TagChangeEvent>>();
            TagListeners[key] = list;
        }
        if (!list.Contains(callback))
            list.Add(callback);
    }

    /// <summary>
    ///     取消订阅指定 Tag 的变更事件。
    /// </summary>
    public static void Unsubscribe(string tag, Action<TagChangeEvent> callback)
    {
        if (string.IsNullOrWhiteSpace(tag) || callback == null) return;
        var key = tag.Trim().ToLowerInvariant();
        if (TagListeners.TryGetValue(key, out var list))
            list.Remove(callback);
    }

    // ---- 发布 ----

    private static void PublishTagAdded(GEHost host, string tag)
    {
        var evt = new TagChangeEvent { Host = host, Tag = tag, ChangeType = TagChangeType.Added };
        OnTagChanged?.Invoke(evt);
        DispatchTagListeners(tag, evt);
    }

    private static void PublishTagRemoved(GEHost host, string tag)
    {
        var evt = new TagChangeEvent { Host = host, Tag = tag, ChangeType = TagChangeType.Removed };
        OnTagChanged?.Invoke(evt);
        DispatchTagListeners(tag, evt);
    }

    private static void DispatchTagListeners(string tag, TagChangeEvent evt)
    {
        if (!TagListeners.TryGetValue(tag, out var list)) return;
        for (var i = 0; i < list.Count; i++)
        {
            try { list[i]?.Invoke(evt); }
            catch (Exception ex) { Debug.LogException(ex); }
        }
    }

    /// <summary>
    ///     清除所有订阅（场景切换时调用）。
    /// </summary>
    public static void ClearAll()
    {
        OnTagChanged = null;
        TagListeners.Clear();
    }
}
