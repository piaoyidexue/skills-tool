using System;

/// <summary>
///     怪物配置数据结构。
///     对应 Monster.csv 的每一行数据。
/// </summary>
[Serializable]
public class MonsterConfig
{
    /// <summary>怪物唯一ID</summary>
    public int MonsterID;

    /// <summary>怪物名称</summary>
    public string Name;

    /// <summary>预制体路径</summary>
    public string PrefabPath;

    /// <summary>AI等级（Minion/Elite/Boss）</summary>
    public string AiTier;

    /// <summary>基础生命值</summary>
    public float BaseHP;

    /// <summary>基础攻击力</summary>
    public float BaseATK;

    /// <summary>基础防御力</summary>
    public float BaseDEF;

    /// <summary>移动速度</summary>
    public float MoveSpeed;

    /// <summary>攻击范围</summary>
    public float AttackRange;

    /// <summary>火焰抗性（百分比减伤）</summary>
    public float ResFire;

    /// <summary>冰霜抗性（百分比减伤）</summary>
    public float ResIce;

    /// <summary>闪电抗性（百分比减伤）</summary>
    public float ResLightning;

    /// <summary>AI行为树ID</summary>
    public int AiTreeID;

    /// <summary>掉落表ID</summary>
    public int DropTableID;
}