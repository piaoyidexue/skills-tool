using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     目标查询配置 —— 定义复杂索敌规则。
///     支持可插拔 Filter / Sorter 策略，完全依赖空间哈希网格。
/// </summary>
[System.Serializable]
public class TargetQueryConfig
{
    public string QueryId;
    public float MaxRange = 10f;
    public string[] RequiredTags;
    public bool ExcludeDead = true;
    public float DistanceWeight = 1f;
    public float HealthWeight;
    public int MaxResults = 1;
    public SortOrder SortOrder;
    public int TeamFilter = -1;

    // ---- 可插拔 Filter / Sorter (non-serialized, configured at runtime) ----
    [NonSerialized] public List<ITargetFilter> Filters = new();
    [NonSerialized] public List<ITargetSorter> Sorters = new();

    /// <summary>
    ///     执行查询，使用可插拔 Filter/Sorter 流水线。
    ///     如果 Filters/Sorters 为空，回退到传统模式。
    /// </summary>
    public List<Transform> Execute(Vector3 center, Transform self, SkillContext ctx)
    {
        // 如果配置了可插拔策略，使用新管线
        if (Filters.Count > 0 || Sorters.Count > 0)
            return ExecutePlugins(center, self, ctx);

        // ---- 传统模式（向后兼容） ----
        return ExecuteLegacy(center, self, ctx);
    }

    /// <summary>
    ///     可插拔管线 —— 遍历 Filter → 评分 → 排序 → 截取 Top-N。
    /// </summary>
    private List<Transform> ExecutePlugins(Vector3 center, Transform self, SkillContext ctx)
    {
        var results = new List<Transform>();
        var queryCtx = new TargetQueryContext
        {
            Center = center,
            Self = self,
            SkillCtx = ctx,
            Config = this
        };

        // 1. 空间哈希范围查询
        var grid = SpatialHashGrid.Instance;
        var spatialResults = new List<ISpatialEntity>(MaxResults * 2);

        if (grid != null)
        {
            grid.QueryRange(center, MaxRange, spatialResults, TeamFilter,
                excludeEntityId: GetSelfEntityId(self));
        }

        // 2. 收集 Transform 列表
        var candidates = new List<Transform>(spatialResults.Count);
        foreach (var entity in spatialResults)
        {
            var t = (entity as MonoBehaviour)?.transform;
            if (t != null) candidates.Add(t);
        }

        // 3. 遍历 Filter 链
        for (var i = candidates.Count - 1; i >= 0; i--)
        {
            foreach (var filter in Filters)
            {
                if (!filter.Pass(candidates[i], queryCtx))
                {
                    candidates.RemoveAt(i);
                    break;
                }
            }
        }

        // 4. 评分 + 排序
        if (Sorters.Count > 0)
        {
            // 使用第一个 Sorter（多 Sorter 扩展点：加权组合等）
            var sorter = Sorters[0];
            var scoredList = new List<(Transform t, float score)>(candidates.Count);
            foreach (var c in candidates)
            {
                var score = sorter.Score(c, queryCtx);
                scoredList.Add((c, score));
            }

            scoredList.Sort((a, b) =>
                sorter.SortDirection == SortOrder.Ascending
                    ? a.score.CompareTo(b.score)
                    : b.score.CompareTo(a.score));

            for (var i = 0; i < Mathf.Min(scoredList.Count, MaxResults); i++)
                results.Add(scoredList[i].t);
        }
        else
        {
            // 无排序器，直接截取
            for (var i = 0; i < Mathf.Min(candidates.Count, MaxResults); i++)
                results.Add(candidates[i]);
        }

        return results;
    }

    /// <summary>
    ///     传统模式 —— 保持向后兼容。
    /// </summary>
    private List<Transform> ExecuteLegacy(Vector3 center, Transform self, SkillContext ctx)
    {
        var results = new List<Transform>();
        var grid = SpatialHashGrid.Instance;
        var spatialResults = new List<ISpatialEntity>(MaxResults * 2);

        if (grid != null)
        {
            grid.QueryRange(center, MaxRange, spatialResults, TeamFilter,
                excludeEntityId: GetSelfEntityId(self));
        }

        var candidates = new List<(Transform transform, float score)>();
        foreach (var entity in spatialResults)
        {
            var entityTransform = (entity as MonoBehaviour)?.transform;
            if (entityTransform == null) continue;

            if (RequiredTags != null && RequiredTags.Length > 0)
            {
                var geHost = entityTransform.GetComponent<GEHost>();
                var match = true;
                foreach (var tag in RequiredTags)
                {
                    if (geHost == null || !geHost.HasTag(tag)) { match = false; break; }
                }
                if (!match) continue;
            }

            if (ExcludeDead)
            {
                var attr = entityTransform.GetComponent<AttributeSet>();
                if (attr != null && !attr.IsAlive) continue;
            }

            var dist = Vector3.Distance(center, entityTransform.position);
            var score = dist * DistanceWeight;
            if (HealthWeight > 0f) score += HealthWeight * 100f;

            candidates.Add((entityTransform, score));
        }

        candidates.Sort((a, b) =>
            SortOrder == SortOrder.Ascending
                ? a.score.CompareTo(b.score)
                : b.score.CompareTo(a.score));

        for (var i = 0; i < Mathf.Min(candidates.Count, MaxResults); i++)
            results.Add(candidates[i].transform);

        return results;
    }

    /// <summary>
    ///     便捷方法：选出范围内血量最低且含有指定 Tag 的 Top-N 敌人。
    /// </summary>
    /// <example>
    ///     // 选出范围内血量最低且带灼烧的3个敌人
    ///     var config = new TargetQueryConfig
    ///     {
    ///         MaxRange = 15f,
    ///         MaxResults = 3
    ///     };
    ///     config.Filters.Add(new TagFilter { RequiredTags = new[] { "tag.status.burn" } });
    ///     config.Filters.Add(new HPThresholdFilter { ExcludeDead = true });
    ///     config.Sorters.Add(new ByHPSorter { SortDirection = SortOrder.Ascending });
    /// </example>
    public static TargetQueryConfig CreateTemplate(
        float range, int maxResults,
        string[] requiredTags = null,
        bool excludeDead = true)
    {
        var config = new TargetQueryConfig
        {
            MaxRange = range,
            MaxResults = maxResults,
            ExcludeDead = excludeDead,
            RequiredTags = requiredTags
        };
        return config;
    }

    private int GetSelfEntityId(Transform self)
    {
        if (self == null) return -1;
        var entity = self.GetComponent<ISpatialEntity>();
        return entity?.EntityId ?? -1;
    }
}

/// <summary>排序方向</summary>
public enum SortOrder
{
    Ascending,
    Descending
}
