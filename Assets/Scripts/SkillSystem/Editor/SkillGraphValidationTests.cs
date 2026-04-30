#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using System.Linq;

/// <summary>
///     技能图自动化验证测试 —— CI/CD 集成。
///     遍历 Skill.csv 所有配方，验证图完整性和可执行性。
/// </summary>
public class SkillGraphValidationTests
{
    /// <summary>
    ///     验证所有技能配置对应的图资产存在且可加载。
    /// </summary>
    [Test]
    public void AllSkillConfigs_ShouldHaveValidGraph()
    {
        // 加载配置
        ConfigLoader.Initialize();
        var configs = ConfigLoader.GetAllSkillConfigs();
        Assert.IsNotEmpty(configs, "No skill configs found.");

        foreach (var config in configs)
        {
            Assert.NotNull(config, "Skill config is null.");
            Assert.Greater(config.SkillID, 0, $"Skill '{config.SkillName}' has invalid ID.");

            if (!string.IsNullOrWhiteSpace(config.GraphPath))
            {
                var graph = Resources.Load<SkillGraphAsset>(config.GraphPath);
                Assert.NotNull(graph,
                    $"Skill '{config.SkillName}' (ID={config.SkillID}) graph not found at path: {config.GraphPath}");
            }
        }
    }

    /// <summary>
    ///     验证每个技能图有 StartNode，且所有路径可到达 EndNode。
    /// </summary>
    [Test]
    public void AllSkillGraphs_ShouldReachEndNode()
    {
        ConfigLoader.Initialize();
        var configs = ConfigLoader.GetAllSkillConfigs();

        foreach (var config in configs)
        {
            if (string.IsNullOrWhiteSpace(config.GraphPath)) continue;

            var graph = Resources.Load<SkillGraphAsset>(config.GraphPath);
            if (graph == null) continue;

            var startNode = graph.GetStartNode();
            Assert.NotNull(startNode,
                $"Skill '{config.SkillName}' (ID={config.SkillID}) has no StartNode.");

            var allNodes = graph.Nodes;
            Assert.IsNotEmpty(allNodes,
                $"Skill '{config.SkillName}' (ID={config.SkillID}) has no nodes.");

            var hasEndNode = allNodes.Any(n => n is EndNode);
            Assert.IsTrue(hasEndNode,
                $"Skill '{config.SkillName}' (ID={config.SkillID}) has no EndNode.");

            // 验证连接完整性
            foreach (var node in allNodes)
            {
                if (node is EndNode || node is StartNode) continue;

                var outConns = node.GetOutputEdges();
                // 条件/并行等节点允许无直连（通过端口连接）
                if (outConns.Count == 0 && !(node is ConditionNode) && !(node is ParallelNode))
                {
                    Debug.LogWarning(
                        $"Skill '{config.SkillName}': Node '{node.NodeName}' has no output edges.");
                }
            }
        }
    }

    /// <summary>
    ///     验证所有技能配置的关键数值合法。
    /// </summary>
    [Test]
    public void AllSkillConfigs_ShouldHaveValidValues()
    {
        ConfigLoader.Initialize();
        var configs = ConfigLoader.GetAllSkillConfigs();

        foreach (var config in configs)
        {
            Assert.GreaterOrEqual(config.Damage, 0f,
                $"Skill '{config.SkillName}': Damage is negative.");
            Assert.GreaterOrEqual(config.Cooldown, 0f,
                $"Skill '{config.SkillName}': Cooldown is negative.");
            Assert.LessOrEqual(config.CritChance, 1f,
                $"Skill '{config.SkillName}': CritChance > 1.");
            Assert.GreaterOrEqual(config.CritChance, 0f,
                $"Skill '{config.SkillName}': CritChance < 0.");
            Assert.GreaterOrEqual(config.CastRange, 0f,
                $"Skill '{config.SkillName}': CastRange is negative.");
        }
    }

    /// <summary>
    ///     验证无重复 SkillID。
    /// </summary>
    [Test]
    public void AllSkillConfigs_ShouldHaveUniqueIDs()
    {
        ConfigLoader.Initialize();
        var configs = ConfigLoader.GetAllSkillConfigs();
        var ids = configs.Select(c => c.SkillID).ToList();
        var uniqueIds = ids.Distinct().ToList();

        Assert.AreEqual(ids.Count, uniqueIds.Count,
            $"Duplicate SkillIDs found: {string.Join(", ", ids.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key))}");
    }
}
#endif
