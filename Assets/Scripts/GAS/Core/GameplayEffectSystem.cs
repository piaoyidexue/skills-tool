using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  Gameplay Effect (GE) 系统 —— 类 Unreal GAS 的 Buff/Debuff 框架
//  CSV 驱动，Modifier 队列模式，Tag 系统验证 + 事件拦截。
// ============================================================

/// <summary>GE 修改器操作类型</summary>
public enum GEModOp
{
    Add, Multiply, Override
}

/// <summary>GE 修改器目标属性</summary>
public enum GEAttribute
{
    DamageTakenMultiplier, DamageDealtMultiplier,
    MoveSpeed, AttackSpeed, DamagePerTick, HealingReceived,
    Custom
}

/// <summary>GE 持续时间策略</summary>
public enum GEDurationPolicy
{
    Instant, HasDuration, Infinite
}

public class GameplayEffectInstance
{
    public int GEId;
    public string Name;
    public GEDurationPolicy DurationPolicy;
    public float RemainingDuration;
    public float TotalDuration;
    public List<GEModifier> Modifiers = new();
    public List<string> GrantedTags = new();
    public List<string> RequiredTags = new();
    public Transform Instigator;
    public float Period;
    public float PeriodTimer;
    public int StackCount;
    public int MaxStacks;
    public GEStackPolicy StackPolicy;
    public bool IsActive => DurationPolicy == GEDurationPolicy.Infinite || RemainingDuration > 0f;

    public void Tick(float deltaTime)
    {
        if (DurationPolicy == GEDurationPolicy.HasDuration) RemainingDuration -= deltaTime;
        if (Period > 0f) PeriodTimer += deltaTime;
    }
}

[System.Serializable]
public class GEModifier
{
    public GEAttribute Attribute;
    public string CustomAttribute;
    public GEModOp Operation;
    public float Magnitude;

    public float Evaluate(float baseValue) => Operation switch
    {
        GEModOp.Add => baseValue + Magnitude,
        GEModOp.Multiply => baseValue * Magnitude,
        GEModOp.Override => Magnitude,
        _ => baseValue
    };
}

public class GEConfig
{
    public int GEId;
    public string Name;
    public GEDurationPolicy DurationPolicy;
    public GEStackPolicy StackPolicy = GEStackPolicy.Refresh;
    public float Duration;
    public float Period;
    public int MaxStacks;
    public List<GEModifier> Modifiers = new();
    public List<string> GrantedTags = new();
    public List<string> RequiredTags = new();

    public GameplayEffectInstance CreateInstance(Transform instigator)
    {
        var instance = new GameplayEffectInstance
        {
            GEId = GEId, Name = Name, DurationPolicy = DurationPolicy,
            StackPolicy = StackPolicy, TotalDuration = Duration,
            RemainingDuration = Duration, Instigator = instigator,
            Period = Period, MaxStacks = MaxStacks, StackCount = 1
        };
        foreach (var mod in Modifiers)
            instance.Modifiers.Add(new GEModifier { Attribute = mod.Attribute,
                CustomAttribute = mod.CustomAttribute, Operation = mod.Operation,
                Magnitude = mod.Magnitude });
        instance.GrantedTags.AddRange(GrantedTags);
        instance.RequiredTags.AddRange(RequiredTags);
        return instance;
    }
}

public class GEEventContext
{
    public string EventId;
    public Transform Target;
    public Transform Instigator;
    public float RawValue;
    public float Value;
    public List<string> Tags = new();
}

public class GEHost : MonoBehaviour
{
    private readonly List<GameplayEffectInstance> _activeEffects = new(16);
    private readonly List<GameplayEffectInstance> _toRemove = new(8);
    private readonly GameplayTagContainer _innateTags = default;

    // ---- 缓存当前所有活跃 Tag（用于变更检测） ----
    private readonly HashSet<string> _cachedTags = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<GameplayEffectInstance> ActiveEffects => _activeEffects;

    // ---- 事件 ----
    public event Action<GameplayEffectInstance> OnEffectApplied;
    public event Action<GameplayEffectInstance> OnEffectRemoved;
    public event Action<GEEventContext> OnGameplayEvent;

    /// <summary>Tag 新增事件：当实体获得一个之前没有的 Tag 时触发。</summary>
    public event Action<GEHost, string> OnTagAdded;

    /// <summary>Tag 移除事件：当实体失去一个之前拥有的 Tag 时触发。</summary>
    public event Action<GEHost, string> OnTagRemoved;

    /// <summary>Effect 过期事件：当 Effect 因持续时间结束自动移除时触发。</summary>
    public event Action<GameplayEffectInstance> OnEffectExpired;

    /// <summary>Effect 叠层变更事件：当 Effect 层数增加时触发。</summary>
    public event Action<GameplayEffectInstance> OnStackChanged;

    private void Update()
    {
        var dt = Time.deltaTime;
        _toRemove.Clear();
        foreach (var ge in _activeEffects)
        {
            ge.Tick(dt);
            if (!ge.IsActive) _toRemove.Add(ge);
        }
        foreach (var ge in _toRemove)
        {
            _activeEffects.Remove(ge);
            OnEffectRemoved?.Invoke(ge);
            OnEffectExpired?.Invoke(ge);
            NotifyTagChanges();
        }
    }

    public bool ApplyEffect(GEConfig config, Transform instigator)
    {
        if (config == null) return false;
        foreach (var tag in config.RequiredTags)
            if (!HasTag(tag)) return false;

        var existing = FindEffect(config.GEId);
        if (existing != null)
        {
            var stacked = TryStack(existing, config, instigator);
            if (stacked) OnStackChanged?.Invoke(existing);
            return stacked;
        }

        var instance = config.CreateInstance(instigator);
        _activeEffects.Add(instance);
        OnEffectApplied?.Invoke(instance);
        NotifyTagChanges();
        return true;
    }

    public bool RemoveEffect(int geId)
    {
        for (var i = _activeEffects.Count - 1; i >= 0; i--)
        {
            if (_activeEffects[i].GEId == geId)
            {
                var e = _activeEffects[i]; _activeEffects.RemoveAt(i);
                OnEffectRemoved?.Invoke(e);
                NotifyTagChanges();
                return true;
            }
        }
        return false;
    }

    public bool HasTag(string tag)
    {
        if (_innateTags.HasTag(tag)) return true;
        foreach (var ge in _activeEffects)
            foreach (var granted in ge.GrantedTags)
                if (new GameplayTag(granted).Matches(tag)) return true;
        return false;
    }

    public bool HasEffect(int geId) => FindEffect(geId) != null;

    public void AddInnateTag(string tag)
    {
        _innateTags.AddTag(tag);
        if (!_cachedTags.Contains(new GameplayTag(tag).Value))
            OnTagAdded?.Invoke(this, tag);
        RebuildCachedTags();
    }

    public void RemoveInnateTag(string tag)
    {
        _innateTags.RemoveTag(tag);
        NotifyTagChanges();
    }

    public bool HasInnateTag(string tag) => _innateTags.HasTag(tag);

    /// <summary>获取当前所有活跃 Tag 的快照（包含先天 Tag 和 Effect 授予的 Tag）。</summary>
    public void GetCurrentTags(HashSet<string> result)
    {
        result.Clear();
        if (_innateTags.AllTags != null)
            foreach (var tag in _innateTags.AllTags)
                result.Add(tag);
        foreach (var ge in _activeEffects)
            foreach (var granted in ge.GrantedTags)
                result.Add(new GameplayTag(granted).Value);
    }

    /// <summary>获取当前所有活跃 Tag 的快照列表。</summary>
    public List<string> GetCurrentTagsList()
    {
        var result = new List<string>();
        if (_innateTags.AllTags != null)
            foreach (var tag in _innateTags.AllTags)
                result.Add(tag);
        foreach (var ge in _activeEffects)
            foreach (var granted in ge.GrantedTags)
                result.Add(new GameplayTag(granted).Value);
        return result;
    }

    // ============================================================
    //  Tag 变更通知
    // ============================================================

    /// <summary>
    ///     对比当前 Tag 集合与缓存，触发 OnTagAdded/OnTagRemoved 事件。
    ///     仅在 Effect 应用/移除/先天 Tag 变更时调用。
    /// </summary>
    private void NotifyTagChanges()
    {
        var newTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        GetCurrentTags(newTags);

        // 检测新增 Tag
        foreach (var tag in newTags)
            if (!_cachedTags.Contains(tag))
                OnTagAdded?.Invoke(this, tag);

        // 检测移除 Tag
        foreach (var tag in _cachedTags)
            if (!newTags.Contains(tag))
                OnTagRemoved?.Invoke(this, tag);

        _cachedTags.Clear();
        foreach (var tag in newTags)
            _cachedTags.Add(tag);
    }

    /// <summary>完全重建缓存（跳过变更检测）。</summary>
    private void RebuildCachedTags()
    {
        _cachedTags.Clear();
        GetCurrentTags(_cachedTags);
    }

    // ---- 事件拦截触发 ----

    /// <summary>
    ///     直接触发 OnGameplayEvent，使用外部传入的共享上下文。
    ///     DamagePipeline 等调用此方法注入 GEEventContext，
    ///     所有订阅者修改同一份 ctx，调用方后续读取 ctx.Value 即得最终结果。
    /// </summary>
    public void RaiseGameplayEvent(GEEventContext ctx)
    {
        OnGameplayEvent?.Invoke(ctx);
    }

    /// <summary>
    ///     创建新上下文并触发 OnGameplayEvent，返回处理后的值。
    ///     适用于独立调用，无需与外部共享 ctx。
    /// </summary>
    public float InvokeGameplayEvent(string eventId, float value, Transform instigator, params string[] tags)
    {
        var ctx = new GEEventContext
        {
            EventId = eventId, Target = transform,
            Instigator = instigator, RawValue = value, Value = value
        };
        if (tags != null) ctx.Tags.AddRange(tags);

        foreach (var ge in _activeEffects)
        {
            if (eventId == "Tick" && ge.Period > 0f && ge.PeriodTimer >= ge.Period)
            {
                ge.PeriodTimer = 0f;
                foreach (var mod in ge.Modifiers)
                    if (mod.Attribute == GEAttribute.DamagePerTick)
                        ctx.Value += mod.Magnitude * ge.StackCount;
            }
        }

        OnGameplayEvent?.Invoke(ctx);
        return ctx.Value;
    }

    public float EvaluateAttribute(GEAttribute attribute, float baseValue)
    {
        var result = baseValue;
        float additiveSum = 0f, overrideValue = float.MinValue;
        var hasOverride = false;

        foreach (var ge in _activeEffects)
            foreach (var mod in ge.Modifiers)
            {
                if (mod.Attribute != attribute) continue;
                switch (mod.Operation)
                {
                    case GEModOp.Add: additiveSum += mod.Magnitude; break;
                    case GEModOp.Override: overrideValue = mod.Magnitude; hasOverride = true; break;
                }
            }

        result = hasOverride ? overrideValue : baseValue + additiveSum;

        foreach (var ge in _activeEffects)
            foreach (var mod in ge.Modifiers)
                if (mod.Attribute == attribute && mod.Operation == GEModOp.Multiply)
                    result *= mod.Magnitude;

        return result;
    }

    public void GetPeriodicEffects(List<GameplayEffectInstance> results)
    {
        results.Clear();
        foreach (var ge in _activeEffects)
            if (ge.Period > 0f && ge.PeriodTimer >= ge.Period)
            { ge.PeriodTimer = 0f; results.Add(ge); }
    }

    public void ClearAll()
    {
        foreach (var ge in _activeEffects) OnEffectRemoved?.Invoke(ge);
        _activeEffects.Clear();
        NotifyTagChanges();
    }

    private void Awake()
    {
        // 初始化缓存
        RebuildCachedTags();
        // 自动注册到全局 Tag 事件总线
        TagEventBus.Register(this);
    }

    private GameplayEffectInstance FindEffect(int geId)
    {
        foreach (var ge in _activeEffects)
            if (ge.GEId == geId) return ge;
        return null;
    }

    private bool TryStack(GameplayEffectInstance existing, GEConfig config, Transform instigator)
    {
        switch (config.StackPolicy)
        {
            case GEStackPolicy.Ignore: return false;
            case GEStackPolicy.Add:
                if (existing.StackCount < existing.MaxStacks)
                { existing.StackCount++; existing.RemainingDuration = Mathf.Max(existing.RemainingDuration, config.Duration); return true; }
                existing.RemainingDuration = Mathf.Max(existing.RemainingDuration, config.Duration); return true;
            default:
                existing.RemainingDuration = Mathf.Max(existing.RemainingDuration, config.Duration); return true;
        }
    }

    private void OnDestroy()
    {
        ClearAll();
        // 自动注销
        TagEventBus.Unregister(this);
    }
}

// ============================================================
//  AttributeSet
// ============================================================

public class AttributeSet : MonoBehaviour
{
    [SerializeField] private float _attack = 10f;
    [SerializeField] private float _defense = 2f;
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _attackSpeed = 1f;
    [SerializeField] private float _critChance = 0.05f;
    [SerializeField] private float _critDamage = 1.5f;

    private GEHost _geHost;

    // ──────────── 数据绑定属性（UI 响应式驱动） ────────────

    /// <summary>当前生命值 —— 赋值时自动通知 UI 刷新</summary>
    public readonly BindableFloat BindableHealth = new();

    /// <summary>最大生命值 —— 赋值时自动通知 UI 刷新</summary>
    public readonly BindableFloat BindableMaxHealth = new();

    public float BaseAttack => _attack;
    public float BaseDefense => _defense;
    public float BaseMaxHealth => _maxHealth;
    public float BaseMoveSpeed => _moveSpeed;
    public float BaseAttackSpeed => _attackSpeed;
    public float BaseCritChance => _critChance;
    public float BaseCritDamage => _critDamage;

    /// <summary>
    ///     当前生命值。写入时同步更新 BindableHealth，
    ///     自动触发已注册的 UI 回调。
    /// </summary>
    public float CurrentHealth
    {
        get => BindableHealth.GetValue();
        set => BindableHealth.SetClamped(value, 0f, MaxHealth);
    }

    public bool IsAlive => BindableHealth.GetValue() > 0f;
    public float MaxHealth => BindableMaxHealth.GetValue();
    public float HealthPercent
    {
        get
        {
            var max = BindableMaxHealth.GetValue();
            return max > 0f ? BindableHealth.GetValue() / max : 0f;
        }
    }

    public float FinalAttack => _geHost != null ? _geHost.EvaluateAttribute(GEAttribute.Custom, _attack) : _attack;
    public float FinalMoveSpeed => _geHost != null ? _geHost.EvaluateAttribute(GEAttribute.MoveSpeed, _moveSpeed) : _moveSpeed;
    public float FinalAttackSpeed => _geHost != null ? _geHost.EvaluateAttribute(GEAttribute.AttackSpeed, _attackSpeed) : _attackSpeed;

    private void Awake()
    {
        _geHost = GetComponent<GEHost>();

        // 初始化绑定属性（静默赋值，不触发回调）
        BindableMaxHealth.SetValueWithoutNotify(_maxHealth);
        BindableHealth.SetValueWithoutNotify(_maxHealth);
    }

    public void TakeDamage(float amount, Transform instigator)
    {
        if (!IsAlive) return;
        CurrentHealth -= Mathf.Max(1f, amount - _defense);
        if (BindableHealth.GetValue() <= 0f) OnDeath?.Invoke(instigator);
    }

    public void Heal(float amount) { if (IsAlive) CurrentHealth += amount; }

    public event Action<Transform> OnDeath;

    private void OnDestroy()
    {
        BindableHealth.ClearListeners();
        BindableMaxHealth.ClearListeners();
    }
}
