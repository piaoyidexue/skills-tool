using System;
using UnityEngine;

/// <summary>
///     怪物属性集 —— 扩展 GAS 属性系统，支持怪物专属属性。
///     包含基础战斗属性、元素抗性、韧性、威胁值等。
/// </summary>
[CreateAssetMenu(fileName = "MonsterAttributeSet", menuName = "GAS/Attribute/Monster AttributeSet")]
public class MonsterAttributeSet : ScriptableObject
{
    /// <summary>基础生命值（HP）</summary>
    public float BaseHP = 100f;

    /// <summary>基础攻击力（ATK）</summary>
    public float BaseATK = 10f;

    /// <summary>基础防御力（DEF）</summary>
    public float BaseDEF = 5f;

    /// <summary>移动速度</summary>
    public float MoveSpeed = 3.5f;

    /// <summary>攻击范围</summary>
    public float AttackRange = 1.2f;

    /// <summary>火焰抗性（百分比减伤）</summary>
    public float ResFire = 0f;

    /// <summary>冰霜抗性（百分比减伤）</summary>
    public float ResIce = 0f;

    /// <summary>闪电抗性（百分比减伤）</summary>
    public float ResLightning = 0f;

    /// <summary>韧性（抗打断）</summary>
    public float Tenacity = 0f;

    /// <summary>威胁值（用于EQS筛选）</summary>
    public float ThreatLevel = 1f;

    /// <summary>装甲（物理减伤）</summary>
    public float Armor = 0f;

    /// <summary>根据等级计算最终属性值</summary>
    /// <param name="level">怪物等级</param>
    /// <returns>计算后的属性集</returns>
    public MonsterAttributeSet CalculateForLevel(int level)
    {
        var result = Instantiate(this);
        
        // 等级成长公式：基础值 * (1 + level * 0.1)
        result.BaseHP *= (1f + level * 0.1f);
        result.BaseATK *= (1f + level * 0.15f);
        result.BaseDEF *= (1f + level * 0.05f);
        result.MoveSpeed *= (1f + level * 0.02f);
        result.AttackRange *= (1f + level * 0.01f);
        
        return result;
    }
}