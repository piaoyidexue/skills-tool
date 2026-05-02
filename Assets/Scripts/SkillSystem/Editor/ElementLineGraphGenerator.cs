using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// ============================================================
//  ElementLineGraphGenerator —— 统一图生成器
//  整合两种生成模式：
//  1. Preset 驱动：从 GraphPreset 模板一键生成（策划交互式）
//  2. Recipe 驱动：从 SkillRecipe.csv 批量生成（生产线自动化）
//  同时管理 Common 公共子图资产。
// ============================================================

/// <summary>
///     元素阵线统一图生成器 —— 支持预设模板生成 + CSV 批量生成。
/// </summary>
public static class ElementLineGraphGenerator
{
    // ---- 路径常量 ----
    private const string RecipeCsvPath = "Assets/Resources/Config/SkillRecipe.csv";
    public const string ResourceGraphRootFolder = "Assets/Resources/SkillGraphs/ElementLine";
    private const string CommonFolder = ResourceGraphRootFolder + "/Common";
    private const string RecipeFolder = ResourceGraphRootFolder + "/Recipes";

    // ============================================================
    //  模式1：Preset 驱动生成（维度3 - 交互式）
    // ============================================================

    /// <summary>
    ///     根据预设生成完整的 SkillGraphAsset。
    /// </summary>
    public static SkillGraphAsset Generate(GraphPreset preset, int skillId = 0, string assetPath = "")
    {
        if (preset == null)
        {
            Debug.LogError("[GraphGenerator] Preset is null.");
            return null;
        }

        var graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
        if (!string.IsNullOrEmpty(assetPath))
        {
            AssetDatabase.CreateAsset(graph, assetPath);
        }

        switch (preset.PresetType)
        {
            case GraphPresetType.ImpactLane:
                GenerateImpactLane(graph, preset);
                break;
            case GraphPresetType.BeamLane:
                GenerateBeamLane(graph, preset);
                break;
            case GraphPresetType.ProjectileLane:
                GenerateProjectileLane(graph, preset);
                break;
            case GraphPresetType.AoELane:
                GenerateAoELane(graph, preset);
                break;
            case GraphPresetType.ChannelLane:
                GenerateChannelLane(graph, preset);
                break;
            case GraphPresetType.FinisherLane:
                GenerateFinisherLane(graph, preset);
                break;
            default:
                Debug.LogWarning("[GraphGenerator] Custom preset type - generating empty graph.");
                break;
        }

        EditorUtility.SetDirty(graph);
        AssetDatabase.SaveAssets();
        return graph;
    }

    // ---- 穿透模板 ----
    private static void GenerateImpactLane(SkillGraphAsset graph, GraphPreset preset)
    {
        var start = AddNode<StartNode>(graph, "Start");
        var preCast = AddNode<PreCastNode>(graph, "PreCast");
        var animWait = AddNode<AnimationEventWaitNode>(graph, "WaitHit");
        animWait.waitEvent = "OnHit";
        var applyEffect = AddNode<ApplyEffectNode>(graph, "ApplyDamage");
        var playVfx = AddNode<PlayVFXNode>(graph, "ImpactVFX");
        playVfx.stage = PlayVFXNode.VFXStage.Impact;

        SkillNodeBase beforeEnd = playVfx;

        if (preset.HasConditionBranch)
        {
            var condition = AddNode<ConditionNode>(graph, "BranchCheck");
            condition.mode = preset.BranchConditionMode;
            beforeEnd = condition;
        }

        if (preset.HasTargetQuery)
        {
            var targetQuery = AddNode<TargetQueryNode>(graph, "EQS");
            targetQuery.QueryConfig.MaxRange = preset.TargetQueryRange;
            targetQuery.QueryConfig.MaxResults = preset.TargetQueryMaxResults;
            ChainNodes(graph, start, preCast, animWait, targetQuery, applyEffect, playVfx);
        }
        else
        {
            ChainNodes(graph, start, preCast, animWait, applyEffect, playVfx);
        }

        if (preset.HasTerrainPaint && !string.IsNullOrEmpty(preset.TerrainTags))
        {
            var paintTerrain = AddNode<PaintTerrainNode>(graph, "PaintTerrain");
            paintTerrain.terrainTags = new StringBinding
                { Source = StringBinding.SourceType.Literal, LiteralValue = preset.TerrainTags };
            ChainNodes(graph, beforeEnd, paintTerrain);
            beforeEnd = paintTerrain;
        }

        if (preset.HasResonance && !string.IsNullOrEmpty(preset.ResonanceTags))
        {
            var resonance = AddNode<ResonanceNode>(graph, "Resonance");
            resonance.resonanceTags = new StringBinding
                { Source = StringBinding.SourceType.Literal, LiteralValue = preset.ResonanceTags };
            ChainNodes(graph, beforeEnd, resonance);
            beforeEnd = resonance;
        }

        var postCast = AddNode<PostCastNode>(graph, "PostCast");
        var end = AddNode<EndNode>(graph, "End");
        ChainNodes(graph, beforeEnd, postCast, end);
    }

    // ---- 射线模板 ----
    private static void GenerateBeamLane(SkillGraphAsset graph, GraphPreset preset)
    {
        var start = AddNode<StartNode>(graph, "Start");
        var preCast = AddNode<PreCastNode>(graph, "PreCast");
        var beamVfx = AddNode<PlayVFXNode>(graph, "BeamVFX");
        beamVfx.stage = PlayVFXNode.VFXStage.Beam;
        var channel = AddNode<ChannelNode>(graph, "Channel");
        var applyEffect = AddNode<ApplyEffectNode>(graph, "ApplyDamage");

        SkillNodeBase beforeEnd = applyEffect;

        if (preset.HasResonance && !string.IsNullOrEmpty(preset.ResonanceTags))
        {
            var resonance = AddNode<ResonanceNode>(graph, "Resonance");
            resonance.resonanceTags = new StringBinding
                { Source = StringBinding.SourceType.Literal, LiteralValue = preset.ResonanceTags };
            beforeEnd = resonance;
            ChainNodes(graph, applyEffect, resonance);
        }

        var postCast = AddNode<PostCastNode>(graph, "PostCast");
        var end = AddNode<EndNode>(graph, "End");

        ChainNodes(graph, start, preCast, beamVfx, channel, applyEffect);
        ChainNodes(graph, beforeEnd, postCast, end);
    }

    // ---- 投射物模板 ----
    private static void GenerateProjectileLane(SkillGraphAsset graph, GraphPreset preset)
    {
        var start = AddNode<StartNode>(graph, "Start");
        var preCast = AddNode<PreCastNode>(graph, "PreCast");
        var projectile = AddNode<ProjectileNode>(graph, "Projectile");
        projectile.waitForCompletion = true;
        var applyEffect = AddNode<ApplyEffectNode>(graph, "ApplyDamage");
        var impactVfx = AddNode<PlayVFXNode>(graph, "ImpactVFX");
        impactVfx.stage = PlayVFXNode.VFXStage.Impact;

        SkillNodeBase beforeEnd = impactVfx;

        if (preset.HasTerrainPaint && !string.IsNullOrEmpty(preset.TerrainTags))
        {
            var paintTerrain = AddNode<PaintTerrainNode>(graph, "PaintTerrain");
            paintTerrain.terrainTags = new StringBinding
                { Source = StringBinding.SourceType.Literal, LiteralValue = preset.TerrainTags };
            ChainNodes(graph, impactVfx, paintTerrain);
            beforeEnd = paintTerrain;
        }

        var postCast = AddNode<PostCastNode>(graph, "PostCast");
        var end = AddNode<EndNode>(graph, "End");

        ChainNodes(graph, start, preCast, projectile, applyEffect, impactVfx);
        ChainNodes(graph, beforeEnd, postCast, end);
    }

    // ---- AoE 范围模板 ----
    private static void GenerateAoELane(SkillGraphAsset graph, GraphPreset preset)
    {
        var start = AddNode<StartNode>(graph, "Start");
        var preCast = AddNode<PreCastNode>(graph, "PreCast");
        var targetQuery = AddNode<TargetQueryNode>(graph, "EQS");
        targetQuery.QueryConfig.MaxRange = preset.TargetQueryRange;
        targetQuery.QueryConfig.MaxResults = preset.TargetQueryMaxResults;
        var aoeVfx = AddNode<PlayVFXNode>(graph, "AoEVFX");
        aoeVfx.stage = PlayVFXNode.VFXStage.Impact;
        var applyEffect = AddNode<ApplyEffectNode>(graph, "ApplyDamage");

        SkillNodeBase beforeEnd = applyEffect;

        if (preset.HasTerrainPaint && !string.IsNullOrEmpty(preset.TerrainTags))
        {
            var paintTerrain = AddNode<PaintTerrainNode>(graph, "PaintTerrain");
            paintTerrain.terrainTags = new StringBinding
                { Source = StringBinding.SourceType.Literal, LiteralValue = preset.TerrainTags };
            ChainNodes(graph, applyEffect, paintTerrain);
            beforeEnd = paintTerrain;
        }

        var postCast = AddNode<PostCastNode>(graph, "PostCast");
        var end = AddNode<EndNode>(graph, "End");

        ChainNodes(graph, start, preCast, targetQuery, aoeVfx, applyEffect);
        ChainNodes(graph, beforeEnd, postCast, end);
    }

    // ---- 吟唱模板 ----
    private static void GenerateChannelLane(SkillGraphAsset graph, GraphPreset preset)
    {
        var start = AddNode<StartNode>(graph, "Start");
        var preCast = AddNode<PreCastNode>(graph, "PreCast");
        var channel = AddNode<ChannelNode>(graph, "Channel");
        var applyEffect = AddNode<ApplyEffectNode>(graph, "ApplyDamage");

        SkillNodeBase beforeEnd = applyEffect;

        if (preset.HasResonance && !string.IsNullOrEmpty(preset.ResonanceTags))
        {
            var resonance = AddNode<ResonanceNode>(graph, "Resonance");
            resonance.resonanceTags = new StringBinding
                { Source = StringBinding.SourceType.Literal, LiteralValue = preset.ResonanceTags };
            ChainNodes(graph, applyEffect, resonance);
            beforeEnd = resonance;
        }

        var postCast = AddNode<PostCastNode>(graph, "PostCast");
        var end = AddNode<EndNode>(graph, "End");

        ChainNodes(graph, start, preCast, channel, applyEffect);
        ChainNodes(graph, beforeEnd, postCast, end);
    }

    // ---- 终结技模板 ----
    private static void GenerateFinisherLane(SkillGraphAsset graph, GraphPreset preset)
    {
        var start = AddNode<StartNode>(graph, "Start");
        var preCast = AddNode<PreCastNode>(graph, "PreCast");
        var finisher = AddNode<FinisherStagedNode>(graph, "Finisher");
        var applyEffect = AddNode<ApplyEffectNode>(graph, "ApplyDamage");

        SkillNodeBase beforeEnd = applyEffect;

        if (preset.HasResonance && !string.IsNullOrEmpty(preset.ResonanceTags))
        {
            var resonance = AddNode<ResonanceNode>(graph, "Resonance");
            resonance.resonanceTags = new StringBinding
                { Source = StringBinding.SourceType.Literal, LiteralValue = preset.ResonanceTags };
            ChainNodes(graph, applyEffect, resonance);
            beforeEnd = resonance;
        }

        var postCast = AddNode<PostCastNode>(graph, "PostCast");
        var end = AddNode<EndNode>(graph, "End");

        ChainNodes(graph, start, preCast, finisher, applyEffect);
        ChainNodes(graph, beforeEnd, postCast, end);
    }

    // ============================================================
    //  模式2：Recipe 驱动批量生成（CSV 生产线）
    // ============================================================

    [MenuItem("Tools/Skills/Templates/根据配置生成元素阵线技能图")]
    public static void GenerateAllGraphsFromConfig()
    {
        GenerateAllGraphsInternal(true);
    }

    public static void GenerateAllGraphsSilently()
    {
        GenerateAllGraphsInternal(false);
    }

    private static void GenerateAllGraphsInternal(bool logResult)
    {
        EnsureFolder("Assets/Examples");
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/SkillGraphs");
        EnsureFolder(ResourceGraphRootFolder);
        EnsureFolder(CommonFolder);
        EnsureFolder(RecipeFolder);

        var commonGraphs = CreateCommonGraphs();
        var recipes = ReadRecipes();
        foreach (var recipe in recipes) BuildRecipeGraph(recipe, commonGraphs);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (logResult) Debug.Log($"[ElementLineGraphGenerator] 已根据配置生成 {recipes.Count} 个技能图。");
    }

    // ---- 公共子图 ----

    private static Dictionary<string, SkillGraphAsset> CreateCommonGraphs()
    {
        var result = new Dictionary<string, SkillGraphAsset>(StringComparer.OrdinalIgnoreCase);
        result["Common_ImpactDamage"] = BuildCommonImpactDamage();
        result["Common_StatusPrime"] = BuildCommonStatusPrime();
        result["Common_RowPulse"] = BuildCommonRowPulse();
        result["Common_TerrainPaint"] = BuildCommonTerrainPaint();
        result["Common_ExecuteCheck"] = BuildCommonExecuteCheck();
        return result;
    }

    /// <summary>公共子图：命中伤害</summary>
    private static SkillGraphAsset BuildCommonImpactDamage()
    {
        var graph = LoadOrCreateGraph(CommonFolder + "/Common_ImpactDamage.asset", "Common_ImpactDamage");
        ClearGraph(graph);

        var start = graph.AddNode<StartNode>();
        var vfx = graph.AddNode<PlayVFXNode>();
        var applyEffect = graph.AddNode<ApplyEffectNode>();
        var end = graph.AddNode<EndNode>();

        start.Position = new Vector2(80f, 120f);
        vfx.Position = new Vector2(280f, 120f);
        applyEffect.Position = new Vector2(520f, 120f);
        end.Position = new Vector2(760f, 120f);

        vfx.stage = PlayVFXNode.VFXStage.Impact;
        applyEffect.UseBlackboardTarget = true;

        Connect(start, "output", vfx, "input");
        Connect(vfx, "output", applyEffect, "input");
        Connect(applyEffect, "output", end, "input");
        EditorUtility.SetDirty(graph);
        return graph;
    }

    /// <summary>公共子图：状态挂载</summary>
    private static SkillGraphAsset BuildCommonStatusPrime()
    {
        var graph = LoadOrCreateGraph(CommonFolder + "/Common_StatusPrime.asset", "Common_StatusPrime");
        ClearGraph(graph);

        var start = graph.AddNode<StartNode>();
        var applyEffect = graph.AddNode<ApplyEffectNode>();
        var log = graph.AddNode<LogNode>();
        var end = graph.AddNode<EndNode>();

        start.Position = new Vector2(80f, 120f);
        applyEffect.Position = new Vector2(300f, 120f);
        log.Position = new Vector2(520f, 120f);
        end.Position = new Vector2(760f, 120f);

        applyEffect.UseBlackboardTarget = true;
        log.message.Source = StringBinding.SourceType.Literal;
        log.message.LiteralValue = "公共子图：状态挂载";

        Connect(start, "output", applyEffect, "input");
        Connect(applyEffect, "output", log, "input");
        Connect(log, "output", end, "input");
        EditorUtility.SetDirty(graph);
        return graph;
    }

    /// <summary>公共子图：行脉冲</summary>
    private static SkillGraphAsset BuildCommonRowPulse()
    {
        var graph = LoadOrCreateGraph(CommonFolder + "/Common_RowPulse.asset", "Common_RowPulse");
        ClearGraph(graph);

        var start = graph.AddNode<StartNode>();
        var resonance = graph.AddNode<ResonanceNode>();
        var vfx = graph.AddNode<PlayVFXNode>();
        var end = graph.AddNode<EndNode>();

        start.Position = new Vector2(80f, 120f);
        resonance.Position = new Vector2(280f, 120f);
        vfx.Position = new Vector2(520f, 120f);
        end.Position = new Vector2(760f, 120f);

        resonance.resonanceTags.Source = StringBinding.SourceType.Literal;
        resonance.resonanceTags.LiteralValue = "row_resonance";
        vfx.stage = PlayVFXNode.VFXStage.Beam;

        Connect(start, "output", resonance, "input");
        Connect(resonance, "output", vfx, "input");
        Connect(vfx, "output", end, "input");
        EditorUtility.SetDirty(graph);
        return graph;
    }

    /// <summary>公共子图：地形涂绘</summary>
    private static SkillGraphAsset BuildCommonTerrainPaint()
    {
        var graph = LoadOrCreateGraph(CommonFolder + "/Common_TerrainPaint.asset", "Common_TerrainPaint");
        ClearGraph(graph);

        var start = graph.AddNode<StartNode>();
        var terrain = graph.AddNode<PaintTerrainNode>();
        var end = graph.AddNode<EndNode>();

        start.Position = new Vector2(80f, 120f);
        terrain.Position = new Vector2(320f, 120f);
        end.Position = new Vector2(560f, 120f);

        terrain.terrainTags.Source = StringBinding.SourceType.Literal;
        terrain.terrainTags.LiteralValue = "scorch";

        Connect(start, "output", terrain, "input");
        Connect(terrain, "output", end, "input");
        EditorUtility.SetDirty(graph);
        return graph;
    }

    /// <summary>公共子图：处决校验</summary>
    private static SkillGraphAsset BuildCommonExecuteCheck()
    {
        var graph = LoadOrCreateGraph(CommonFolder + "/Common_ExecuteCheck.asset", "Common_ExecuteCheck");
        ClearGraph(graph);

        var start = graph.AddNode<StartNode>();
        var condition = graph.AddNode<ConditionNode>();
        var applyEffect = graph.AddNode<ApplyEffectNode>();
        var end = graph.AddNode<EndNode>();

        start.Position = new Vector2(80f, 120f);
        condition.Position = new Vector2(320f, 120f);
        applyEffect.Position = new Vector2(580f, 60f);
        end.Position = new Vector2(820f, 120f);

        condition.mode = ConditionMode.Random;
        condition.threshold.Source = FloatBinding.SourceType.SkillConfig;
        condition.threshold.SkillField = SkillFloatField.CritChance;
        applyEffect.UseBlackboardTarget = true;

        Connect(start, "output", condition, "input");
        Connect(condition, "truePort", applyEffect, "input");
        Connect(applyEffect, "output", end, "input");
        Connect(condition, "falsePort", end, "input");
        EditorUtility.SetDirty(graph);
        return graph;
    }

    // ---- Recipe 图生成 ----

    private static void BuildRecipeGraph(RecipeRow row, IReadOnlyDictionary<string, SkillGraphAsset> commonGraphs)
    {
        var safeName = SanitizeFileName(row.RecipeId + "_" + row.Recipe);
        var graph = LoadOrCreateGraph($"{RecipeFolder}/{safeName}.asset", $"Skill_{row.Name}");
        ClearGraph(graph);

        var start = graph.AddNode<StartNode>();
        start.Position = new Vector2(80f, 220f);

        var cursorX = 280f;

        // 日志节点
        var log = default(LogNode);
        if (!string.IsNullOrWhiteSpace(row.Name))
        {
            log = graph.AddNode<LogNode>();
            log.Position = new Vector2(cursorX, 100f);
            log.message.Source = StringBinding.SourceType.Literal;
            log.message.LiteralValue = BuildSummary(row);
            cursorX += 220f;
        }

        // PreCastVFX
        var castVfx = CreateVfxNode(graph, row, cursorX, 220f, PlayVFXNode.VFXStage.Cast, false);
        cursorX += 240f;

        // Delay
        var delay = CreateDelayNode(graph, cursorX, 220f);
        cursorX += 220f;

        // ApplyEffectNode
        var applyEffect = graph.AddNode<ApplyEffectNode>();
        applyEffect.Position = new Vector2(cursorX, 220f);
        applyEffect.EffectId = row.EffectId > 0 ? row.EffectId : 0;
        applyEffect.UseBlackboardTarget = true;
        cursorX += 280f;

        // ImpactVFX
        var impactVfx = CreateVfxNode(graph, row, cursorX, 220f,
            UsesBeamVfx(row) ? PlayVFXNode.VFXStage.Beam : PlayVFXNode.VFXStage.Impact,
            UsesBeamVfx(row));
        cursorX += 240f;

        // TerrainVFX + Terrain
        var terrainVfx = CreateVfxNode(graph, row, cursorX, 220f, PlayVFXNode.VFXStage.Terrain, false);
        cursorX += 240f;
        var terrain = CreateTerrainNode(graph, row, cursorX, 220f);
        cursorX += 240f;

        // PostCastVFX
        var postCast = CreatePostCastNode(graph, cursorX, 220f);
        cursorX += 220f;

        var end = graph.AddNode<EndNode>();
        end.Position = new Vector2(cursorX, 220f);

        // 连线
        SkillNodeBase entry = start;
        if (log != null)
        {
            Connect(entry, "output", log, "input");
            entry = log;
        }

        Connect(entry, "output", castVfx, "input");
        Connect(castVfx, "output", delay, "input");
        Connect(delay, "output", applyEffect, "input");
        Connect(applyEffect, "output", impactVfx, "input");
        Connect(impactVfx, "output", terrainVfx, "input");
        Connect(terrainVfx, "output", terrain, "input");
        Connect(terrain, "output", postCast, "input");
        Connect(postCast, "output", end, "input");

        EditorUtility.SetDirty(graph);
    }

    // ============================================================
    //  节点工厂辅助
    // ============================================================

    private static T AddNode<T>(SkillGraphAsset graph, string displayName) where T : SkillNodeBase
    {
        var node = graph.AddNode<T>();
        node.name = displayName;
        return node;
    }

    private static void ChainNodes(SkillGraphAsset graph, params SkillNodeBase[] nodes)
    {
        for (int i = 0; i < nodes.Length - 1; i++)
        {
            graph.AddEdge(nodes[i].NodeGuid, "output", nodes[i + 1].NodeGuid, "input");
        }
    }

    private static void Connect(SkillNodeBase fromNode, string fromPort, SkillNodeBase toNode, string toPort)
    {
        if (fromNode == null || toNode == null) return;
        var graph = fromNode.OwningGraph;
        if (graph == null) return;
        graph.AddEdge(new SkillEdge(fromNode.NodeGuid, fromPort, toNode.NodeGuid, toPort));
    }

    private static DelayNode CreateDelayNode(SkillGraphAsset graph, float x, float y)
    {
        var delay = graph.AddNode<DelayNode>();
        delay.Position = new Vector2(x, y);
        delay.delaySeconds.Source = FloatBinding.SourceType.SkillConfig;
        delay.delaySeconds.SkillField = SkillFloatField.DelaySeconds;
        delay.delaySeconds.DefaultValue = 0f;
        return delay;
    }

    private static PostCastNode CreatePostCastNode(SkillGraphAsset graph, float x, float y)
    {
        var node = graph.AddNode<PostCastNode>();
        node.Position = new Vector2(x, y);
        node.postCastTime.Source = FloatBinding.SourceType.SkillConfig;
        node.postCastTime.SkillField = SkillFloatField.PostCastTime;
        return node;
    }

    private static PlayVFXNode CreateVfxNode(SkillGraphAsset graph, RecipeRow row, float x, float y,
        PlayVFXNode.VFXStage stage, bool preferBeam)
    {
        var node = graph.AddNode<PlayVFXNode>();
        node.Position = new Vector2(x, y);
        node.stage = stage;
        node.parentBinding = PlayVFXNode.TransformBinding.World;
        node.directionMode = stage == PlayVFXNode.VFXStage.Terrain ? PlayVFXNode.DirectionMode.CustomDirection : PlayVFXNode.DirectionMode.CasterToTarget;
        if (stage == PlayVFXNode.VFXStage.Terrain) node.customDirection = Vector3.up;
        node.lengthOverride.Source = FloatBinding.SourceType.SkillConfig;
        node.lengthOverride.SkillField = preferBeam || stage == PlayVFXNode.VFXStage.Beam ? SkillFloatField.CastRange : SkillFloatField.Radius;
        node.durationOverride.Source = FloatBinding.SourceType.SkillConfig;
        node.durationOverride.SkillField = SkillFloatField.VFXDuration;
        node.scaleMultiplier.Source = FloatBinding.SourceType.Literal;
        node.scaleMultiplier.LiteralValue = stage == PlayVFXNode.VFXStage.Cast ? 0.85f : 1f;
        return node;
    }

    private static PaintTerrainNode CreateTerrainNode(SkillGraphAsset graph, RecipeRow row, float x, float y)
    {
        var node = graph.AddNode<PaintTerrainNode>();
        node.Position = new Vector2(x, y);
        node.terrainTags.Source = StringBinding.SourceType.Literal;
        node.terrainTags.LiteralValue = row.TerrainTags;
        return node;
    }

    private static bool UsesBeamVfx(RecipeRow row)
    {
        return row.AttackPattern.Contains("射线") ||
               row.AttackPattern.Contains("雷链") ||
               row.AttackPattern.Contains("墙") ||
               row.NodePresetId == "Preset_BeamLane" ||
               row.NodePresetId == "Preset_ConductiveChain" ||
               row.NodePresetId == "Preset_RowResonance";
    }

    private static string BuildSummary(RecipeRow row)
    {
        return $"配方 {row.Recipe}：{row.Name} | 协同={row.SynergyLogic} | 质变={row.RuleMutation}";
    }

    // ============================================================
    //  图资产持久化
    // ============================================================

    private static SkillGraphAsset LoadOrCreateGraph(string path, string graphName)
    {
        var graph = AssetDatabase.LoadAssetAtPath<SkillGraphAsset>(path);
        if (graph != null)
        {
            graph.name = graphName;
            return graph;
        }

        graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
        graph.name = graphName;
        AssetDatabase.CreateAsset(graph, path);
        return graph;
    }

    private static void ClearGraph(SkillGraphAsset graph)
    {
        graph.Clear();
        EditorUtility.SetDirty(graph);
    }

    // ============================================================
    //  CSV 读取
    // ============================================================

    private static List<RecipeRow> ReadRecipes()
    {
        var text = AssetDatabase.LoadAssetAtPath<TextAsset>(RecipeCsvPath);
        var result = new List<RecipeRow>();
        if (text == null) return result;

        var lines = text.text.Replace("\r", string.Empty).Split('\n');
        if (lines.Length <= 1) return result;

        var headers = SplitCsvLine(lines[0]);
        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            var values = SplitCsvLine(lines[i]);
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < headers.Count; c++) row[headers[c]] = c < values.Count ? values[c] : string.Empty;

            result.Add(new RecipeRow
            {
                RecipeId = GetValue(row, "recipe_id"),
                SlotCount = int.TryParse(GetValue(row, "slot_count"), out var slotCount) ? slotCount : 2,
                Core = GetValue(row, "core"),
                Recipe = GetValue(row, "recipe"),
                Name = GetValue(row, "name"),
                CombatRole = GetValue(row, "combat_role"),
                AttackPattern = GetValue(row, "attack_pattern"),
                SynergyLogic = GetValue(row, "synergy_logic"),
                RuleMutation = GetValue(row, "rule_mutation"),
                StatusTags = GetValue(row, "status_tags"),
                TerrainTags = GetValue(row, "terrain_tags"),
                ResonanceTags = GetValue(row, "resonance_tags"),
                NodePresetId = GetValue(row, "node_preset_id"),
                GraphTemplateId = GetValue(row, "graph_template_id"),
                Notes = GetValue(row, "notes"),
                EffectId = int.TryParse(GetValue(row, "effect_id"), out var eid) ? eid : 0
            });
        }

        return result;
    }

    private static string GetValue(IReadOnlyDictionary<string, string> row, string key)
    {
        return row.TryGetValue(key, out var value) ? value : string.Empty;
    }

    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        var current = string.Empty;
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"') { inQuotes = !inQuotes; continue; }
            if (ch == ',' && !inQuotes)
            {
                result.Add(current.Trim());
                current = string.Empty;
                continue;
            }
            current += ch;
        }

        result.Add(current.Trim());
        return result;
    }

    // ============================================================
    //  文件/文件夹工具
    // ============================================================

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

    private static string SanitizeFileName(string value)
    {
        foreach (var ch in Path.GetInvalidFileNameChars()) value = value.Replace(ch, '_');
        return value.Replace("|", "_").Replace(" ", "_");
    }

    // ============================================================
    //  RecipeRow 数据类
    // ============================================================

    private sealed class RecipeRow
    {
        public string Core;
        public string AttackPattern;
        public string CombatRole;
        public string GraphTemplateId;
        public string Name;
        public string NodePresetId;
        public string Notes;
        public string Recipe;
        public string RecipeId;
        public string ResonanceTags;
        public string RuleMutation;
        public int SlotCount;
        public string StatusTags;
        public string TerrainTags;
        public string SynergyLogic;
        /// <summary>GAS架构：关联的 GameplayEffectData ID</summary>
        public int EffectId;
    }
}

// ============================================================
//  编辑器菜单入口（维度3 - 交互式生成窗口）
// ============================================================

public static class GraphGeneratorMenu
{
    [MenuItem("Assets/Skill System/Generate Graph from Preset...", false, 101)]
    private static void ShowGeneratorWindow()
    {
        var window = GraphGeneratorWindow.Create();
        window.Show();
    }
}

/// <summary>
///     图生成器窗口 —— 提供可视化配置界面。
/// </summary>
public class GraphGeneratorWindow : EditorWindow
{
    private GraphPresetType _presetType = GraphPresetType.ImpactLane;
    private GraphPreset _preset;
    private string _assetName = "NewSkillGraph";
    private string _outputPath = "Assets/Resources/SkillGraphs/";

    public static GraphGeneratorWindow Create()
    {
        var window = GetWindow<GraphGeneratorWindow>("图模板生成器");
        window.minSize = new Vector2(350, 500);
        return window;
    }

    private void OnEnable()
    {
        _preset = GraphPreset.GetDefault(_presetType);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("元素阵线 - 技能图模板生成器", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        // 预设选择
        EditorGUI.BeginChangeCheck();
        _presetType = (GraphPresetType)EditorGUILayout.EnumPopup("预设类型", _presetType);
        if (EditorGUI.EndChangeCheck())
        {
            _preset = GraphPreset.GetDefault(_presetType);
        }

        EditorGUILayout.Space(5);

        // 输出配置
        _assetName = EditorGUILayout.TextField("资产名称", _assetName);
        _outputPath = EditorGUILayout.TextField("输出路径", _outputPath);

        EditorGUILayout.Space(10);

        // 预设参数
        EditorGUILayout.LabelField("预设参数", EditorStyles.boldLabel);
        _preset.HasConditionBranch = EditorGUILayout.Toggle("条件分支", _preset.HasConditionBranch);
        if (_preset.HasConditionBranch)
        {
            _preset.BranchConditionMode =
                (ConditionMode)EditorGUILayout.EnumPopup("分支类型", _preset.BranchConditionMode);
        }

        _preset.HasTargetQuery = EditorGUILayout.Toggle("EQS目标查询", _preset.HasTargetQuery);
        if (_preset.HasTargetQuery)
        {
            _preset.TargetQueryRange = EditorGUILayout.FloatField("查询范围", _preset.TargetQueryRange);
            _preset.TargetQueryMaxResults =
                EditorGUILayout.IntField("最大目标数", _preset.TargetQueryMaxResults);
        }

        _preset.HasElementTags = EditorGUILayout.Toggle("元素标签", _preset.HasElementTags);
        if (_preset.HasElementTags)
        {
            _preset.ElementTags = EditorGUILayout.TextField("元素标签", _preset.ElementTags);
        }

        _preset.HasTerrainPaint = EditorGUILayout.Toggle("地形涂绘", _preset.HasTerrainPaint);
        if (_preset.HasTerrainPaint)
        {
            _preset.TerrainTags = EditorGUILayout.TextField("地形标签", _preset.TerrainTags);
        }

        _preset.HasResonance = EditorGUILayout.Toggle("共鸣节点", _preset.HasResonance);
        if (_preset.HasResonance)
        {
            _preset.ResonanceTags = EditorGUILayout.TextField("共鸣标签", _preset.ResonanceTags);
        }

        EditorGUILayout.Space(20);

        // 生成按钮
        if (GUILayout.Button("生成技能图", GUILayout.Height(40)))
        {
            var fullPath = $"{_outputPath}{_assetName}.asset";
            var graph = ElementLineGraphGenerator.Generate(_preset, 0, fullPath);
            if (graph != null)
            {
                EditorUtility.DisplayDialog("生成成功",
                    $"技能图已生成:\n{fullPath}\n\n节点数: {graph.Nodes.Count}\n边数: {graph.Edges.Count}",
                    "确定");
                Selection.activeObject = graph;
            }
        }

        EditorGUILayout.Space(5);

        if (GUILayout.Button("批量生成（从 SkillRecipe.csv）", GUILayout.Height(30)))
        {
            ElementLineGraphGenerator.GenerateAllGraphsFromConfig();
        }
    }
}
