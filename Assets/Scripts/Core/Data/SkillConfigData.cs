using UnityEngine;

/// <summary>
///     技能配置数据资产 —— CSV 烘焙后的 ScriptableObject。
///     运行时直接读取结构化数据，跳过 CSV 文本解析。
///     在 AssetPostprocessor 中自动生成/更新。
/// </summary>
[CreateAssetMenu(menuName = "Skill System/Skill Config Data")]
public class SkillConfigData : ScriptableObject
{
    /// <summary>技能条目（按 SkillID 排序以支持二分查找）</summary>
    public SkillEntry[] Skills;

    /// <summary>
    ///     按 SkillID 查找配置（二分查找）。
    /// </summary>
    public bool TryGetSkill(int id, out SkillEntry entry)
    {
        if (Skills == null || Skills.Length == 0)
        {
            entry = default;
            return false;
        }

        // 二分查找
        var left = 0;
        var right = Skills.Length - 1;
        while (left <= right)
        {
            var mid = (left + right) / 2;
            var midId = Skills[mid].SkillID;
            if (midId == id)
            {
                entry = Skills[mid];
                return true;
            }

            if (midId < id)
                left = mid + 1;
            else
                right = mid - 1;
        }

        entry = default;
        return false;
    }
}

/// <summary>
///     技能条目（值类型，缓存友好）。
/// </summary>
[System.Serializable]
public struct SkillEntry
{
    public int SkillID;
    public string SkillName;
    public string GraphPath;
    public string ImpactVFXKey;
    public string BeamVFXKey;
    public string VFXProfileKey;
    public float Damage;
    public float DamageRate;
    public float Cooldown;
    public float CastRange;
    public float DelaySeconds;
    public float CritChance;
    public float Radius;
    public float ChainCount;
    public float VFXDuration;
    public float CastTime;
    public float ChannelDuration;
    public float PostCastTime;
    public float ProjectileSpeed;
    public string ProjectilePrefab;
    /// <summary>投射物弹道类型（0=直线, 1=追踪, 2=抛物线）</summary>
    public int ProjectileTrajectory;
    /// <summary>投射物命中判定半径</summary>
    public float ProjectileHitRadius;
    /// <summary>投射物最大存活时间（秒）</summary>
    public float ProjectileLifetime;
    /// <summary>投射物抛物线重力系数</summary>
    public float ProjectileGravity;
    /// <summary>投射物携带的元素/状态标签（分号分隔）</summary>
    public string ProjectileTags;
    public string ProjectileConfigKey;
    public float ResourceCost;
    public bool IsInterruptible;
}
