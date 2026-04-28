using UnityEngine;
using UnityEngine.Events;

/// <summary>
///     简易 Health 组件 —— 用于沙盒测试和原型阶段。
///     生产环境应替换为完整的伤害系统和 GE 管线。
/// </summary>
public class HealthComponent : MonoBehaviour
{
    [Header("属性")]
    public float MaxHealth = 100f;
    public float CurrentHealth { get; private set; }

    [Header("事件")]
    public UnityEvent<float, float> OnHealthChanged; // (current, max)
    public UnityEvent OnDeath;

    private void Awake()
    {
        CurrentHealth = MaxHealth;
    }

    /// <summary>受到伤害（会被 GEHost 的 DamageTakenMultiplier 影响）</summary>
    public void TakeDamage(float rawDamage, Transform instigator = null)
    {
        // 查询 GE 系统的伤害修正
        var geHost = GetComponent<GEHost>();
        if (geHost != null)
        {
            rawDamage = geHost.EvaluateAttribute(GEAttribute.DamageTakenMultiplier, rawDamage);
        }

        CurrentHealth = Mathf.Max(0f, CurrentHealth - rawDamage);
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);

        if (CurrentHealth <= 0f)
        {
            OnDeath?.Invoke();
        }
    }

    /// <summary>治疗</summary>
    public void Heal(float amount)
    {
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    /// <summary>重置到满血</summary>
    public void ResetToMax()
    {
        CurrentHealth = MaxHealth;
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    /// <summary>是否存活</summary>
    public bool IsAlive => CurrentHealth > 0f;
}
