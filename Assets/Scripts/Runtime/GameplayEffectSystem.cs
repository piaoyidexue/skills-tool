using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  Gameplay Effect (GE) 系统 —— 类 Unreal GAS 的 Buff/Debuff 框架
//  CSV 驱动，Modifier 队列模式，Tag 系统验证。
// ============================================================

/// <summary>GE 修改器操作类型</summary>
public enum GEModOp
{
    /// <summary>加法</summary>
    Add,
    /// <summary>乘法</summary>
    Multiply,
    /// <summary>覆盖</summary>
    Override
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
    /// <summary>自定义属性（字符串匹配）</summary>
    Custom
}

/// <summary>GE 持续时间策略</summary>
public enum GEDurationPolicy
{
    /// <summary>瞬间（立即执行后消失）</summary>
    Instant,
    /// <summary>有持续时间</summary>
    HasDuration,
    /// <summary>永久（手动移除）</summary>
    Infinite
}

/// <summary>
///     Gameplay Effect 实例 —— 挂载在目标身上的 Buff/Debuff。
/// </summary>
public class GameplayEffectInstance
{
    /// <summary>GE 配置 ID（对应 CSV 行的 ge_id）</summary>
    public int GEId;

    /// <summary>GE 名称</summary>
    public string Name;

    /// <summary>持续时间策略</summary>
    public GEDurationPolicy DurationPolicy;

    /// <summary>剩余时间（秒）</summary>
    public float RemainingDuration;

    /// <summary>总持续时间</summary>
    public float TotalDuration;

    /// <summary>修改器列表</summary>
    public List<GEModifier> Modifiers = new();

    /// <summary>授予的 Gameplay Tag 列表</summary>
    public List<string> GrantedTags = new();

    /// <summary>施加条件：需要目标已有的 Tag</summary>
    public List<string> RequiredTags = new();

    /// <summary>施加者</summary>
    public Transform Instigator;

    /// <summary>周期 Tick 间隔（0=不 Tick）</summary>
    public float Period;

    /// <summary>周期 Tick 计时器</summary>
    public float PeriodTimer;

    /// <summary>堆叠层数</summary>
    public int StackCount;

    /// <summary>最大堆叠层数</summary>
    public int MaxStacks;

    /// <summary>是否活跃</summary>
    public bool IsActive => DurationPolicy == GEDurationPolicy.Infinite ||
                            RemainingDuration > 0f;

    /// <summary>
    ///     Tick 驱动（每帧由 GEHost 调用）。
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (DurationPolicy == GEDurationPolicy.HasDuration)
        {
            RemainingDuration -= deltaTime;
        }

        if (Period > 0f)
        {
            PeriodTimer += deltaTime;
        }
    }
}

/// <summary>
///     GE 修改器 —— 单个属性修改。
/// </summary>
[System.Serializable]
public class GEModifier
{
    /// <summary>目标属性</summary>
    public GEAttribute Attribute;

    /// <summary>自定义属性名（当 Attribute == Custom 时使用）</summary>
    public string CustomAttribute;

    /// <summary>操作类型</summary>
    public GEModOp Operation;

    /// <summary>修改数值</summary>
    public float Magnitude;

    /// <summary>计算最终值</summary>
    public float Evaluate(float baseValue)
    {
        return Operation switch
        {
            GEModOp.Add => baseValue + Magnitude,
            GEModOp.Multiply => baseValue * Magnitude,
            GEModOp.Override => Magnitude,
            _ => baseValue
        };
    }
}

/// <summary>
///     GE 配置（CSV 行映射）。
/// </summary>
public class GEConfig
{
    public int GEId;
    public string Name;
    public GEDurationPolicy DurationPolicy;
    public float Duration;
    public float Period;
    public int MaxStacks;

    public List<GEModifier> Modifiers = new();
    public List<string> GrantedTags = new();
    public List<string> RequiredTags = new();

    /// <summary>
    ///     创建运行时 GE 实例。
    /// </summary>
    public GameplayEffectInstance CreateInstance(Transform instigator)
    {
        var instance = new GameplayEffectInstance
        {
            GEId = GEId,
            Name = Name,
            DurationPolicy = DurationPolicy,
            TotalDuration = Duration,
            RemainingDuration = Duration,
            Instigator = instigator,
            Period = Period,
            MaxStacks = MaxStacks,
            StackCount = 1
        };

        foreach (var mod in Modifiers)
        {
            instance.Modifiers.Add(new GEModifier
            {
                Attribute = mod.Attribute,
                CustomAttribute = mod.CustomAttribute,
                Operation = mod.Operation,
                Magnitude = mod.Magnitude
            });
        }

        instance.GrantedTags.AddRange(GrantedTags);
        instance.RequiredTags.AddRange(RequiredTags);

        return instance;
    }
}

/// <summary>
///     GE 宿主 —— 挂载在角色上，管理所有活跃的 GE 实例。
/// </summary>
public class GEHost : MonoBehaviour
{
    /// <summary>活跃 GE 实例列表</summary>
    private readonly List<GameplayEffectInstance> _activeEffects = new(16);

    private readonly List<GameplayEffectInstance> _toRemove = new(8);

    /// <summary>获取所有活跃 GE（只读）</summary>
    public IReadOnlyList<GameplayEffectInstance> ActiveEffects => _activeEffects;

    /// <summary>GE 变更事件</summary>
    public event Action<GameplayEffectInstance> OnEffectApplied;
    public event Action<GameplayEffectInstance> OnEffectRemoved;

    private void Update()
    {
        var dt = Time.deltaTime;
        _toRemove.Clear();

        foreach (var ge in _activeEffects)
        {
            ge.Tick(dt);
            if (!ge.IsActive)
            {
                _toRemove.Add(ge);
            }
        }

        foreach (var ge in _toRemove)
        {
            _activeEffects.Remove(ge);
            OnEffectRemoved?.Invoke(ge);
        }
    }

    /// <summary>
    ///     施加一个 GE。
    /// </summary>
    public bool ApplyEffect(GEConfig config, Transform instigator)
    {
        if (config == null) return false;

        // 检查施加条件
        foreach (var requiredTag in config.RequiredTags)
        {
            if (!HasTag(requiredTag)) return false;
        }

        // 检查是否已存在（堆叠逻辑）
        var existing = FindEffect(config.GEId);
        if (existing != null)
        {
            return TryStack(existing, config, instigator);
        }

        var instance = config.CreateInstance(instigator);
        _activeEffects.Add(instance);
        OnEffectApplied?.Invoke(instance);
        return true;
    }

    /// <summary>
    ///     移除指定 GE。
    /// </summary>
    public bool RemoveEffect(int geId)
    {
        for (var i = _activeEffects.Count - 1; i >= 0; i--)
        {
            if (_activeEffects[i].GEId == geId)
            {
                var effect = _activeEffects[i];
                _activeEffects.RemoveAt(i);
                OnEffectRemoved?.Invoke(effect);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     查询是否拥有指定 Tag。
    /// </summary>
    public bool HasTag(string tag)
    {
        foreach (var ge in _activeEffects)
        {
            if (ge.GrantedTags.Contains(tag))
                return true;
        }

        return false;
    }

    /// <summary>
    ///     查询是否拥有指定 GE。
    /// </summary>
    public bool HasEffect(int geId)
    {
        return FindEffect(geId) != null;
    }

    /// <summary>
    ///     计算指定属性的最终值（遍历所有 Modifier）。
    /// </summary>
    public float EvaluateAttribute(GEAttribute attribute, float baseValue)
    {
        var result = baseValue;

        // 先应用所有 Add 和 Override
        float additiveSum = 0f;
        float overrideValue = float.MinValue;
        var hasOverride = false;

        foreach (var ge in _activeEffects)
        {
            foreach (var mod in ge.Modifiers)
            {
                if (mod.Attribute != attribute) continue;

                switch (mod.Operation)
                {
                    case GEModOp.Add:
                        additiveSum += mod.Magnitude;
                        break;
                    case GEModOp.Override:
                        overrideValue = mod.Magnitude;
                        hasOverride = true;
                        break;
                }
            }
        }

        if (hasOverride)
            result = overrideValue;
        else
            result = baseValue + additiveSum;

        // 再应用所有 Multiply
        foreach (var ge in _activeEffects)
        {
            foreach (var mod in ge.Modifiers)
            {
                if (mod.Attribute == attribute && mod.Operation == GEModOp.Multiply)
                {
                    result *= mod.Magnitude;
                }
            }
        }

        return result;
    }

    /// <summary>
    ///     周期 Tick 触发检查（返回本帧需要触发 Tick 的 GE 列表）。
    /// </summary>
    public void GetPeriodicEffects(List<GameplayEffectInstance> results)
    {
        results.Clear();
        foreach (var ge in _activeEffects)
        {
            if (ge.Period > 0f && ge.PeriodTimer >= ge.Period)
            {
                ge.PeriodTimer = 0f;
                results.Add(ge);
            }
        }
    }

    /// <summary>
    ///     清空所有 GE。
    /// </summary>
    public void ClearAll()
    {
        foreach (var ge in _activeEffects)
        {
            OnEffectRemoved?.Invoke(ge);
        }

        _activeEffects.Clear();
    }

    private GameplayEffectInstance FindEffect(int geId)
    {
        foreach (var ge in _activeEffects)
        {
            if (ge.GEId == geId) return ge;
        }

        return null;
    }

    private bool TryStack(GameplayEffectInstance existing, GEConfig config, Transform instigator)
    {
        if (existing.StackCount < existing.MaxStacks)
        {
            existing.StackCount++;
            existing.RemainingDuration = Mathf.Max(existing.RemainingDuration, config.Duration);
            return true;
        }

        // 刷新持续时间但不增加层数
        existing.RemainingDuration = Mathf.Max(existing.RemainingDuration, config.Duration);
        return true;
    }

    private void OnDestroy()
    {
        ClearAll();
    }
}
