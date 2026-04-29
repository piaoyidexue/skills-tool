using System;
using System.Collections.Generic;

// ============================================================
//  GameplayTag 系统 —— 分层级标签，类似 Unreal GAS 的 FGameplayTag。
//  支持层级匹配："Tag.Status.Burn" 可被 "Tag.Status" 匹配。
// ============================================================

/// <summary>
///     单个 Gameplay Tag，使用点号分隔的层级命名规则。
///     不可变值类型，可安全用于 Job System。
/// </summary>
public readonly struct GameplayTag : IEquatable<GameplayTag>
{
    /// <summary>完整 Tag 字符串（小写），如 "tag.status.burn"</summary>
    public readonly string Value;

    /// <summary>层级数组（预分割，避免运行时拆分）</summary>
    private readonly int _hash;

    public static readonly GameplayTag None = default;

    public GameplayTag(string value)
    {
        Value = Normalize(value);
        _hash = Value.GetHashCode();
    }

    public bool IsValid => !string.IsNullOrEmpty(Value);

    /// <summary>当前 Tag 是否匹配（等于或子孙）指定的父级 Tag。</summary>
    /// <example>"tag.status.burn".Matches("tag.status") → true</example>
    public bool Matches(string parent)
    {
        if (string.IsNullOrEmpty(Value) || string.IsNullOrEmpty(parent)) return false;
        var normalized = Normalize(parent);
        return Value == normalized || Value.StartsWith(normalized + ".");
    }

    /// <summary>精确相等比较（区分大小写归一化后）</summary>
    public bool Equals(GameplayTag other) => Value == other.Value;
    public override bool Equals(object obj) => obj is GameplayTag other && Equals(other);
    public override int GetHashCode() => _hash;
    public override string ToString() => Value ?? "<None>";

    public static bool operator ==(GameplayTag a, GameplayTag b) => a.Value == b.Value;
    public static bool operator !=(GameplayTag a, GameplayTag b) => a.Value != b.Value;

    // ---- 隐式转换，兼容旧版 string tag API ----
    public static implicit operator string(GameplayTag tag) => tag.Value;
    public static implicit operator GameplayTag(string value) => new(value);

    private static string Normalize(string raw) => raw?.Trim().ToLowerInvariant() ?? string.Empty;
}

/// <summary>
///     Gameplay Tag 容器 —— 管理一组 Tag，支持层级匹配查询。
///     可作为值类型嵌入 GEHost 或独立使用。
/// </summary>
public struct GameplayTagContainer
{
    private HashSet<string> _tags;

    public int Count => _tags?.Count ?? 0;

    /// <summary>是否包含指定 Tag（精确匹配或层级匹配）。</summary>
    public readonly bool HasTag(string tag)
    {
        if (_tags == null || string.IsNullOrEmpty(tag)) return false;
        var normalized = GameplayTag.None;
        {
            var gt = new GameplayTag(tag);
            normalized = gt;
        }

        // 精确匹配
        if (_tags.Contains(normalized.Value)) return true;

        // 层级匹配：检查是否有子 Tag 包含此父级前缀
        foreach (var existing in _tags)
        {
            if (existing.StartsWith(normalized.Value + "."))
                return true;
        }

        return false;
    }

    /// <summary>精确匹配（不检查层级）。</summary>
    public readonly bool HasExactTag(string tag)
    {
        if (_tags == null) return false;
        return _tags.Contains(new GameplayTag(tag).Value);
    }

    /// <summary>添加 Tag（重复添加无副作用）。</summary>
    public void AddTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return;
        _tags ??= new HashSet<string>();
        _tags.Add(new GameplayTag(tag).Value);
    }

    /// <summary>移除 Tag。</summary>
    public bool RemoveTag(string tag)
    {
        if (_tags == null) return false;
        return _tags.Remove(new GameplayTag(tag).Value);
    }

    /// <summary>清除所有 Tag。</summary>
    public void Clear() => _tags?.Clear();

    /// <summary>获取所有 Tag 的快照。</summary>
    public readonly IReadOnlyCollection<string> AllTags => _tags;

    /// <summary>合并另一个容器的 Tag。</summary>
    public void MergeFrom(GameplayTagContainer other)
    {
        if (other._tags == null) return;
        _tags ??= new HashSet<string>();
        foreach (var tag in other._tags)
            _tags.Add(tag);
    }
}

/// <summary>
///     GE 叠加策略。
/// </summary>
public enum GEStackPolicy
{
    /// <summary>刷新持续时间，不增加层数</summary>
    Refresh,
    /// <summary>增加一层，允许堆叠</summary>
    Add,
    /// <summary>忽略（已存在则不施加）</summary>
    Ignore
}
