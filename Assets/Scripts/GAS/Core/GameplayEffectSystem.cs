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
    DamageTakenMultiplier,
    DamageDealtMultiplier,
    MoveSpeed,
    AttackSpeed,
    DamagePerTick,
    HealingReceived,
    Custom,
    ResFire,
    ResIce,
    ResLightning,
    Armor
}

/// <summary>GE 持续时间策略</summary>
public enum GEDurationPolicy
{
    Instant, HasDuration, Infinite
}

public class GameplayEffectInstance
{
    /// <summary>效果唯一标识ID</summary>
    public int GEId;
    /// <summary>效果名称</summary>
    public string Name;
    /// <summary>持续时间策略：即时 / 有限时长 / 永久</summary>
    public GEDurationPolicy DurationPolicy;
    /// <summary>剩余持续时间（秒）</summary>
    public float RemainingDuration;
    /// <summary>总持续时间（秒）</summary>
    public float TotalDuration;
    /// <summary>修改器列表，定义对属性的加成/削减</summary>
    public List<GEModifier> Modifiers = new();
    /// <summary>本效果授予的Tag列表</summary>
    public List<string> GrantedTags = new();
    /// <summary>本效果生效所需的前提Tag列表</summary>
    public List<string> RequiredTags = new();
    /// <summary>施放者（来源Transform）</summary>
    public Transform Instigator;
    /// <summary>周期触发间隔（秒），0表示非周期效果</summary>
    public float Period;
    /// <summary>周期触发计时器</summary>
    public float PeriodTimer;
    /// <summary>当前叠层数</summary>
    public int StackCount;
    /// <summary>最大叠层数</summary>
    public int MaxStacks;
    /// <summary>叠层策略</summary>
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
    /// <summary>修改的目标属性</summary>
    public GEAttribute Attribute;
    /// <summary>自定义属性名（当Attribute为Custom时使用）</summary>
    public string CustomAttribute;
    /// <summary>修改操作类型：加/乘/覆写</summary>
    public GEModOp Operation;
    /// <summary>修改量数值</summary>
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
    /// <summary>效果唯一标识ID</summary>
    public int GEId;
    /// <summary>效果名称</summary>
    public string Name;
    /// <summary>持续时间策略</summary>
    public GEDurationPolicy DurationPolicy;
    /// <summary>叠层策略，默认刷新时长</summary>
    public GEStackPolicy StackPolicy = GEStackPolicy.Refresh;
    /// <summary>持续时间（秒）</summary>
    public float Duration;
    /// <summary>周期触发间隔（秒），0表示非周期效果</summary>
    public float Period;
    /// <summary>最大叠层数</summary>
    public int MaxStacks;
    /// <summary>修改器列表</summary>
    public List<GEModifier> Modifiers = new();
    /// <summary>本效果授予的Tag列表</summary>
    public List<string> GrantedTags = new();
    /// <summary>本效果生效所需的前提Tag列表</summary>
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
    /// <summary>事件标识ID</summary>
    public string EventId;
    /// <summary>事件目标（受影响者）</summary>
    public Transform Target;
    /// <summary>事件施放者（来源）</summary>
    public Transform Instigator;
    /// <summary>原始数值（事件触发前的初始值）</summary>
    public float RawValue;
    /// <summary>处理后数值（可被订阅者修改，作为最终结果）</summary>
    public float Value;
    /// <summary>事件关联的Tag列表</summary>
    public List<string> Tags = new();
}

public class GEHost : MonoBehaviour, IStatusReceiver
{
    /// <summary>当前活跃的效果实例列表</summary>
    private readonly List<GameplayEffectInstance> _activeEffects = new(16);
    /// <summary>待移除效果缓冲区，避免遍历时修改集合</summary>
    private readonly List<GameplayEffectInstance> _toRemove = new(8);
    /// <summary>实体先天Tag容器（不由Effect授予）</summary>
    private readonly GameplayTagContainer _innateTags = default;

    // ---- 缓存当前所有活跃 Tag（用于变更检测） ----
    private readonly HashSet<string> _cachedTags = new(StringComparer.OrdinalIgnoreCase);

    // ---- 状态（Status）语义层 ----
    /// <summary>活跃状态字典：StatusType → StatusRuntime，桥接 Status 语义与 GE 底层</summary>
    private readonly Dictionary<StatusType, StatusRuntime> _activeStatuses = new();
    /// <summary>状态变更事件：当任何状态被应用、刷新或消耗时触发</summary>
    public event Action<GEHost, StatusRuntime> OnStatusChanged;

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

        // 清理过期的 Status（同步 GE 层的过期移除）
        var expiredStatuses = new List<StatusType>();
        foreach (var kvp in _activeStatuses)
        {
            kvp.Value.Remaining -= dt;
            if (!kvp.Value.IsActive) expiredStatuses.Add(kvp.Key);
        }
        foreach (var type in expiredStatuses)
        {
            _activeStatuses.Remove(type);
            OnStatusChanged?.Invoke(this, null);
        }
    }

    /// <summary>
    ///     应用效果（公开接口，已标记废弃）。
    ///     外部系统请使用 EffectSystem.ApplyEffect() 统一派发，
    ///     确保 Modifier 管线 + Reaction 引擎 + DamagePipeline 全链路生效。
    ///     GEHost.ApplyEffect() 会绕过上述管线，违反架构红线。
    /// </summary>
    [System.Obsolete("请使用 EffectSystem.ApplyEffect() 统一派发，禁止直接调用 GEHost.ApplyEffect()。" +
                     "直接调用会绕过 Modifier 管线和 DamagePipeline，违反架构红线。")]
    public bool ApplyEffect(GEConfig config, Transform instigator)
    {
        return ApplyEffectInternal(config, instigator);
    }

    /// <summary>
    ///     应用效果（内部接口）。
    ///     仅供 EffectSystem / ReactionEngine 等管线内部使用，
    ///     外部调用者必须走 EffectSystem.ApplyEffect()。
    /// </summary>
    public bool ApplyEffectInternal(GEConfig config, Transform instigator)
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

    // ============================================================
    //  IStatusReceiver 实现 —— Status 语义层桥接 GE 底层
    // ============================================================

    /// <summary>
    ///     施加状态。若同名状态已存在则刷新（重置剩余时间），
    ///     同时在 GE 层创建对应的 GE 实例以驱动 Modifier 和 Tag。
    /// </summary>
    public void ApplyStatus(StatusRuntime status)
    {
        if (status == null || status.Type == StatusType.None) return;

        if (_activeStatuses.TryGetValue(status.Type, out var existing))
        {
            // 刷新已有状态
            existing.Reset(status.Value, status.Duration, status.SourceTag, status.Instigator);
        }
        else
        {
            // 新建状态并加入字典
            var newStatus = new StatusRuntime
            {
                Type = status.Type
            };
            newStatus.Reset(status.Value, status.Duration, status.SourceTag, status.Instigator);
            _activeStatuses[status.Type] = newStatus;
        }

        // 同步到 GE 层：为该状态创建对应的 GE 实例
        ApplyStatusAsGE(status);

        OnStatusChanged?.Invoke(this, _activeStatuses[status.Type]);
    }

    /// <summary>是否拥有指定类型的状态（且状态仍活跃）。</summary>
    public bool HasStatus(StatusType type)
    {
        return _activeStatuses.TryGetValue(type, out var s) && s.IsActive;
    }

    /// <summary>尝试获取指定类型的状态实例。</summary>
    public bool TryGetStatus(StatusType type, out StatusRuntime status)
    {
        if (_activeStatuses.TryGetValue(type, out var s) && s.IsActive)
        {
            status = s;
            return true;
        }
        status = null;
        return false;
    }

    /// <summary>
    ///     消耗指定类型的状态：取出后从活跃字典中移除，
    ///     同时移除对应的 GE 实例和授予的 Tag。
    /// </summary>
    public bool ConsumeStatus(StatusType type, out StatusRuntime status)
    {
        if (_activeStatuses.TryGetValue(type, out var s) && s.IsActive)
        {
            status = s;
            _activeStatuses.Remove(type);

            // 同步移除 GE 层实例
            RemoveStatusGE(type);

            OnStatusChanged?.Invoke(this, status);
            return true;
        }
        status = null;
        return false;
    }

    /// <summary>获取当前所有活跃状态的只读集合。</summary>
    public IReadOnlyCollection<StatusRuntime> GetActiveStatuses()
    {
        var result = new List<StatusRuntime>();
        foreach (var kvp in _activeStatuses)
            if (kvp.Value.IsActive) result.Add(kvp.Value);
        return result;
    }

    // ---- Status → GE 桥接 ----

    /// <summary>StatusType 到 GE Tag 的映射表</summary>
    private static readonly Dictionary<StatusType, string> StatusTagMap = new()
    {
        { StatusType.Burn, "status.burn" },
        { StatusType.Chill, "status.chill" },
        { StatusType.Conductive, "status.conductive" },
        { StatusType.Mark, "status.mark" },
        { StatusType.Freeze, "status.freeze" },
        { StatusType.Slow, "status.slow" },
        { StatusType.Stun, "status.stun" },
        { StatusType.Poison, "status.poison" },
        { StatusType.Root, "status.root" },
    };

    /// <summary>StatusType 到 GE ID 的偏移基数（避免与普通 GE ID 冲突）</summary>
    private const int StatusGEIdBase = 100000;

    /// <summary>将状态作为 GE 实例应用到 GE 层</summary>
    private void ApplyStatusAsGE(StatusRuntime status)
    {
        var tag = GetStatusTag(status.Type);
        if (tag == null) return;

        var geId = StatusGEIdBase + (int)status.Type;
        var config = new GEConfig
        {
            GEId = geId,
            Name = $"Status_{status.Type}",
            DurationPolicy = GEDurationPolicy.HasDuration,
            Duration = status.Duration,
            StackPolicy = GEStackPolicy.Refresh,
            MaxStacks = 1
        };
        config.GrantedTags.Add(tag);

        // 根据状态类型添加对应的 Modifier
        AddStatusModifiers(status, config);

        // 如果 GE 已存在则先移除再重新应用（刷新时长）
        if (HasEffect(geId)) RemoveEffect(geId);
        ApplyEffectInternal(config, status.Instigator);
    }

    /// <summary>根据状态类型添加对应的 GE Modifier</summary>
    private void AddStatusModifiers(StatusRuntime status, GEConfig config)
    {
        switch (status.Type)
        {
            case StatusType.Slow:
                config.Modifiers.Add(new GEModifier
                {
                    Attribute = GEAttribute.MoveSpeed, Operation = GEModOp.Multiply, Magnitude = status.Value
                });
                break;
            case StatusType.Freeze:
                // 冻结：移速为 0，攻速为 0
                config.Modifiers.Add(new GEModifier
                {
                    Attribute = GEAttribute.MoveSpeed, Operation = GEModOp.Override, Magnitude = 0f
                });
                config.Modifiers.Add(new GEModifier
                {
                    Attribute = GEAttribute.AttackSpeed, Operation = GEModOp.Override, Magnitude = 0f
                });
                break;
            case StatusType.Stun:
                // 眩晕：移速为 0，攻速为 0
                config.Modifiers.Add(new GEModifier
                {
                    Attribute = GEAttribute.MoveSpeed, Operation = GEModOp.Override, Magnitude = 0f
                });
                config.Modifiers.Add(new GEModifier
                {
                    Attribute = GEAttribute.AttackSpeed, Operation = GEModOp.Override, Magnitude = 0f
                });
                break;
            case StatusType.Burn:
                // 燃烧：周期性伤害
                config.Period = 0.5f;
                config.Modifiers.Add(new GEModifier
                {
                    Attribute = GEAttribute.DamagePerTick, Operation = GEModOp.Add, Magnitude = status.Value
                });
                break;
            case StatusType.Poison:
                // 中毒：周期性伤害
                config.Period = 1f;
                config.Modifiers.Add(new GEModifier
                {
                    Attribute = GEAttribute.DamagePerTick, Operation = GEModOp.Add, Magnitude = status.Value
                });
                break;
            case StatusType.Root:
                // 定身：移速为 0
                config.Modifiers.Add(new GEModifier
                {
                    Attribute = GEAttribute.MoveSpeed, Operation = GEModOp.Override, Magnitude = 0f
                });
                break;
            // Chill、Conductive、Mark 仅通过 Tag 生效，不需要额外 Modifier
        }
    }

    /// <summary>移除状态对应的 GE 实例</summary>
    private void RemoveStatusGE(StatusType type)
    {
        var geId = StatusGEIdBase + (int)type;
        RemoveEffect(geId);
    }

    /// <summary>获取状态类型对应的 Tag 字符串</summary>
    private static string GetStatusTag(StatusType type)
    {
        return StatusTagMap.TryGetValue(type, out var tag) ? tag : null;
    }

    public void ClearAll()
    {
        foreach (var ge in _activeEffects) OnEffectRemoved?.Invoke(ge);
        _activeEffects.Clear();
        _activeStatuses.Clear();
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
    /// <summary>基础攻击力</summary>
    [SerializeField] private float _attack = 10f;
    /// <summary>基础防御力</summary>
    [SerializeField] private float _defense = 2f;
    /// <summary>基础最大生命值</summary>
    [SerializeField] private float _maxHealth = 100f;
    /// <summary>基础移动速度</summary>
    [SerializeField] private float _moveSpeed = 5f;
    /// <summary>基础攻击速度</summary>
    [SerializeField] private float _attackSpeed = 1f;
    /// <summary>基础暴击率（0~1）</summary>
    [SerializeField] private float _critChance = 0.05f;
    /// <summary>基础暴击伤害倍率</summary>
    [SerializeField] private float _critDamage = 1.5f;

    /// <summary>关联的GEHost组件引用，用于查询GE修改后的最终属性</summary>
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
