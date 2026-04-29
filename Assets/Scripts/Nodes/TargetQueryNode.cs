using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     目标查询节点 —— 通过配置好的策略动态选出目标列表。
///     结果写入黑板的 BBKey.TargetList，后续 ParallelNode 遍历多发施法。
/// </summary>
public class TargetQueryNode : SkillNode
{
    /// <summary>查询配置</summary>
    public TargetQueryConfig QueryConfig = new()
    {
        QueryId = "Default",
        MaxRange = 10f,
        MaxResults = 1
    };

    /// <summary>结果数量写入黑板键</summary>
    public string countKey = BBKey.TargetCount;

    /// <summary>结果列表写入黑板键</summary>
    public string listKey = BBKey.TargetList;

    /// <summary>是否将第一个结果设为 ctx.Target（兼容旧节点）</summary>
    public bool updateContextTarget = true;

    // ---- 缓存 ----
    [System.NonSerialized] private List<Transform> _results;

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        var center = ctx.Caster != null ? ctx.Caster.position : Vector3.zero;
        _results = QueryConfig.Execute(center, ctx.Caster, ctx);

        ctx.Blackboard.SetValue(countKey, _results.Count);

        if (_results.Count > 0)
        {
            // 将结果列表写入黑板（特殊处理：List<Transform>）
            ctx.Blackboard.SetValue(listKey + "_Count", _results.Count);
            for (var i = 0; i < _results.Count; i++)
            {
                ctx.Blackboard.SetValue($"{listKey}_{i}", _results[i].gameObject.name);
            }

            if (updateContextTarget)
            {
                ctx.Target = _results[0];
            }
        }

        return NodeTickResult.Success;
    }
}
