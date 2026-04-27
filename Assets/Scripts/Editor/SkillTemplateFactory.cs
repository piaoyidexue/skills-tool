using System.IO;
using UnityEditor;
using UnityEngine;

public static class SkillTemplateFactory
{
    private const string GraphFolder = "Assets/Examples/Graphs";

    [MenuItem("Tools/Skills/Templates/Generate Common Skill Graphs")]
    private static void GenerateCommonSkillGraphs()
    {
        EnsureFolder("Assets/Examples");
        EnsureFolder(GraphFolder);

        var impactGraph = CreateImpactSubgraph();
        CreateBasicFireballGraph(impactGraph);
        CreateCritFireballGraph(impactGraph);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SkillTemplateFactory] Generated common skill graph templates under Assets/Examples/Graphs.");
    }

    private static SkillGraph CreateImpactSubgraph()
    {
        const string path = GraphFolder + "/Common_ImpactDamage.asset";
        var graph = LoadOrCreateGraph(path, "Common_ImpactDamage");
        graph.allNodes.Clear();

        var start = graph.AddNodeToGraph<StartNode>();
        var vfx = graph.AddNodeToGraph<PlayVFXNode>();
        var damage = graph.AddNodeToGraph<DamageNode>();
        var end = graph.AddNodeToGraph<EndNode>();

        start.position = new Vector2(80f, 160f);
        vfx.position = new Vector2(320f, 160f);
        damage.position = new Vector2(580f, 160f);
        end.position = new Vector2(840f, 160f);

        Connect(start, "output", vfx, "input");
        Connect(vfx, "output", damage, "input");
        Connect(damage, "output", end, "input");

        EditorUtility.SetDirty(graph);
        return graph;
    }

    private static void CreateBasicFireballGraph(SkillGraph impactGraph)
    {
        const string path = GraphFolder + "/Skill_Fireball.asset";
        var graph = LoadOrCreateGraph(path, "Skill_Fireball");
        graph.allNodes.Clear();

        var start = graph.AddNodeToGraph<StartNode>();
        var delay = graph.AddNodeToGraph<DelayNode>();
        var subGraph = graph.AddNodeToGraph<SubGraphNode>();
        var end = graph.AddNodeToGraph<EndNode>();

        subGraph.subGraph = impactGraph;

        start.position = new Vector2(80f, 160f);
        delay.position = new Vector2(320f, 160f);
        subGraph.position = new Vector2(580f, 160f);
        end.position = new Vector2(840f, 160f);

        Connect(start, "output", delay, "input");
        Connect(delay, "output", subGraph, "input");
        Connect(subGraph, "output", end, "input");

        EditorUtility.SetDirty(graph);
    }

    private static void CreateCritFireballGraph(SkillGraph impactGraph)
    {
        const string path = GraphFolder + "/Skill_CritFireball.asset";
        var graph = LoadOrCreateGraph(path, "Skill_CritFireball");
        graph.allNodes.Clear();

        var start = graph.AddNodeToGraph<StartNode>();
        var rollChance = graph.AddNodeToGraph<RollChanceNode>();
        var condition = graph.AddNodeToGraph<ConditionNode>();
        var normalDamage = graph.AddNodeToGraph<SubGraphNode>();
        var critDamage = graph.AddNodeToGraph<DamageNode>();
        var critVfx = graph.AddNodeToGraph<PlayVFXNode>();
        var end = graph.AddNodeToGraph<EndNode>();

        rollChance.outputKey = BBKey.IsCrit;
        condition.mode = ConditionMode.BlackboardBool;
        condition.bbKey = BBKey.IsCrit;

        normalDamage.subGraph = impactGraph;

        critDamage.damageAmount.Source = FloatBinding.SourceType.Blackboard;
        critDamage.damageAmount.BlackboardKey = BBKey.DamageOverride;
        critDamage.damageAmount.DefaultValue = 0f;
        critDamage.multiplyByDamageRate = false;

        critVfx.vfxKey.Source = StringBinding.SourceType.SkillConfigField;
        critVfx.vfxKey.SkillConfigFieldName = nameof(SkillConfig.BeamVFXKey);

        var boostDamage = graph.AddNodeToGraph<ModifyFloatNode>();
        boostDamage.outputKey = BBKey.DamageOverride;
        boostDamage.inputValue.Source = FloatBinding.SourceType.SkillConfig;
        boostDamage.inputValue.SkillField = SkillFloatField.Damage;
        boostDamage.multiplier.Source = FloatBinding.SourceType.Literal;
        boostDamage.multiplier.LiteralValue = 2f;

        start.position = new Vector2(60f, 220f);
        rollChance.position = new Vector2(260f, 220f);
        condition.position = new Vector2(500f, 220f);
        normalDamage.position = new Vector2(760f, 120f);
        boostDamage.position = new Vector2(760f, 320f);
        critVfx.position = new Vector2(1040f, 280f);
        critDamage.position = new Vector2(1300f, 280f);
        end.position = new Vector2(1560f, 220f);

        Connect(start, "output", rollChance, "input");
        Connect(rollChance, "output", condition, "input");
        Connect(condition, "truePort", boostDamage, "input");
        Connect(condition, "falsePort", normalDamage, "input");
        Connect(boostDamage, "output", critVfx, "input");
        Connect(critVfx, "output", critDamage, "input");
        Connect(normalDamage, "output", end, "input");
        Connect(critDamage, "output", end, "input");

        EditorUtility.SetDirty(graph);
    }

    private static void Connect(SkillNode fromNode, string fromPort, SkillNode toNode, string toPort)
    {
        if (fromNode == null || toNode == null) return;
        var conn = SkillConnection.Create(fromNode, toNode, fromPort);
        if (conn != null)
        {
            conn.portName = fromPort;
        }
    }

    private static SkillGraph LoadOrCreateGraph(string path, string assetName)
    {
        var graph = AssetDatabase.LoadAssetAtPath<SkillGraph>(path);
        if (graph != null)
        {
            graph.name = assetName;
            return graph;
        }

        graph = ScriptableObject.CreateInstance<SkillGraph>();
        graph.name = assetName;
        AssetDatabase.CreateAsset(graph, path);
        return graph;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        var parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
        var name = Path.GetFileName(folderPath);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}