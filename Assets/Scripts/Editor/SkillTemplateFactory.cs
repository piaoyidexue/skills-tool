using System.IO;
using UnityEditor;
using UnityEngine;

public static class  SkillTemplateFactory
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

    private static SkillGraphAsset CreateImpactSubgraph()
    {
        const string path = GraphFolder + "/Common_ImpactDamage.asset";
        var graph = LoadOrCreateGraph(path, "Common_ImpactDamage");
        graph.Clear();

        var start = graph.AddNode<StartNode>();
        var vfx = graph.AddNode<PlayVFXNode>();
        var damage = graph.AddNode<DamageNode>();
        var end = graph.AddNode<EndNode>();

        start.Position = new Vector2(80f, 160f);
        vfx.Position = new Vector2(320f, 160f);
        damage.Position = new Vector2(580f, 160f);
        end.Position = new Vector2(840f, 160f);

        Connect(start, "output", vfx, "input");
        Connect(vfx, "output", damage, "input");
        Connect(damage, "output", end, "input");

        EditorUtility.SetDirty(graph);
        return graph;
    }

    private static void CreateBasicFireballGraph(SkillGraphAsset impactGraph)
    {
        const string path = GraphFolder + "/Skill_Fireball.asset";
        var graph = LoadOrCreateGraph(path, "Skill_Fireball");
        graph.Clear();

        var start = graph.AddNode<StartNode>();
        var delay = graph.AddNode<DelayNode>();
        var subGraph = graph.AddNode<SubGraphNode>();
        var end = graph.AddNode<EndNode>();

        subGraph.subGraph = impactGraph;

        start.Position = new Vector2(80f, 160f);
        delay.Position = new Vector2(320f, 160f);
        subGraph.Position = new Vector2(580f, 160f);
        end.Position = new Vector2(840f, 160f);

        Connect(start, "output", delay, "input");
        Connect(delay, "output", subGraph, "input");
        Connect(subGraph, "output", end, "input");

        EditorUtility.SetDirty(graph);
    }

    private static void CreateCritFireballGraph(SkillGraphAsset impactGraph)
    {
        const string path = GraphFolder + "/Skill_CritFireball.asset";
        var graph = LoadOrCreateGraph(path, "Skill_CritFireball");
        graph.Clear();

        var start = graph.AddNode<StartNode>();
        var rollChance = graph.AddNode<RollChanceNode>();
        var condition = graph.AddNode<ConditionNode>();
        var normalDamage = graph.AddNode<SubGraphNode>();
        var critDamage = graph.AddNode<DamageNode>();
        var critVfx = graph.AddNode<PlayVFXNode>();
        var end = graph.AddNode<EndNode>();

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

        var boostDamage = graph.AddNode<ModifyFloatNode>();
        boostDamage.outputKey = BBKey.DamageOverride;
        boostDamage.inputValue.Source = FloatBinding.SourceType.SkillConfig;
        boostDamage.inputValue.SkillField = SkillFloatField.Damage;
        boostDamage.multiplier.Source = FloatBinding.SourceType.Literal;
        boostDamage.multiplier.LiteralValue = 2f;

        start.Position = new Vector2(60f, 220f);
        rollChance.Position = new Vector2(260f, 220f);
        condition.Position = new Vector2(500f, 220f);
        normalDamage.Position = new Vector2(760f, 120f);
        boostDamage.Position = new Vector2(760f, 320f);
        critVfx.Position = new Vector2(1040f, 280f);
        critDamage.Position = new Vector2(1300f, 280f);
        end.Position = new Vector2(1560f, 220f);

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

    private static void Connect(SkillNodeBase fromNode, string fromPort, SkillNodeBase toNode, string toPort)
    {
        if (fromNode == null || toNode == null) return;
        var graph = fromNode.OwningGraph;
        if (graph == null) return;
        graph.AddEdge(new SkillEdge(fromNode.NodeGuid, fromPort, toNode.NodeGuid, toPort));
    }

    private static SkillGraphAsset LoadOrCreateGraph(string path, string assetName)
    {
        var graph = AssetDatabase.LoadAssetAtPath<SkillGraphAsset>(path);
        if (graph != null)
        {
            graph.name = assetName;
            return graph;
        }

        graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
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