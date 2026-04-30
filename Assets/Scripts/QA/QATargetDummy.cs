using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     QA 增强靶子 —— 取代基础 TargetDummy，用于所有测试场景。
///     功能：世界空间 UI 显示状态/GE Tags、受击变色、浮动跳字、
///           Odin Inspector 实时调试面板、世界坐标对齐的彩色图标。
/// </summary>
[RequireComponent(typeof(GEHost))]
[RequireComponent(typeof(HealthComponent))]
public class QATargetDummy : MonoBehaviour, IDamageable
{
    [Header("=== QA 基础配置 ===")]
    [Tooltip("靶子初始生命值")]
    [SerializeField] private float _maxHealth = 1000f;

    [Header("=== QA 显示配置 ===")]
    [Tooltip("头顶 UI Canvas 相对于靶子中心的偏移")]
    [SerializeField] private Vector3 _uiOffset = new(0f, 2.2f, 0f);

    [Tooltip("浮动跳字预制体（可选，不赋值则用 Debug GUI）")]
    [SerializeField] private QAFloatingText _floatingTextPrefab;

    [Tooltip("受击时变色的持续时间")]
    [SerializeField] private float _hitColorDuration = 0.3f;

    [Header("=== QA 状态颜色 ===")]
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _hitColor = Color.red;
    [SerializeField] private Color _deadColor = Color.gray;

    // ---- 运行时引用 ----
    private GEHost _geHost;
    private HealthComponent _healthComp;

    // ---- 状态机 ----
    private float _currentHealth;
    private float _hitColorTimer;
    private bool _isDead;
    private Renderer[] _renderers;

    // ---- QA 事件记录 ----
    private readonly List<QADamageRecord> _damageHistory = new(64);
    private readonly List<QAStatusSnapshot> _statusSnapshots = new(16);

    // ---- 公开属性 ----
    public float CurrentHealth => _currentHealth;
    public float HealthRatio => _maxHealth > 0f ? _currentHealth / _maxHealth : 0f;
    public bool IsDead => _isDead;
    public IReadOnlyList<QADamageRecord> DamageHistory => _damageHistory;
    public GEHost GEHostComponent => _geHost;

    public event Action<float, Transform, float> OnDamaged;    // (finalDamage, source, rawDamage)
    public event Action OnDied;
    public event Action OnReset;

    private void Awake()
    {
        _geHost = GetComponent<GEHost>();
        _healthComp = GetComponent<HealthComponent>();
        _renderers = GetComponentsInChildren<Renderer>();

        _currentHealth = _maxHealth;
    }

    private void Update()
    {
        // 受击变色消退
        if (_hitColorTimer > 0f)
        {
            _hitColorTimer -= Time.deltaTime;
            ApplyHitColor();
        }

        // 同步历史记录大小
        while (_damageHistory.Count > 128) _damageHistory.RemoveAt(0);
    }

    // ===== IDamageable 实现 =====

    public void TakeDamage(float amount, Transform instigator)
    {
        if (_isDead || amount <= 0f) return;

        // 基础防御 + GE 伤害修正
        var finalAmount = amount;
        if (_geHost != null)
        {
            finalAmount = _geHost.EvaluateAttribute(GEAttribute.DamageTakenMultiplier, amount);
        }

        // GE 伤害修正：通过 GEHost.EvaluateAttribute 获取 DamageTakenMultiplier
        var attrSet = GetComponent<AttributeSet>();
        if (attrSet != null)
        {
            attrSet.TakeDamage(amount, instigator);
            return; // AttributeSet 内部已处理
        }

        finalAmount = Mathf.Max(1f, amount);
        _currentHealth = Mathf.Max(0f, _currentHealth - finalAmount);

        // 记录伤害
        _damageHistory.Add(new QADamageRecord
        {
            Timestamp = Time.time,
            RawDamage = amount,
            FinalDamage = finalAmount,
            SourceName = instigator != null ? instigator.name : "Unknown",
            RemainingHealth = _currentHealth
        });

        // QA 反馈
        TriggerHitColor();
        ShowFloatingText(finalAmount, instigator);
        OnDamaged?.Invoke(finalAmount, instigator, amount);

        Debug.Log($"<color=red><b>[QA 受击]</b></color> {gameObject.name} -{finalAmount:F1} HP " +
                  $"(来源: {instigator?.name ?? "Unknown"}, 剩余: {_currentHealth:F1}/{_maxHealth})");

        // 死亡判定
        if (_currentHealth <= 0f)
        {
            _isDead = true;
            ApplyDeadColor();
            OnDied?.Invoke();
            Debug.Log($"<color=gray><b>[QA 死亡]</b></color> {gameObject.name} 已阵亡。");
        }
    }

    // ===== QA 工具方法 =====

    /// <summary>重置到满血，清空历史记录，停止所有 GE 和状态。</summary>
    public void ResetQA()
    {
        _currentHealth = _maxHealth;
        _isDead = false;
        _damageHistory.Clear();
        _statusSnapshots.Clear();
        _hitColorTimer = 0f;

        // 清除 GE（GEHost.ClearAll 已清理所有效果和标签）
        _geHost?.ClearAll();

        // 恢复渲染颜色
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            foreach (var m in r.materials) m.color = _normalColor;
        }

        // 重置 HealthComponent
        if (_healthComp != null) _healthComp.ResetToMax();

        OnReset?.Invoke();
        Debug.Log($"<color=green><b>[QA 重置]</b></color> {gameObject.name} 已重置。");
    }

    /// <summary>获取当前快照（用于编辑器 Odin 面板显示）。</summary>
    public QAStatusSnapshot GetSnapshot()
    {
        var snap = new QAStatusSnapshot
        {
            Health = _currentHealth,
            HealthRatio = HealthRatio,
            IsDead = _isDead,
            Time = Time.time
        };

        // 快照 GE Tags + 从 GE Tag 映射 StatusType
        if (_geHost != null)
        {
            foreach (var ge in _geHost.ActiveEffects)
            {
                var remaining = ge.DurationPolicy == GEDurationPolicy.Infinite
                    ? float.PositiveInfinity
                    : ge.RemainingDuration;
                snap.GETags.Add(ge.Name ?? $"GE_{ge.GEId}");
                snap.GERemainingTimes.Add(remaining);
                snap.GEStacks.Add(ge.StackCount);

                // 从 GE 授予的 Tag 映射到 StatusType
                foreach (var tag in ge.GrantedTags)
                    if (System.Enum.TryParse<StatusType>(tag, true, out var st) && st != StatusType.None)
                    {
                        snap.StatusTypes.Add(st);
                        snap.StatusValues.Add(ge.StackCount);
                        snap.StatusRemaining.Add(remaining);
                    }
            }
        }

        return snap;
    }

    // ===== 内部方法 =====

    private void TriggerHitColor()
    {
        _hitColorTimer = _hitColorDuration;
        ApplyHitColor();
    }

    private void ApplyHitColor()
    {
        if (_renderers == null) return;
        var t = _hitColorTimer / _hitColorDuration;
        var col = Color.Lerp(_normalColor, _hitColor, t);
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            foreach (var m in r.materials) m.color = col;
        }
    }

    private void ApplyDeadColor()
    {
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            foreach (var m in r.materials) m.color = _deadColor;
        }
    }

    private void ShowFloatingText(float damage, Transform source)
    {
        if (_floatingTextPrefab != null)
        {
            var text = Instantiate(_floatingTextPrefab,
                transform.position + _uiOffset, Quaternion.identity);
            text.ShowDamage(damage, source != null && source.CompareTag("Player"));
        }
    }
}

// ===== QA 辅助数据结构 =====

/// <summary>单条伤害记录（用于编辑器面板回放）</summary>
[System.Serializable]
public struct QADamageRecord
{
    public float Timestamp;
    public float RawDamage;
    public float FinalDamage;
    public string SourceName;
    public float RemainingHealth;
}

/// <summary>当前状态快照（用于 Odin Inspector 实时显示）</summary>
[System.Serializable]
public class QAStatusSnapshot
{
    public float Health;
    public float HealthRatio;
    public bool IsDead;
    public float Time;

    public readonly List<string> GETags = new();
    public readonly List<float> GERemainingTimes = new();
    public readonly List<int> GEStacks = new();

    public readonly List<StatusType> StatusTypes = new();
    public readonly List<float> StatusValues = new();
    public readonly List<float> StatusRemaining = new();
}
