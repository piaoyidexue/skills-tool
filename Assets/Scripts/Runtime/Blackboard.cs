using System;
using System.Collections.Generic;
using UnityEngine;

public class Blackboard
{
    private readonly Dictionary<string, object> _data = new();

    public event Action<string, object> OnValueChanged;

    // ============================================================
    //  GAS 红线：禁止写入的废弃键集合。
    //  这些键对应的业务逻辑已迁移到 EffectSystem / GEHost / ReactionEngine。
    //  写入时将输出警告日志并静默忽略。
    // ============================================================
    private static readonly HashSet<string> DeprecatedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        BBKey.DamageOverride,
        BBKey.IsCrit,
        BBKey.LastDamage,
        BBKey.StatusTags,
        BBKey.ReactionSummary,
        BBKey.HasResonance,
        BBKey.DelayOverride,
        BBKey.DamagePercent,
        BBKey.CritChance
    };

    /// <summary>是否启用红线校验（编辑器下默认开启）</summary>
    public bool EnforceBoundaryChecks { get; set; } = true;

    public void SetValue<T>(string key, T value)
    {
        if (EnforceBoundaryChecks && DeprecatedKeys.Contains(key))
        {
            Debug.LogWarning($"[Blackboard] GAS红线：禁止写入废弃键 '{key}'，该逻辑已迁移到 EffectSystem。");
            return;
        }

        _data[key] = value;
        OnValueChanged?.Invoke(key, value);
    }

    public T GetValue<T>(string key, T defaultValue = default)
    {
        if (_data.TryGetValue(key, out var value))
        {
            if (value is T typedValue) return typedValue;

            try
            {
                if (value != null) return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
            }
        }

        return defaultValue;
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        return GetValue(key, defaultValue);
    }

    public float GetFloat(string key, float defaultValue = 0f)
    {
        return GetValue(key, defaultValue);
    }

    public string GetString(string key, string defaultValue = "")
    {
        return GetValue(key, defaultValue);
    }

    public bool TryGetValue<T>(string key, out T value)
    {
        if (_data.TryGetValue(key, out var obj))
        {
            if (obj is T typed)
            {
                value = typed;
                return true;
            }

            try
            {
                value = (T)Convert.ChangeType(obj, typeof(T));
                return true;
            }
            catch
            {
            }
        }

        value = default;
        return false;
    }

    public IReadOnlyDictionary<string, object> GetAllData()
    {
        return _data;
    }

    public Dictionary<string, string> GetSnapshotStrings()
    {
        var snapshot = new Dictionary<string, string>(_data.Count);
        foreach (var pair in _data) snapshot[pair.Key] = pair.Value != null ? pair.Value.ToString() : "<null>";

        return snapshot;
    }
}