using UnityEngine;

/// <summary>
///     怪物属性运行时数据 —— 存储怪物实例化后的实际属性值。
///     由 MonsterFactory 在创建怪物时初始化，供 AI 和战斗系统查询。
/// </summary>
public class MonsterAttributeRuntime : MonoBehaviour
{
    [Header("Current Stats")]
    public float CurrentHP;
    public float CurrentATK;
    public float CurrentDEF;
    public float CurrentMoveSpeed;
    public float CurrentAttackRange;

    [Header("Elemental Resistances")]
    public float ResFire;
    public float ResIce;
    public float ResLightning;

    [Header("Other Stats")]
    public float Tenacity;
    public float ThreatLevel;
    public float Armor;

    /// <summary>
    ///     从配置数据初始化运行时属性。
    /// </summary>
    /// <param name="config">包含计算后属性的 MonsterAttributeSet</param>
    public void InitializeFrom(MonsterAttributeSet config)
    {
        if (config == null) return;

        CurrentHP = config.BaseHP;
        CurrentATK = config.BaseATK;
        CurrentDEF = config.BaseDEF;
        CurrentMoveSpeed = config.MoveSpeed;
        CurrentAttackRange = config.AttackRange;
        ResFire = config.ResFire;
        ResIce = config.ResIce;
        ResLightning = config.ResLightning;
        Tenacity = config.Tenacity;
        ThreatLevel = config.ThreatLevel;
        Armor = config.Armor;
    }

    /// <summary>
    ///     根据伤害类型和抗性计算实际减伤后的伤害值。
    /// </summary>
    public float CalculateDamage(float damage, DamageType damageType)
    {
        var resistance = damageType switch
        {
            DamageType.Fire => ResFire,
            DamageType.Ice => ResIce,
            DamageType.Lightning => ResLightning,
            _ => 0f
        };

        var reduction = resistance / (resistance + 100f);
        return damage * (1f - reduction);
    }
}

/// <summary>
///     伤害类型枚举。
/// </summary>
public enum DamageType
{
    Physical,
    Fire,
    Ice,
    Lightning
}