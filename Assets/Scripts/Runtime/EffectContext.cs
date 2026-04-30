using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  战斗上下文 (EffectContext)
//  不可变数据快照，在技能触发瞬间生成。
//  禁止在后续计算中跨模块回溯获取基础信息。
// ============================================================

/// <summary>
///     战斗上下文 —— 技能/效果触发时的不可变数据快照。
///     在触发瞬间生成，记录所有上下文信息，后续只读。
/// </summary>
[Serializable]
public struct EffectContext : IEquatable<EffectContext>
{
    /// <summary>施法者引用</summary>
    public Transform Instigator;

    /// <summary>施法者的 GEHost（缓存避免重复 GetComponent）</summary>
    public GEHost InstigatorHost;

    /// <summary>原始目标引用（可能为 null）</summary>
    public Transform Target;

    /// <summary>目标的 GEHost</summary>
    public GEHost TargetHost;

    /// <summary>目标点空间坐标</summary>
    public Vector3 TargetPoint;

    /// <summary>当前技能等级</summary>
    public int AbilityLevel;

    /// <summary>随机数种子（用于同步随机）</summary>
    public int RandomSeed;

    /// <summary>触发时刻（世界时间）</summary>
    public float Timestamp;

    /// <summary>关联的 SkillConfig ID</summary>
    public int SourceSkillId;

    /// <summary>关联的 SkillExecution 引用（用于回调）</summary>
    public object SourceExecution;

    /// <summary>附加的游戏标签（如元素类型）</summary>
    public List<string> SourceTags;

    /// <summary>
    ///     工厂方法：创建标准战斗上下文。
    ///     所有参数在创建时固定，后续不可修改。
    /// </summary>
    public static EffectContext Create(Transform instigator, Transform target,
        Vector3 targetPoint, int abilityLevel = 1, int sourceSkillId = 0)
    {
        return new EffectContext
        {
            Instigator = instigator,
            InstigatorHost = instigator != null ? instigator.GetComponent<GEHost>() : null,
            Target = target,
            TargetHost = target != null ? target.GetComponent<GEHost>() : null,
            TargetPoint = targetPoint,
            AbilityLevel = abilityLevel,
            RandomSeed = Environment.TickCount,
            Timestamp = Time.time,
            SourceSkillId = sourceSkillId,
            SourceExecution = null,
            SourceTags = null
        };
    }

    /// <summary>
    ///     从另一个上下文复制（用于派生新上下文）。
    /// </summary>
    public EffectContext Fork()
    {
        var copy = this;
        copy.SourceTags = SourceTags != null ? new List<string>(SourceTags) : null;
        return copy;
    }

    /// <summary>
    ///     获取或创建随机数生成器（基于种子）。
    /// </summary>
    public System.Random GetRandom()
    {
        return new System.Random(RandomSeed);
    }

    /// <summary>
    ///     添加源标签。
    /// </summary>
    public void AddSourceTag(string tag)
    {
        SourceTags ??= new List<string>();
        if (!SourceTags.Contains(tag)) SourceTags.Add(tag);
    }

    /// <summary>
    ///     检查源标签是否包含指定标签（支持层级匹配）。
    /// </summary>
    public bool HasSourceTag(string tag)
    {
        if (SourceTags == null) return false;
        foreach (var t in SourceTags)
        {
            if (string.Equals(t, tag, StringComparison.OrdinalIgnoreCase) ||
                t.StartsWith(tag + ".", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public bool Equals(EffectContext other) => Instigator == other.Instigator && Target == other.Target;
    public override bool Equals(object obj) => obj is EffectContext other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Instigator, Target, RandomSeed);
    public override string ToString() =>
        $"EffectContext[Instigator={Instigator?.name ?? "null"}, Target={Target?.name ?? "null"}, Skill={SourceSkillId}]";
}