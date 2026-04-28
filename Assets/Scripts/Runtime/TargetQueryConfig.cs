using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     目标查询配置 —— 定义复杂索敌规则。
///     可配在 CSV 或用 ScriptableObject 存储。
///     由 TargetQueryNode 在运行时执行查询。
/// </summary>
[System.Serializable]
public class TargetQueryConfig
{
    /// <summary>查询 ID（用于调试和引用）</summary>
    public string QueryId;

    /// <summary>查询半径</summary>
    public float MaxRange = 10f;

    /// <summary>需要的 Gameplay Tag（如 "Team.Enemy", "Status.Burn"）</summary>
    public string[] RequiredTags;

    /// <summary>排除死亡目标</summary>
    public bool ExcludeDead = true;

    /// <summary>距离权重（正=优先选近的）</summary>
    public float DistanceWeight = 1f;

    /// <summary>血量权重（正=优先选低血量）</summary>
    public float HealthWeight;

    /// <summary>最大返回数量</summary>
    public int MaxResults = 1;

    /// <summary>排序方向</summary>
    public SortOrder SortOrder;

    /// <summary>队伍过滤（-1=不过滤）</summary>
    public int TeamFilter = -1;

    /// <summary>
    ///     执行查询并返回结果列表。
    /// </summary>
    public List<Transform> Execute(Vector3 center, Transform self, SkillContext ctx)
    {
        var results = new List<Transform>();
        var grid = SpatialHashGrid.Instance;
        var spatialResults = new List<ISpatialEntity>(MaxResults * 2);

        if (grid != null)
        {
            grid.QueryRange(center, MaxRange, spatialResults, TeamFilter,
                excludeEntityId: GetSelfEntityId(self));
        }
        else
        {
            // Fallback: Physics 查询
            var colliders = Physics.OverlapSphere(center, MaxRange);
            foreach (var col in colliders)
            {
                var entity = col.GetComponent<ISpatialEntity>();
                if (entity != null && entity.IsActive)
                    spatialResults.Add(entity);
            }
        }

        // 过滤
        var candidates = new List<(Transform transform, float score)>();
        foreach (var entity in spatialResults)
        {
            var entityTransform = (entity as MonoBehaviour)?.transform;
            if (entityTransform == null) continue;

            // Tag 过滤
            if (RequiredTags != null && RequiredTags.Length > 0)
            {
                var geHost = entityTransform.GetComponent<GEHost>();
                var match = true;
                foreach (var tag in RequiredTags)
                {
                    if (geHost == null || !geHost.HasTag(tag))
                    {
                        match = false;
                        break;
                    }
                }

                if (!match) continue;
            }

            // 排除死亡
            if (ExcludeDead)
            {
                var health = entityTransform.GetComponent<IDamageable>();
                if (health == null) continue;
            }

            // 评分
            var dist = Vector3.Distance(center, entityTransform.position);
            var score = dist * DistanceWeight;

            // 血量权重（越小越好）
            if (HealthWeight > 0f)
            {
                score += HealthWeight * 100f; // 简化：低血加分
            }

            candidates.Add((entityTransform, score));
        }

        // 排序
        candidates.Sort((a, b) =>
            SortOrder == SortOrder.Ascending
                ? a.score.CompareTo(b.score)
                : b.score.CompareTo(a.score));

        // 截取结果
        for (var i = 0; i < Mathf.Min(candidates.Count, MaxResults); i++)
        {
            results.Add(candidates[i].transform);
        }

        return results;
    }

    private int GetSelfEntityId(Transform self)
    {
        if (self == null) return -1;
        var entity = self.GetComponent<ISpatialEntity>();
        return entity?.EntityId ?? -1;
    }
}

/// <summary>
///     排序方向
/// </summary>
public enum SortOrder
{
    /// <summary>升序（距离最近/血量最低优先）</summary>
    Ascending,
    /// <summary>降序（距离最远/血量最高优先）</summary>
    Descending
}
