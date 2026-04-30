using System.Collections.Generic;
using SkillAI;
using UnityEngine;

// ============================================================
//  EQS (目标查询系统) —— Filter / Sorter 接口 + 内置实现。
//  类似 Unreal EQS，支持可扩展的筛选和排序策略。
// ============================================================

/// <summary>
///     目标筛选上下文 —— 每次查询传递的元信息。
/// </summary>
public struct TargetQueryContext
{
    /// <summary>查询中心位置</summary>
    public Vector3 Center;

    /// <summary>施法者</summary>
    public Transform Self;

    /// <summary>技能上下文</summary>
    public SkillContext SkillCtx;

    /// <summary>筛选配置</summary>
    public TargetQueryConfig Config;
}

/// <summary>
///     目标筛选器接口 —— 策略模式，支持新增筛选规则不改核心代码。
/// </summary>
public interface ITargetFilter
{
    /// <summary>是否通过筛选</summary>
    bool Pass(Transform candidate, TargetQueryContext ctx);

    /// <summary>调试名称</summary>
    string FilterName { get; }
}

/// <summary>
///     目标排序器接口 —— 策略模式。
/// </summary>
public interface ITargetSorter
{
    /// <summary>为每个候选目标计算评分（值越小越优先）</summary>
    float Score(Transform candidate, TargetQueryContext ctx);

    /// <summary>排序方向（Ascending=值小优先 / Descending=值大优先）</summary>
    SortOrder SortDirection { get; }

    /// <summary>调试名称</summary>
    string SorterName { get; }
}

// ============================================================
//  内置 Filter 实现
// ============================================================

/// <summary>距离筛选 —— 排除超出范围的目标</summary>
public class DistanceFilter : ITargetFilter
{
    public string FilterName => "Distance";

    public bool Pass(Transform candidate, TargetQueryContext ctx)
    {
        var distSq = (candidate.position - ctx.Center).sqrMagnitude;
        return distSq <= ctx.Config.MaxRange * ctx.Config.MaxRange;
    }
}

/// <summary>Tag 筛选 —— 排除不包含指定 Tag 的目标</summary>
public class TagFilter : ITargetFilter
{
    public string[] RequiredTags;
    public bool RequireAll = true;

    public string FilterName => "Tag";

    public bool Pass(Transform candidate, TargetQueryContext ctx)
    {
        if (RequiredTags == null || RequiredTags.Length == 0) return true;

        var geHost = candidate.GetComponent<GEHost>();
        if (geHost == null) return false;

        if (RequireAll)
        {
            foreach (var tag in RequiredTags)
                if (!geHost.HasTag(tag)) return false;
            return true;
        }

        foreach (var tag in RequiredTags)
            if (geHost.HasTag(tag)) return true;
        return false;
    }
}

/// <summary>生命阈值筛选 —— 排除血量不足/过高目标</summary>
public class HPThresholdFilter : ITargetFilter
{
    public float MinHP;
    public float MaxHP = 1f;
    public bool ExcludeDead = true;

    public string FilterName => "HP";

    public bool Pass(Transform candidate, TargetQueryContext ctx)
    {
        var attr = candidate.GetComponent<AttributeSet>();
        if (attr == null) return !ExcludeDead;

        if (ExcludeDead && !attr.IsAlive) return false;

        var hp = attr.HealthPercent;
        return hp >= MinHP && hp <= MaxHP;
    }
}

/// <summary>队伍筛选 —— 只保留指定队伍</summary>
public class TeamFilter : ITargetFilter
{
    public int TeamId = -1;

    public string FilterName => "Team";

    public bool Pass(Transform candidate, TargetQueryContext ctx)
    {
        if (TeamId < 0) return true;
        var entity = candidate.GetComponent<ISpatialEntity>();
        return entity != null && entity.TeamId == TeamId;
    }
}

// ============================================================
//  内置 Sorter 实现
// ============================================================

/// <summary>按距离排序</summary>
public class ByDistanceSorter : ITargetSorter
{
    public SortOrder SortDirection { get; set; } = SortOrder.Ascending;
    public string SorterName => "ByDistance";

    public float Score(Transform candidate, TargetQueryContext ctx)
    {
        return Vector3.Distance(candidate.position, ctx.Center);
    }
}

/// <summary>按生命百分比排序</summary>
public class ByHPSorter : ITargetSorter
{
    public SortOrder SortDirection { get; set; } = SortOrder.Ascending;
    public string SorterName => "ByHP";

    public float Score(Transform candidate, TargetQueryContext ctx)
    {
        var attr = candidate.GetComponent<AttributeSet>();
        return attr != null ? attr.HealthPercent : 1f;
    }
}

/// <summary>按威胁值排序（基于 AIAlertLevel）</summary>
public class ByThreatSorter : ITargetSorter
{
    public SortOrder SortDirection { get; set; } = SortOrder.Descending;
    public string SorterName => "ByThreat";

    public float Score(Transform candidate, TargetQueryContext ctx)
    {
        var ai = candidate.GetComponent<AIController>();
        return ai != null ? (float)ai.AlertLevel : 0f;
    }
}
