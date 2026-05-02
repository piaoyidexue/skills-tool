using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// ============================================================
//  ElementLineGraphGenerator —— 统一生成器
//  整合三部分功能：
//  1. Preset 驱动：从 GraphPreset 模板一键生成技能图（策划交互式）
//  2. Recipe 驱动：从 SkillRecipe.csv 批量生成技能图 + 运行时配置（生产线自动化）
//  3. 公共 CSV 解析：ReadRecipes / RecipeRow 供编辑器工具复用
// ============================================================

/// <summary>
///     元素阵线统一生成器 —— 支持预设模板生成 + CSV 批量生成 + 运行时配置生成。
/// </summary>
public static class ElementLineGraphGenerator
{
    // ---- 路径常量 ----
    public const string RecipeCsvPath = "Assets/Resources/Config/SkillRecipe.csv";
    private const string RuntimeSkillCsvPath = "Assets/Resources/Config/Skill.csv";
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
            graph.name = Path.GetFileNameWithoutExtension(assetPath);
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

        // 同步生成运行时 Skill.csv
        GenerateRuntimeSkillConfigSilently();

        if (logResult) Debug.Log($"[ElementLineGraphGenerator] 已根据配置生成 {recipes.Count} 个技能图 + 运行时配置。");
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
        var graph = LoadOrCreateGraph(CommonFolder + "/Common_ImpactDamage.asset");
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
        var graph = LoadOrCreateGraph(CommonFolder + "/Common_StatusPrime.asset");
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
        var graph = LoadOrCreateGraph(CommonFolder + "/Common_RowPulse.asset");
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
        var graph = LoadOrCreateGraph(CommonFolder + "/Common_TerrainPaint.asset");
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
        var graph = LoadOrCreateGraph(CommonFolder + "/Common_ExecuteCheck.asset");
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
        var graph = LoadOrCreateGraph($"{RecipeFolder}/{safeName}.asset");
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

    private static SkillGraphAsset LoadOrCreateGraph(string path)
    {
        var assetName = Path.GetFileNameWithoutExtension(path);
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

    private static void ClearGraph(SkillGraphAsset graph)
    {
        graph.Clear();
        EditorUtility.SetDirty(graph);
    }

    // ============================================================
    //  公共 CSV 解析（供编辑器工具复用）
    // ============================================================

    public static List<RecipeRow> ReadRecipes()
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

    public static string GetValue(IReadOnlyDictionary<string, string> row, string key)
    {
        return row.TryGetValue(key, out var value) ? value : string.Empty;
    }

    public static List<string> SplitCsvLine(string line)
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
    //  RecipeRow 公共数据类
    // ============================================================

    public sealed class RecipeRow
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

    // ============================================================
    //  模式3：运行时 Skill.csv 生成（从 SkillRecipe.csv 派生数值）
    // ============================================================

    [MenuItem("Tools/Skills/Templates/根据配方生成运行时技能配置")]
    public static void GenerateRuntimeSkillConfig()
    {
        GenerateRuntimeSkillConfigInternal(true);
    }

    public static void GenerateRuntimeSkillConfigSilently()
    {
        GenerateRuntimeSkillConfigInternal(false);
    }

    private static void GenerateRuntimeSkillConfigInternal(bool logResult)
    {
        var recipes = ReadRecipes();
        var csv = BuildRuntimeSkillCsv(recipes);

        File.WriteAllText(RuntimeSkillCsvPath, csv, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(RuntimeSkillCsvPath, ImportAssetOptions.ForceUpdate);

        if (logResult)
            Debug.Log($"[ElementLineGraphGenerator] 已生成 {recipes.Count} 条运行时技能配置。");
    }

    private static string BuildRuntimeSkillCsv(List<RecipeRow> recipes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("skill_id,name,graph_path,impact_vfx,beam_vfx,damage,damage_rate,cooldown,cast_range,delay_seconds,crit_chance,radius,chain_count,vfx_duration,cast_time,channel_duration,post_cast_time,interruptible,projectile_speed,projectile_prefab,projectile_trajectory,projectile_hit_radius,projectile_lifetime,projectile_gravity,projectile_tags,resource_cost,projectile_config_key");

        foreach (var recipe in recipes)
        {
            var graphPath = $"SkillGraphs/ElementLine/Recipes/{recipe.RecipeId}_{recipe.Recipe}".Replace("|", "_");

            sb.Append(recipe.RecipeId).Append(',')
                .Append(recipe.Name).Append(',')
                .Append(graphPath).Append(',')
                .Append(ResolveImpactVfx(recipe)).Append(',')
                .Append(ResolveBeamVfx(recipe)).Append(',')
                .Append(ResolveDamage(recipe).ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(ResolveDamageRate(recipe).ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(ResolveCooldown(recipe).ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(ResolveCastRange(recipe).ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(ResolveDelay(recipe).ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(ResolveCritChance(recipe).ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(ResolveRadius(recipe).ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(ResolveChainCount(recipe).ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(ResolveVfxDuration(recipe).ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(ResolveCastTime(recipe).ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(ResolveChannelDuration(recipe).ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(ResolvePostCastTime(recipe).ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(ResolveInterruptible(recipe)).Append(',')
                .Append(ResolveProjectileSpeed(recipe).ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(ResolveProjectilePrefab(recipe)).Append(',')
                .Append(ResolveProjectileTrajectory(recipe)).Append(',')
                .Append(ResolveProjectileHitRadius(recipe).ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(ResolveProjectileLifetime(recipe).ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(ResolveProjectileGravity(recipe).ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(ResolveProjectileTags(recipe)).Append(',')
                .Append(ResolveResourceCost(recipe).ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(ResolveProjectileConfigKey(recipe))
                .AppendLine();
        }

        return sb.ToString();
    }

    // ---- VFX 解析 ----

    private static string ResolveImpactVfx(RecipeRow recipe)
    {
        if (recipe.NodePresetId == "Preset_ElementCollapse" ||
            recipe.NodePresetId == "Preset_ChainUltimate" ||
            recipe.CombatRole.Contains("终极") ||
            recipe.RuleMutation.Contains("爆破") ||
            recipe.RuleMutation.Contains("清场"))
            return "ExplosionWave";

        if (recipe.Core == "Ice" || recipe.StatusTags.Contains("freeze") || recipe.TerrainTags.Contains("ice"))
            return "FrostBurst";

        return "HitSpark";
    }

    private static string ResolveBeamVfx(RecipeRow recipe)
    {
        if (!UsesBeamVfx(recipe)) return string.Empty;

        if (recipe.AttackPattern.Contains("墙") ||
            recipe.NodePresetId == "Preset_ElementCollapse" ||
            recipe.Recipe.Contains("AC-FI-GL-FU") ||
            recipe.Recipe.Contains("FU-FI-GL-AC"))
            return "BulwarkBeam";

        if (recipe.AttackPattern.Contains("折线") ||
            recipe.AttackPattern.Contains("棱镜") ||
            recipe.NodePresetId == "Preset_RowResonance" ||
            recipe.Core == "Ice" && recipe.AttackPattern.Contains("射线"))
            return "PrismBeam";

        if (recipe.AttackPattern.Contains("雷链") ||
            recipe.AttackPattern.Contains("跳链") ||
            recipe.NodePresetId == "Preset_ConductiveChain" ||
            recipe.NodePresetId == "Preset_ChainUltimate")
            return "ArcBeam";

        return "LightningBeam";
    }

    // ---- 数值解析 ----

    private static float ResolveDamage(RecipeRow recipe)
    {
        var value = recipe.SlotCount switch { 2 => 72f, 3 => 118f, 4 => 175f, _ => 70f };
        if (recipe.CombatRole.Contains("终极")) value += 35f;
        if (recipe.CombatRole.Contains("爆发")) value += 18f;
        if (recipe.CombatRole.Contains("处决")) value += 28f;
        if (recipe.CombatRole.Contains("控制")) value -= 12f;
        if (recipe.AttackPattern.Contains("雷链")) value -= 8f;
        if (recipe.AttackPattern.Contains("射线")) value -= 6f;
        if (recipe.AttackPattern.Contains("地刺")) value += 10f;
        return Mathf.Max(35f, value);
    }

    private static float ResolveDamageRate(RecipeRow recipe)
    {
        var value = recipe.SlotCount switch { 2 => 1f, 3 => 1.25f, 4 => 1.65f, _ => 1f };
        if (recipe.CombatRole.Contains("控制")) value -= 0.1f;
        if (recipe.CombatRole.Contains("收割") || recipe.CombatRole.Contains("斩杀")) value += 0.2f;
        return Mathf.Max(0.7f, value);
    }

    private static float ResolveCooldown(RecipeRow recipe)
    {
        var value = recipe.SlotCount switch { 2 => 1.8f, 3 => 3.4f, 4 => 6.5f, _ => 2f };
        if (recipe.CombatRole.Contains("终极")) value += 1.5f;
        if (recipe.CombatRole.Contains("控制")) value += 0.4f;
        if (recipe.AttackPattern.Contains("地雷")) value += 0.5f;
        return value;
    }

    private static float ResolveCastRange(RecipeRow recipe)
    {
        var value = 8f;
        if (recipe.AttackPattern.Contains("射线")) value = 7.5f;
        if (recipe.AttackPattern.Contains("雷链")) value = 8.5f;
        if (recipe.AttackPattern.Contains("地刺") || recipe.AttackPattern.Contains("地雷")) value = 5.5f;
        if (recipe.CombatRole.Contains("狙杀")) value += 1f;
        return value;
    }

    private static float ResolveDelay(RecipeRow recipe)
    {
        var value = recipe.SlotCount switch { 2 => 0.08f, 3 => 0.12f, 4 => 0.18f, _ => 0.1f };
        if (recipe.AttackPattern.Contains("重型")) value += 0.06f;
        if (recipe.AttackPattern.Contains("高速")) value -= 0.03f;
        return Mathf.Max(0.02f, value);
    }

    private static float ResolveCritChance(RecipeRow recipe)
    {
        var value = recipe.Core == "Metal" ? 0.32f : 0.18f;
        if (recipe.NodePresetId == "Preset_CritBranch" || recipe.NodePresetId == "Preset_ExecuteUltimate") value += 0.18f;
        if (recipe.CombatRole.Contains("处决") || recipe.CombatRole.Contains("暴击")) value += 0.1f;
        if (recipe.CombatRole.Contains("控制")) value -= 0.05f;
        return Mathf.Clamp01(value);
    }

    private static float ResolveRadius(RecipeRow recipe)
    {
        if (recipe.NodePresetId == "Preset_ElementCollapse") return 5.5f;
        if (recipe.NodePresetId == "Preset_ChainUltimate") return 4.2f;
        if (recipe.AttackPattern.Contains("雷链")) return 3.8f;
        if (recipe.AttackPattern.Contains("爆破")) return 4.6f;
        if (recipe.AttackPattern.Contains("墙")) return 5f;
        return 2.2f;
    }

    private static float ResolveChainCount(RecipeRow recipe)
    {
        if (recipe.AttackPattern.Contains("雷链")) return recipe.SlotCount + 1;
        if (recipe.NodePresetId == "Preset_ConductiveChain" || recipe.NodePresetId == "Preset_ChainUltimate") return recipe.SlotCount;
        return 0f;
    }

    private static float ResolveVfxDuration(RecipeRow recipe)
    {
        return recipe.SlotCount switch { 2 => 0.35f, 3 => 0.55f, 4 => 0.85f, _ => 0.4f };
    }

    // ---- 释放管线数值 ----

    private static float ResolveCastTime(RecipeRow recipe)
    {
        var value = recipe.SlotCount switch { 2 => 0.12f, 3 => 0.18f, 4 => 0.28f, _ => 0.15f };
        if (recipe.AttackPattern.Contains("重型")) value += 0.06f;
        if (recipe.AttackPattern.Contains("高速")) value -= 0.04f;
        if (recipe.CombatRole.Contains("终极")) value += 0.06f;
        return Mathf.Max(0.04f, value);
    }

    private static float ResolveChannelDuration(RecipeRow recipe)
    {
        if (recipe.AttackPattern.Contains("射线") && recipe.NodePresetId == "Preset_BeamLane") return 1.2f;
        if (recipe.AttackPattern.Contains("雷链") && recipe.SlotCount >= 3) return 1.5f;
        if (recipe.CombatRole.Contains("引导") || recipe.AttackPattern.Contains("引导")) return 2f;
        return 0f;
    }

    private static float ResolvePostCastTime(RecipeRow recipe)
    {
        var value = recipe.SlotCount switch { 2 => 0.08f, 3 => 0.12f, 4 => 0.2f, _ => 0.1f };
        if (recipe.AttackPattern.Contains("重型")) value += 0.06f;
        if (recipe.AttackPattern.Contains("高速")) value -= 0.03f;
        return Mathf.Max(0f, value);
    }

    private static string ResolveInterruptible(RecipeRow recipe)
    {
        if (recipe.CombatRole.Contains("终极") && recipe.SlotCount >= 4) return "false";
        if (recipe.RuleMutation.Contains("不可打断")) return "false";
        return "true";
    }

    private static float ResolveProjectileSpeed(RecipeRow recipe)
    {
        if (recipe.AttackPattern.Contains("地刺") || recipe.AttackPattern.Contains("地雷")) return 10f;
        if (recipe.Core == "Metal" && recipe.AttackPattern.Contains("投掷")) return 14f;
        if (recipe.AttackPattern.Contains("射击") || recipe.AttackPattern.Contains("轨迹")) return 12f;
        return 0f;
    }

    private static string ResolveProjectilePrefab(RecipeRow recipe)
    {
        if (ResolveProjectileSpeed(recipe) <= 0f) return string.Empty;
        if (recipe.Core == "Metal") return "SpikeProjectile";
        if (recipe.Core == "Fire") return "FireballProjectile";
        if (recipe.Core == "Ice") return "IceShardProjectile";
        if (recipe.Core == "Thunder") return "LightningOrbProjectile";
        return string.Empty;
    }

    private static float ResolveResourceCost(RecipeRow recipe)
    {
        var value = recipe.SlotCount switch { 2 => 15f, 3 => 28f, 4 => 45f, _ => 20f };
        if (recipe.CombatRole.Contains("终极")) value += 10f;
        if (recipe.CombatRole.Contains("控制")) value -= 3f;
        return Mathf.Max(5f, value);
    }

    private static int ResolveProjectileTrajectory(RecipeRow recipe)
    {
        if (ResolveProjectileSpeed(recipe) <= 0f) return 0;
        if (recipe.AttackPattern.Contains("追踪")) return 1;
        if (recipe.AttackPattern.Contains("抛物") || recipe.AttackPattern.Contains("抛射")) return 2;
        return 0; // 默认直线
    }

    private static float ResolveProjectileHitRadius(RecipeRow recipe)
    {
        return 0.5f; // 默认命中半径
    }

    private static float ResolveProjectileLifetime(RecipeRow recipe)
    {
        if (recipe.AttackPattern.Contains("远程")) return 6f;
        return 5f; // 默认存活时间
    }

    private static float ResolveProjectileGravity(RecipeRow recipe)
    {
        if (ResolveProjectileTrajectory(recipe) == 2) return 9.8f; // 抛物线
        return 0f;
    }

    private static string ResolveProjectileTags(RecipeRow recipe)
    {
        if (ResolveProjectileSpeed(recipe) <= 0f) return string.Empty;
        var tags = new System.Collections.Generic.List<string>();
        if (recipe.Core == "Fire") tags.Add("element.fire");
        if (recipe.Core == "Ice") tags.Add("element.ice");
        if (recipe.Core == "Thunder") tags.Add("element.lightning");
        if (recipe.Core == "Metal") tags.Add("element.metal");
        return tags.Count > 0 ? string.Join("|", tags) : string.Empty;
    }
    private static string ResolveProjectileConfigKey(RecipeRow recipe)
    {
        if (recipe.AttackPattern.Contains("雷链")) return "lightning_orb";
        if (recipe.Core == "Fire") return "fireball_default";
        if (recipe.Core == "Ice") return "frost_burst";
        if (recipe.Core == "Thunder") return "lightning_orb";
        if (recipe.Core == "Metal") return "ice_shard";
        return string.Empty;
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
