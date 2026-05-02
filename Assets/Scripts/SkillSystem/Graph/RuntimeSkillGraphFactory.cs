using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  运行时技能图工厂 (RuntimeSkillGraphFactory)
//  为没有 .asset 技能图的技能 ID 动态生成可执行的 SkillGraphAsset。
//  生成流程：Start → Delay → ApplyEffect → PlayVFX → End
//
//  注意：运行时创建的 ScriptableObject 不会持久化，
//  仅用于测试和原型验证，生产环境应使用编辑器创建的资产。
// ============================================================

/// <summary>
///     运行时技能图工厂。
///     为指定技能 ID 动态生成最简技能图。
///     图结构：Start → Delay → ApplyEffect → PlayVFX → End
/// </summary>
public static class RuntimeSkillGraphFactory
{
    /// <summary>
    ///     缓存已生成的图（避免重复创建）。
    ///     Key: skillId, Value: 生成的 SkillGraphAsset
    /// </summary>
    private static readonly Dictionary<int, SkillGraphAsset> Cache = new();

    /// <summary>
    ///     获取或创建指定技能的运行时技能图。
    ///     如果该技能已有持久化资产，优先返回资产。
    /// </summary>
    public static SkillGraphAsset GetOrCreate(int skillId)
    {
        // 检查缓存
        if (Cache.TryGetValue(skillId, out var cached))
            return cached;

        // 尝试从 Resources 加载已有资产
        var config = ConfigLoader.GetSkillConfig(skillId);
        if (config != null && !string.IsNullOrWhiteSpace(config.GraphPath))
        {
            var existing = Resources.Load<SkillGraphAsset>(config.GraphPath);
            if (existing != null)
            {
                Cache[skillId] = existing;
                return existing;
            }
        }

        // 动态创建
        var graph = CreateGraph(skillId, config);
        Cache[skillId] = graph;
        Debug.Log($"[RuntimeSkillGraphFactory] 已为技能 {skillId} 生成运行时图: {graph.name}");
        return graph;
    }

    /// <summary>
    ///     创建最简技能图：Start → Delay → ApplyEffect → PlayVFX → End
    /// </summary>
    private static SkillGraphAsset CreateGraph(int skillId, SkillConfig config)
    {
        var graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
        graph.name = $"RuntimeGraph_{skillId}";

        // 创建节点
        var startNode = AddNode<StartNode>(graph, "Start");
        var delayNode = AddNode<DelayNode>(graph, "Delay");
        var effectNode = AddNode<ApplyEffectNode>(graph, "ApplyEffect");
        var vfxNode = AddNode<PlayVFXNode>(graph, "PlayVFX");
        var endNode = AddNode<EndNode>(graph, "End");

        // 配置 Delay 节点
        if (config != null)
        {
            delayNode.delaySeconds = new FloatBinding
            {
                Source = FloatBinding.SourceType.SkillConfig,
                SkillField = SkillFloatField.DelaySeconds,
                DefaultValue = config.DelaySeconds > 0 ? config.DelaySeconds : 0.1f
            };
        }

        // 配置 ApplyEffect 节点：从 CSV 查找 GameplayEffectData
        var geData = FindMatchingEffectData(skillId);
        if (geData != null)
        {
            effectNode.EffectId = geData.EffectId;
            effectNode.EffectData = geData;

            // 透传元素标签（让 DamagePipeline 触发元素反应）
            if (geData.GrantedTags != null && geData.GrantedTags.Count > 0)
            {
                effectNode.extraTags = string.Join(";", geData.GrantedTags);
            }
        }
        else
        {
            // 兜底：使用技能 ID 作为 Effect ID
            effectNode.EffectId = skillId;
        }

        // 配置 VFX 节点：从 SkillConfig 获取 impact_vfx
        if (config != null)
        {
            vfxNode.vfxKey = new StringBinding
            {
                Source = StringBinding.SourceType.SkillConfigField,
                SkillConfigFieldName = nameof(SkillConfig.ImpactVFXKey)
            };
            vfxNode.stage = PlayVFXNode.VFXStage.Impact;
        }

        // 连线：Start → Delay → ApplyEffect → PlayVFX → End
        graph.AddEdge(startNode.NodeGuid, "output", delayNode.NodeGuid, "input");
        graph.AddEdge(delayNode.NodeGuid, "output", effectNode.NodeGuid, "input");
        graph.AddEdge(effectNode.NodeGuid, "output", vfxNode.NodeGuid, "input");
        graph.AddEdge(vfxNode.NodeGuid, "output", endNode.NodeGuid, "input");

        return graph;
    }

    private static T AddNode<T>(SkillGraphAsset graph, string displayName) where T : SkillNodeBase
    {
        var node = ScriptableObject.CreateInstance<T>();
        node.NodeGuid = System.Guid.NewGuid().ToString("N");
        node.OwningGraph = graph;
        node.name = displayName;
        graph.Nodes?.ToList()?.Add(node); // 运行时添加（非序列化）
        return node;
    }

    /// <summary>
    ///     根据 skillId 查找匹配的 GameplayEffectData。
    ///     优先精确匹配，其次按范围匹配。
    /// </summary>
    private static GameplayEffectData FindMatchingEffectData(int skillId)
    {
        // 精确匹配
        var exact = ConfigLoader.GetGameplayEffectData(skillId);
        if (exact != null) return exact;

        // 范围匹配：查找同系列的第一个 Effect（如 10001 → 10001）
        var all = ConfigLoader.GetAllGameplayEffectDatas();
        if (all == null) return null;

        var prefix = (skillId / 1000) * 1000; // 如 10001 → 10000
        foreach (var data in all)
        {
            if (data.EffectId >= prefix && data.EffectId < prefix + 1000)
                return data;
        }

        return null;
    }

    /// <summary>
    ///     清空缓存（场景切换时调用）。
    /// </summary>
    public static void ClearCache()
    {
        Cache.Clear();
    }
}

/// <summary>
///     List 扩展方法（避免额外 using）。
/// </summary>
internal static class ListExt
{
    public static void Add<T>(this IReadOnlyList<T> list, T item)
    {
        if (list is System.Collections.Generic.List<T> concrete)
            concrete.Add(item);
    }

    public static System.Collections.Generic.List<T> ToList<T>(this IReadOnlyList<T> list)
    {
        var result = new System.Collections.Generic.List<T>(list.Count);
        for (var i = 0; i < list.Count; i++)
            result.Add(list[i]);
        return result;
    }
}
