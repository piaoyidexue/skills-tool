using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     运行时黑板 —— 技能图节点的上下文共享层。
///     架构原则：
///     - 图定义逻辑形状，表定义数值大小，黑板提供运行时上下文
///     - 节点间禁止直接传参，通过黑板解耦输入输出
///     - EQSNode 写入 BBKey.TargetList，DamageNode 读取 BBKey.TargetList
///     - 黑板不存储数值流转变量（由 GAS 接管），不存储业务判定（由 EffectSystem 接管）
/// </summary>
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

    // ---- 数据流追踪（维度4 调试支持） ----

    /// <summary>是否启用写入追踪（调试用，记录每次写入的来源）</summary>
    public bool EnableWriteTracing { get; set; }

    /// <summary>写入追踪记录：Key → 最近一次写入的调用栈描述</summary>
    private readonly Dictionary<string, string> _writeTrace = new();

    public void SetValue<T>(string key, T value)
    {
        if (EnforceBoundaryChecks && DeprecatedKeys.Contains(key))
        {
            Debug.LogWarning($"[Blackboard] GAS红线：禁止写入废弃键 '{key}'，该逻辑已迁移到 EffectSystem。");
            return;
        }

        _data[key] = value;

        // 写入追踪
        if (EnableWriteTracing)
        {
            _writeTrace[key] = System.Environment.StackTrace;
        }

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

    // ============================================================
    //  维度4 增强：上下文级复用支持
    //  节点间数据流通过黑板解耦，提供完整的生命周期管理 API。
    // ============================================================

    /// <summary>检查黑板中是否存在指定键</summary>
    public bool HasKey(string key) => _data.ContainsKey(key);

    /// <summary>移除指定键（节点间数据流清理）</summary>
    public void RemoveKey(string key)
    {
        if (_data.Remove(key))
            _writeTrace.Remove(key);
    }

    /// <summary>清空黑板（子图切换时用于隔离上下文）</summary>
    public void Clear()
    {
        _data.Clear();
        _writeTrace.Clear();
    }



    /// <summary>
    ///     获取写入追踪信息（调试用）。
    ///     返回 Key → 最近写入的调用栈摘要。
    /// </summary>
    public IReadOnlyDictionary<string, string> GetWriteTrace()
    {
        return _writeTrace;
    }
}