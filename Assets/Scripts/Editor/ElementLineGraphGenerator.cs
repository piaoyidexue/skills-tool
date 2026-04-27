using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ElementLineGraphGenerator
{
    private const string RecipeCsvPath = "Assets/Resources/Config/SkillRecipe.csv";
    public const string ResourceGraphRootFolder = "Assets/Resources/SkillGraphs/ElementLine";
    private const string GraphRootFolder = ResourceGraphRootFolder;
    private const string CommonFolder = GraphRootFolder + "/Common";
    private const string RecipeFolder = GraphRootFolder + "/Recipes";

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
        EnsureFolder(GraphRootFolder);
        EnsureFolder(CommonFolder);
        EnsureFolder(RecipeFolder);

        var commonGraphs = CreateCommonGraphs();
        var recipes = ReadRecipes();
        foreach (var recipe in recipes) BuildRecipeGraph(recipe, commonGraphs);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (logResult) Debug.Log($"[ElementLineGraphGenerator] 已根据配置生成 {recipes.Count} 个技能图。");
    }

    private static Dictionary<string, SkillGraph> CreateCommonGraphs()
    {
        var result = new Dictionary<string, SkillGraph>(StringComparer.OrdinalIgnoreCase);
        result["Common_ImpactDamage"] = BuildCommonImpactDamage();
        result["Common_StatusPrime"] = BuildCommonStatusPrime();
        result["Common_RowPulse"] = BuildCommonRowPulse();
        result["Common_TerrainPaint"] = BuildCommonTerrainPaint();
        result["Common_ExecuteCheck"] = BuildCommonExecuteCheck();
        return result;
    }

    private static SkillGraph BuildCommonImpactDamage()
    {
        var graph = LoadOrCreateGraph(CommonFolder + "/Common_ImpactDamage.asset", "Common_ImpactDamage");
        ClearGraph(graph);

        var start = graph.AddNodeToGraph<StartNode>();
        var vfx = graph.AddNodeToGraph<PlayVFXNode>();
        var damage = graph.AddNodeToGraph<DamageNode>();
        var status = graph.AddNodeToGraph<ApplyStatusNode>();
        var end = graph.AddNodeToGraph<EndNode>();

        start.position = new Vector2(80f, 120f);
        vfx.position = new Vector2(280f, 120f);
        damage.position = new Vector2(520f, 120f);
        status.position = new Vector2(760f, 120f);
        end.position = new Vector2(980f, 120f);

        vfx.stage = PlayVFXNode.VFXStage.Impact;
        status.statusTags.Source = StringBinding.SourceType.Literal;
        status.statusTags.LiteralValue = "default";

        Connect(start, "output", vfx, "input");
        Connect(vfx, "output", damage, "input");
        Connect(damage, "output", status, "input");
        Connect(status, "output", end, "input");
        EditorUtility.SetDirty(graph);
        return graph;
    }

    private static SkillGraph BuildCommonStatusPrime()
    {
        var graph = LoadOrCreateGraph(CommonFolder + "/Common_StatusPrime.asset", "Common_StatusPrime");
        ClearGraph(graph);

        var start = graph.AddNodeToGraph<StartNode>();
        var status = graph.AddNodeToGraph<ApplyStatusNode>();
        var log = graph.AddNodeToGraph<LogNode>();
        var end = graph.AddNodeToGraph<EndNode>();

        start.position = new Vector2(80f, 120f);
        status.position = new Vector2(300f, 120f);
        log.position = new Vector2(520f, 120f);
        end.position = new Vector2(760f, 120f);

        log.message.Source = StringBinding.SourceType.Literal;
        log.message.LiteralValue = "公共子图：状态挂载";

        Connect(start, "output", status, "input");
        Connect(status, "output", log, "input");
        Connect(log, "output", end, "input");
        EditorUtility.SetDirty(graph);
        return graph;
    }

    private static SkillGraph BuildCommonRowPulse()
    {
        var graph = LoadOrCreateGraph(CommonFolder + "/Common_RowPulse.asset", "Common_RowPulse");
        ClearGraph(graph);

        var start = graph.AddNodeToGraph<StartNode>();
        var resonance = graph.AddNodeToGraph<ResonanceNode>();
        var vfx = graph.AddNodeToGraph<PlayVFXNode>();
        var end = graph.AddNodeToGraph<EndNode>();

        start.position = new Vector2(80f, 120f);
        resonance.position = new Vector2(280f, 120f);
        vfx.position = new Vector2(520f, 120f);
        end.position = new Vector2(760f, 120f);

        resonance.resonanceTags.Source = StringBinding.SourceType.Literal;
        resonance.resonanceTags.LiteralValue = "row_resonance";
        vfx.stage = PlayVFXNode.VFXStage.Beam;

        Connect(start, "output", resonance, "input");
        Connect(resonance, "output", vfx, "input");
        Connect(vfx, "output", end, "input");
        EditorUtility.SetDirty(graph);
        return graph;
    }

    private static SkillGraph BuildCommonTerrainPaint()
    {
        var graph = LoadOrCreateGraph(CommonFolder + "/Common_TerrainPaint.asset", "Common_TerrainPaint");
        ClearGraph(graph);

        var start = graph.AddNodeToGraph<StartNode>();
        var terrain = graph.AddNodeToGraph<PaintTerrainNode>();
        var end = graph.AddNodeToGraph<EndNode>();

        start.position = new Vector2(80f, 120f);
        terrain.position = new Vector2(320f, 120f);
        end.position = new Vector2(560f, 120f);

        terrain.terrainTags.Source = StringBinding.SourceType.Literal;
        terrain.terrainTags.LiteralValue = "scorch";

        Connect(start, "output", terrain, "input");
        Connect(terrain, "output", end, "input");
        EditorUtility.SetDirty(graph);
        return graph;
    }

    private static SkillGraph BuildCommonExecuteCheck()
    {
        var graph = LoadOrCreateGraph(CommonFolder + "/Common_ExecuteCheck.asset", "Common_ExecuteCheck");
        ClearGraph(graph);

        var start = graph.AddNodeToGraph<StartNode>();
        var condition = graph.AddNodeToGraph<ConditionNode>();
        var reaction = graph.AddNodeToGraph<ReactionNode>();
        var end = graph.AddNodeToGraph<EndNode>();

        start.position = new Vector2(80f, 120f);
        condition.position = new Vector2(320f, 120f);
        reaction.position = new Vector2(580f, 60f);
        end.position = new Vector2(820f, 120f);

        condition.mode = ConditionMode.Random;
        condition.threshold.Source = FloatBinding.SourceType.SkillConfig;
        condition.threshold.SkillField = SkillFloatField.CritChance;
        reaction.reactionSummary.Source = StringBinding.SourceType.Literal;
        reaction.reactionSummary.LiteralValue = "公共子图：处决校验";
        reaction.damageMultiplier.LiteralValue = 2f;

        Connect(start, "output", condition, "input");
        Connect(condition, "truePort", reaction, "input");
        Connect(reaction, "output", end, "input");
        Connect(condition, "falsePort", end, "input");
        EditorUtility.SetDirty(graph);
        return graph;
    }

    private static void BuildRecipeGraph(RecipeRow row, IReadOnlyDictionary<string, SkillGraph> commonGraphs)
    {
        var safeName = SanitizeFileName(row.RecipeId + "_" + row.Recipe);
        var graph = LoadOrCreateGraph($"{RecipeFolder}/{safeName}.asset", $"Skill_{row.Name}");
        ClearGraph(graph);

        var start = graph.AddNodeToGraph<StartNode>();
        start.position = new Vector2(80f, 220f);

        var log = default(LogNode);
        var end = graph.AddNodeToGraph<EndNode>();
        end.position = new Vector2(1700f, 220f);

        var entry = (SkillNode)start;
        var cursorX = 360f;

        // 公共子图引用
        var commonImpact = commonGraphs.TryGetValue("Common_ImpactDamage", out var ci) ? ci : null;
        var commonStatusPrime = commonGraphs.TryGetValue("Common_StatusPrime", out var csp) ? csp : null;
        var commonRowPulse = commonGraphs.TryGetValue("Common_RowPulse", out var crp) ? crp : null;
        var commonTerrainPaint = commonGraphs.TryGetValue("Common_TerrainPaint", out var ctp) ? ctp : null;
        var commonExecuteCheck = commonGraphs.TryGetValue("Common_ExecuteCheck", out var cec) ? cec : null;

        // 添加技能描述日志
        if (!string.IsNullOrWhiteSpace(row.Name))
        {
            log = graph.AddNodeToGraph<LogNode>();
            log.position = new Vector2(160f, 100f);
            log.message.Source = StringBinding.SourceType.Literal;
            log.message.LiteralValue = BuildSummary(row);
            Connect(entry, "output", log, "input");
            entry = log;
        }

        // 插入 PreCastNode（释放前腰）
        entry = InsertPreCastNode(graph, row, entry, ref cursorX);

        // 插入 ChannelNode（吟唱支持 —— channelDuration=0 时为 no-op）
        var needsChannel = row.AttackPattern.Contains("射线") ||
                           row.AttackPattern.Contains("雷链") ||
                           row.NodePresetId == "Preset_BeamLane" ||
                           row.NodePresetId == "Preset_ConductiveChain";
        if (needsChannel)
        {
            entry = InsertChannelNode(graph, row, entry, ref cursorX);
        }

        // 根据预设选择构造
        switch (row.NodePresetId)
        {
            case "Preset_BeamLane":
                BuildBeamLane(graph, row, entry, end, cursorX);
                break;
            case "Preset_CritBranch":
                if (commonImpact != null) BuildCritBranch(graph, row, commonImpact, entry, end, cursorX);
                else BuildImpactLane(graph, row, entry, end, cursorX);
                break;
            case "Preset_ConductiveChain":
                BuildConductiveChain(graph, row, entry, end, cursorX);
                break;
            case "Preset_StatusAmplify":
                BuildStatusAmplify(graph, row, entry, end, cursorX);
                break;
            case "Preset_ReactionBurst":
                BuildReactionBurst(graph, row, entry, end, cursorX);
                break;
            case "Preset_RowResonance":
                if (commonRowPulse != null) BuildRowResonance(graph, row, commonRowPulse, entry, end, cursorX);
                else BuildBeamLane(graph, row, entry, end, cursorX);
                break;
            case "Preset_TerrainUltimate":
                BuildTerrainUltimate(graph, row, entry, end, cursorX);
                break;
            case "Preset_ChainUltimate":
                BuildChainUltimate(graph, row, entry, end, cursorX);
                break;
            case "Preset_ElementCollapse":
                BuildElementCollapse(graph, row, entry, end, cursorX);
                break;
            case "Preset_ExecuteUltimate":
                BuildExecuteUltimate(graph, row, entry, end, cursorX);
                break;
            case "Preset_TrapExecute":
                BuildTrapExecute(graph, row, entry, end, cursorX);
                break;
            default:
                BuildImpactLane(graph, row, entry, end, cursorX);
                break;
        }

        EditorUtility.SetDirty(graph);
    }

    private static SkillNode InsertPreCastNode(SkillGraph graph, RecipeRow row, SkillNode entry, ref float cursorX)
    {
        var preCast = graph.AddNodeToGraph<PreCastNode>();
        preCast.position = new Vector2(cursorX, 140f);
        preCast.castTime.Source = FloatBinding.SourceType.SkillConfig;
        preCast.castTime.SkillField = SkillFloatField.CastTime;

        Connect(entry, "output", preCast, "input");
        cursorX += 140f;
        return preCast;
    }

    private static SkillNode InsertChannelNode(SkillGraph graph, RecipeRow row, SkillNode entry, ref float cursorX)
    {
        var channel = graph.AddNodeToGraph<ChannelNode>();
        channel.position = new Vector2(cursorX, 180f);
        channel.channelDuration.Source = FloatBinding.SourceType.SkillConfig;
        channel.channelDuration.SkillField = SkillFloatField.ChannelDuration;

        Connect(entry, "output", channel, "input");
        cursorX += 160f;
        return channel;
    }

    // ==================== Build Templates ====================

    private static void BuildImpactLane(SkillGraph graph, RecipeRow row, SkillNode entry, EndNode end, float startX)
    {
        var castVfx = CreateVfxNode(graph, row, startX, 220f, PlayVFXNode.VFXStage.Cast, false);
        var delay = CreateDelayNode(graph, row, startX + 220f, 220f);
        var vfx = CreateVfxNode(graph, row, startX + 460f, 220f, PlayVFXNode.VFXStage.Impact, false);
        var damage = CreateDamageNode(graph, row, startX + 700f, 220f, false);
        var status = CreateStatusNode(graph, row, startX + 940f, 220f);
        var terrainVfx = CreateVfxNode(graph, row, startX + 1180f, 220f, PlayVFXNode.VFXStage.Terrain, false);
        var terrain = CreateTerrainNode(graph, row, startX + 1420f, 220f);
        var postCast = CreatePostCastNode(graph, row, startX + 1660f, 220f);

        Connect(entry, "output", castVfx, "input");
        Connect(castVfx, "output", delay, "input");
        Connect(vfx, "output", damage, "input");
        Connect(damage, "output", status, "input");
        Connect(status, "output", terrainVfx, "input");
        Connect(terrainVfx, "output", terrain, "input");
        Connect(terrain, "output", postCast, "input");
        Connect(postCast, "output", end, "input");
        Connect(delay, "output", vfx, "input");
    }

    private static void BuildBeamLane(SkillGraph graph, RecipeRow row, SkillNode entry, EndNode end, float startX)
    {
        var castVfx = CreateVfxNode(graph, row, startX, 220f, PlayVFXNode.VFXStage.Cast, false);
        var delay = CreateDelayNode(graph, row, startX + 220f, 220f);
        var vfx = CreateVfxNode(graph, row, startX + 460f, 220f, PlayVFXNode.VFXStage.Beam, true);
        var damage = CreateDamageNode(graph, row, startX + 700f, 220f, false);
        var status = CreateStatusNode(graph, row, startX + 940f, 220f);
        var terrainVfx = CreateVfxNode(graph, row, startX + 1180f, 220f, PlayVFXNode.VFXStage.Terrain, false);
        var terrain = CreateTerrainNode(graph, row, startX + 1420f, 220f);
        var postCast = CreatePostCastNode(graph, row, startX + 1660f, 220f);

        Connect(entry, "output", castVfx, "input");
        Connect(castVfx, "output", delay, "input");
        Connect(delay, "output", vfx, "input");
        Connect(vfx, "output", damage, "input");
        Connect(damage, "output", status, "input");
        Connect(status, "output", terrainVfx, "input");
        Connect(terrainVfx, "output", terrain, "input");
        Connect(terrain, "output", postCast, "input");
        Connect(postCast, "output", end, "input");
    }

    private static void BuildCritBranch(SkillGraph graph, RecipeRow row, SkillGraph commonImpact, SkillNode entry,
        EndNode end, float startX)
    {
        var castVfx = CreateVfxNode(graph, row, startX, 220f, PlayVFXNode.VFXStage.Cast, false);
        var delay = CreateDelayNode(graph, row, startX + 220f, 220f);
        var roll = graph.AddNodeToGraph<RollChanceNode>();
        var condition = graph.AddNodeToGraph<ConditionNode>();
        var modifier = graph.AddNodeToGraph<ModifyFloatNode>();
        var critVfx = CreateVfxNode(graph, row, startX + 1180f, 320f, PlayVFXNode.VFXStage.Finisher, UsesBeamVfx(row));
        var critDamage = CreateDamageNode(graph, row, startX + 1420f, 320f, true);
        var fallback = graph.AddNodeToGraph<SubGraphNode>();
        var status = CreateStatusNode(graph, row, startX + 1660f, 220f);
        var terrainVfx = CreateVfxNode(graph, row, startX + 1900f, 220f, PlayVFXNode.VFXStage.Terrain, false);
        var terrain = CreateTerrainNode(graph, row, startX + 2140f, 220f);
        var postCast = CreatePostCastNode(graph, row, startX + 2380f, 220f);

        roll.position = new Vector2(startX + 460f, 220f);
        condition.position = new Vector2(startX + 700f, 220f);
        modifier.position = new Vector2(startX + 940f, 320f);
        fallback.position = new Vector2(startX + 940f, 120f);

        roll.outputKey = BBKey.IsCrit;
        modifier.outputKey = BBKey.DamageOverride;
        modifier.inputValue.Source = FloatBinding.SourceType.SkillConfig;
        modifier.inputValue.SkillField = SkillFloatField.Damage;
        modifier.multiplier.Source = FloatBinding.SourceType.Literal;
        modifier.multiplier.LiteralValue = row.SlotCount >= 4 ? 2.5f : 2f;
        condition.mode = ConditionMode.BlackboardBool;
        condition.bbKey = BBKey.IsCrit;
        fallback.subGraph = commonImpact;

        Connect(entry, "output", castVfx, "input");
        Connect(castVfx, "output", delay, "input");
        Connect(delay, "output", roll, "input");
        Connect(roll, "output", condition, "input");
        Connect(condition, "truePort", modifier, "input");
        Connect(modifier, "output", critVfx, "input");
        Connect(critVfx, "output", critDamage, "input");
        Connect(condition, "falsePort", fallback, "input");
        Connect(critDamage, "output", status, "input");
        Connect(fallback, "output", status, "input");
        Connect(status, "output", terrainVfx, "input");
        Connect(terrainVfx, "output", terrain, "input");
        Connect(terrain, "output", postCast, "input");
        Connect(postCast, "output", end, "input");
    }

    private static void BuildConductiveChain(SkillGraph graph, RecipeRow row, SkillNode entry, EndNode end,
        float startX)
    {
        var castVfx = CreateVfxNode(graph, row, startX, 220f, PlayVFXNode.VFXStage.Cast, false);
        var delay = CreateDelayNode(graph, row, startX + 220f, 220f);
        var vfx = CreateVfxNode(graph, row, startX + 460f, 220f, PlayVFXNode.VFXStage.Beam, true);
        var damage = CreateDamageNode(graph, row, startX + 700f, 220f, false);
        var status = CreateStatusNode(graph, row, startX + 940f, 220f);
        var reaction = graph.AddNodeToGraph<ReactionNode>();
        var reactionVfx = CreateVfxNode(graph, row, startX + 1180f, 220f, PlayVFXNode.VFXStage.Reaction, false);
        var terrainVfx = CreateVfxNode(graph, row, startX + 1420f, 220f, PlayVFXNode.VFXStage.Terrain, false);
        var terrain = CreateTerrainNode(graph, row, startX + 1660f, 220f);
        var postCast = CreatePostCastNode(graph, row, startX + 1900f, 220f);

        reaction.position = new Vector2(startX + 1180f, 100f);
        reaction.reactionSummary.Source = StringBinding.SourceType.Literal;
        reaction.reactionSummary.LiteralValue = row.SynergyLogic;
        reaction.damageMultiplier.Source = FloatBinding.SourceType.Literal;
        reaction.damageMultiplier.LiteralValue = 1.2f;
        reaction.writeDamageOverride = false;

        Connect(entry, "output", castVfx, "input");
        Connect(castVfx, "output", delay, "input");
        Connect(delay, "output", vfx, "input");
        Connect(vfx, "output", damage, "input");
        Connect(damage, "output", status, "input");
        Connect(status, "output", reactionVfx, "input");
        Connect(reactionVfx, "output", reaction, "input");
        Connect(reaction, "output", terrainVfx, "input");
        Connect(terrainVfx, "output", terrain, "input");
        Connect(terrain, "output", postCast, "input");
        Connect(postCast, "output", end, "input");
    }

    private static void BuildStatusAmplify(SkillGraph graph, RecipeRow row, SkillNode entry, EndNode end, float startX)
    {
        var castVfx = CreateVfxNode(graph, row, startX, 220f, PlayVFXNode.VFXStage.Cast, false);
        var condition = graph.AddNodeToGraph<ConditionNode>();
        var modifier = graph.AddNodeToGraph<ModifyFloatNode>();
        var burstVfx = CreateVfxNode(graph, row, startX + 480f, 320f, PlayVFXNode.VFXStage.Reaction, false);
        var boostedDamage = CreateDamageNode(graph, row, startX + 720f, 320f, true);
        var baseVfx = CreateVfxNode(graph, row, startX + 480f, 120f, PlayVFXNode.VFXStage.Impact, UsesBeamVfx(row));
        var baseDamage = CreateDamageNode(graph, row, startX + 720f, 120f, false);
        var status = CreateStatusNode(graph, row, startX + 960f, 220f);
        var terrainVfx = CreateVfxNode(graph, row, startX + 1200f, 220f, PlayVFXNode.VFXStage.Terrain, false);
        var terrain = CreateTerrainNode(graph, row, startX + 1440f, 220f);
        var postCast = CreatePostCastNode(graph, row, startX + 1680f, 220f);

        condition.position = new Vector2(startX + 220f, 220f);
        modifier.position = new Vector2(startX + 240f, 320f);
        condition.mode = ConditionMode.Random;
        condition.threshold.Source = FloatBinding.SourceType.SkillConfig;
        condition.threshold.SkillField = SkillFloatField.CritChance;
        modifier.outputKey = BBKey.DamageOverride;
        modifier.inputValue.Source = FloatBinding.SourceType.SkillConfig;
        modifier.inputValue.SkillField = SkillFloatField.Damage;
        modifier.multiplier.Source = FloatBinding.SourceType.Literal;
        modifier.multiplier.LiteralValue = 1.5f;

        Connect(entry, "output", castVfx, "input");
        Connect(castVfx, "output", condition, "input");
        Connect(condition, "truePort", modifier, "input");
        Connect(modifier, "output", burstVfx, "input");
        Connect(burstVfx, "output", boostedDamage, "input");
        Connect(condition, "falsePort", baseVfx, "input");
        Connect(baseVfx, "output", baseDamage, "input");
        Connect(boostedDamage, "output", status, "input");
        Connect(baseDamage, "output", status, "input");
        Connect(status, "output", terrainVfx, "input");
        Connect(terrainVfx, "output", terrain, "input");
        Connect(terrain, "output", postCast, "input");
        Connect(postCast, "output", end, "input");
    }

    private static void BuildReactionBurst(SkillGraph graph, RecipeRow row, SkillNode entry, EndNode end, float startX)
    {
        var castVfx = CreateVfxNode(graph, row, startX, 220f, PlayVFXNode.VFXStage.Cast, false);
        var condition = graph.AddNodeToGraph<ConditionNode>();
        var vfx = CreateVfxNode(graph, row, startX + 480f, 320f, UsesBeamVfx(row) ? PlayVFXNode.VFXStage.Beam : PlayVFXNode.VFXStage.Impact, UsesBeamVfx(row));
        var reaction = graph.AddNodeToGraph<ReactionNode>();
        var reactionVfx = CreateVfxNode(graph, row, startX + 720f, 320f, PlayVFXNode.VFXStage.Reaction, false);
        var damage = CreateDamageNode(graph, row, startX + 960f, 320f, true);
        var finisherVfx = CreateVfxNode(graph, row, startX + 1200f, 320f, PlayVFXNode.VFXStage.Finisher, false);
        var status = CreateStatusNode(graph, row, startX + 1440f, 320f);
        var terrainVfx = CreateVfxNode(graph, row, startX + 1680f, 320f, PlayVFXNode.VFXStage.Terrain, false);
        var terrain = CreateTerrainNode(graph, row, startX + 1920f, 320f);
        var postCast = CreatePostCastNode(graph, row, startX + 2160f, 320f);

        condition.position = new Vector2(startX + 220f, 220f);
        reaction.position = new Vector2(startX + 720f, 100f);

        condition.mode = ConditionMode.Random;
        condition.threshold.Source = FloatBinding.SourceType.SkillConfig;
        condition.threshold.SkillField = SkillFloatField.CritChance;
        reaction.reactionSummary.Source = StringBinding.SourceType.Literal;
        reaction.reactionSummary.LiteralValue = row.SynergyLogic;
        reaction.damageMultiplier.Source = FloatBinding.SourceType.Literal;
        reaction.damageMultiplier.LiteralValue = row.SlotCount >= 4 ? 2.5f : 2f;

        Connect(entry, "output", castVfx, "input");
        Connect(castVfx, "output", condition, "input");
        Connect(condition, "truePort", vfx, "input");
        Connect(vfx, "output", reaction, "input");
        Connect(reaction, "output", reactionVfx, "input");
        Connect(reactionVfx, "output", damage, "input");
        Connect(damage, "output", finisherVfx, "input");
        Connect(finisherVfx, "output", status, "input");
        Connect(status, "output", terrainVfx, "input");
        Connect(terrainVfx, "output", terrain, "input");
        Connect(terrain, "output", postCast, "input");
        Connect(postCast, "output", end, "input");
        Connect(condition, "falsePort", end, "input");
    }

    private static void BuildRowResonance(SkillGraph graph, RecipeRow row, SkillGraph commonRowPulse, SkillNode entry,
        EndNode end, float startX)
    {
        var castVfx = CreateVfxNode(graph, row, startX, 220f, PlayVFXNode.VFXStage.Cast, false);
        var delay = CreateDelayNode(graph, row, startX + 220f, 220f);
        var resonance = graph.AddNodeToGraph<ResonanceNode>();
        var pulse = graph.AddNodeToGraph<SubGraphNode>();
        var damage = CreateDamageNode(graph, row, startX + 940f, 220f, false);
        var status = CreateStatusNode(graph, row, startX + 1180f, 220f);
        var terrainVfx = CreateVfxNode(graph, row, startX + 1420f, 220f, PlayVFXNode.VFXStage.Terrain, false);
        var terrain = CreateTerrainNode(graph, row, startX + 1660f, 220f);
        var postCast = CreatePostCastNode(graph, row, startX + 1900f, 220f);

        resonance.position = new Vector2(startX + 460f, 220f);
        pulse.position = new Vector2(startX + 700f, 220f);
        resonance.resonanceTags.Source = StringBinding.SourceType.Literal;
        resonance.resonanceTags.LiteralValue = row.ResonanceTags;
        pulse.subGraph = commonRowPulse;

        Connect(entry, "output", castVfx, "input");
        Connect(castVfx, "output", delay, "input");
        Connect(delay, "output", resonance, "input");
        Connect(resonance, "output", pulse, "input");
        Connect(pulse, "output", damage, "input");
        Connect(damage, "output", status, "input");
        Connect(status, "output", terrainVfx, "input");
        Connect(terrainVfx, "output", terrain, "input");
        Connect(terrain, "output", postCast, "input");
        Connect(postCast, "output", end, "input");
    }

    private static void BuildTerrainUltimate(SkillGraph graph, RecipeRow row, SkillNode entry, EndNode end,
        float startX)
    {
        var castVfx = CreateVfxNode(graph, row, startX, 220f, PlayVFXNode.VFXStage.Cast, false);
        var delay = CreateDelayNode(graph, row, startX + 220f, 220f);
        var vfx = CreateVfxNode(graph, row, startX + 460f, 220f, PlayVFXNode.VFXStage.Impact, false);
        var damage = CreateDamageNode(graph, row, startX + 700f, 220f, false);
        var status = CreateStatusNode(graph, row, startX + 940f, 220f);
        var terrainVfx = CreateVfxNode(graph, row, startX + 1180f, 220f, PlayVFXNode.VFXStage.Terrain, false);
        var terrain = CreateTerrainNode(graph, row, startX + 1420f, 220f);
        var finisherStaged = CreateFinisherStagedNode(graph, row, startX + 1660f, 220f);
        var postCast = CreatePostCastNode(graph, row, startX + 1900f, 220f);

        Connect(entry, "output", castVfx, "input");
        Connect(castVfx, "output", delay, "input");
        Connect(delay, "output", vfx, "input");
        Connect(vfx, "output", damage, "input");
        Connect(damage, "output", status, "input");
        Connect(status, "output", terrainVfx, "input");
        Connect(terrainVfx, "output", terrain, "input");
        Connect(terrain, "output", finisherStaged, "input");
        Connect(finisherStaged, "output", postCast, "input");
        Connect(postCast, "output", end, "input");
    }

    private static void BuildChainUltimate(SkillGraph graph, RecipeRow row, SkillNode entry, EndNode end,
        float startX)
    {
        var castVfx = CreateVfxNode(graph, row, startX, 220f, PlayVFXNode.VFXStage.Cast, false);
        var delay = CreateDelayNode(graph, row, startX + 220f, 220f);
        var parallel = graph.AddNodeToGraph<ParallelNode>();
        var branchA_Vfx = CreateVfxNode(graph, row, startX + 740f, 120f, PlayVFXNode.VFXStage.Beam, true);
        var branchA_Damage = CreateDamageNode(graph, row, startX + 980f, 120f, false);
        var branchB_Vfx = CreateVfxNode(graph, row, startX + 740f, 340f, PlayVFXNode.VFXStage.Impact, false);
        var branchB_Damage = CreateDamageNode(graph, row, startX + 980f, 340f, false);
        var status = CreateStatusNode(graph, row, startX + 1320f, 220f);
        var terrainVfx = CreateVfxNode(graph, row, startX + 1560f, 220f, PlayVFXNode.VFXStage.Terrain, false);
        var terrain = CreateTerrainNode(graph, row, startX + 1800f, 220f);
        var finisherStaged = CreateFinisherStagedNode(graph, row, startX + 2040f, 220f);
        var postCast = CreatePostCastNode(graph, row, startX + 2280f, 220f);

        parallel.position = new Vector2(startX + 480f, 220f);
        // 分支通过 Connect 调用自动创建 SkillConnection，不再需要显式 AddDynamicOutput

        Connect(entry, "output", castVfx, "input");
        Connect(castVfx, "output", delay, "input");
        Connect(delay, "output", parallel, "input");
        Connect(parallel, "branches 0", branchA_Vfx, "input");
        Connect(branchA_Vfx, "output", branchA_Damage, "input");
        Connect(parallel, "branches 1", branchB_Vfx, "input");
        Connect(branchB_Vfx, "output", branchB_Damage, "input");
        Connect(parallel, "output", status, "input");
        Connect(status, "output", terrainVfx, "input");
        Connect(terrainVfx, "output", terrain, "input");
        Connect(terrain, "output", finisherStaged, "input");
        Connect(finisherStaged, "output", postCast, "input");
        Connect(postCast, "output", end, "input");
    }

    private static void BuildElementCollapse(SkillGraph graph, RecipeRow row, SkillNode entry, EndNode end,
        float startX)
    {
        var castVfx = CreateVfxNode(graph, row, startX, 220f, PlayVFXNode.VFXStage.Cast, false);
        var delay = CreateDelayNode(graph, row, startX + 220f, 220f);
        var parallel = graph.AddNodeToGraph<ParallelNode>();
        var branchA_Vfx = CreateVfxNode(graph, row, startX + 740f, 100f, PlayVFXNode.VFXStage.Impact, false);
        var branchA_Damage = CreateDamageNode(graph, row, startX + 980f, 100f, false);
        var branchB_Vfx = CreateVfxNode(graph, row, startX + 740f, 340f, PlayVFXNode.VFXStage.Beam, true);
        var branchB_Damage = CreateDamageNode(graph, row, startX + 980f, 340f, false);
        var status = CreateStatusNode(graph, row, startX + 1320f, 220f);
        var terrainVfx = CreateVfxNode(graph, row, startX + 1560f, 220f, PlayVFXNode.VFXStage.Terrain, false);
        var terrain = CreateTerrainNode(graph, row, startX + 1800f, 220f);
        var finisherStaged = CreateFinisherStagedNode(graph, row, startX + 2040f, 220f);
        var postCast = CreatePostCastNode(graph, row, startX + 2280f, 220f);

        parallel.position = new Vector2(startX + 480f, 220f);
        // 分支通过 Connect 调用自动创建 SkillConnection，不再需要显式 AddDynamicOutput

        Connect(entry, "output", castVfx, "input");
        Connect(castVfx, "output", delay, "input");
        Connect(delay, "output", parallel, "input");
        Connect(parallel, "branches 0", branchA_Vfx, "input");
        Connect(branchA_Vfx, "output", branchA_Damage, "input");
        Connect(parallel, "branches 1", branchB_Vfx, "input");
        Connect(branchB_Vfx, "output", branchB_Damage, "input");
        Connect(parallel, "output", status, "input");
        Connect(status, "output", terrainVfx, "input");
        Connect(terrainVfx, "output", terrain, "input");
        Connect(terrain, "output", finisherStaged, "input");
        Connect(finisherStaged, "output", postCast, "input");
        Connect(postCast, "output", end, "input");
    }

    private static void BuildExecuteUltimate(SkillGraph graph, RecipeRow row, SkillNode entry, EndNode end,
        float startX)
    {
        var castVfx = CreateVfxNode(graph, row, startX, 220f, PlayVFXNode.VFXStage.Cast, false);
        var condition = graph.AddNodeToGraph<ConditionNode>();
        var reaction = graph.AddNodeToGraph<ReactionNode>();
        var reactionVfx = CreateVfxNode(graph, row, startX + 480f, 320f, PlayVFXNode.VFXStage.Reaction, false);
        var finisherStaged = CreateFinisherStagedNode(graph, row, startX + 720f, 320f);
        var damage = CreateDamageNode(graph, row, startX + 960f, 320f, true);
        var status = CreateStatusNode(graph, row, startX + 1200f, 320f);
        var terrainVfx = CreateVfxNode(graph, row, startX + 1440f, 320f, PlayVFXNode.VFXStage.Terrain, false);
        var terrain = CreateTerrainNode(graph, row, startX + 1680f, 320f);
        var postCast = CreatePostCastNode(graph, row, startX + 1920f, 320f);

        condition.position = new Vector2(startX + 220f, 220f);
        reaction.position = new Vector2(startX + 240f, 320f);
        condition.mode = ConditionMode.Random;
        condition.threshold.Source = FloatBinding.SourceType.SkillConfig;
        condition.threshold.SkillField = SkillFloatField.CritChance;
        reaction.reactionSummary.Source = StringBinding.SourceType.Literal;
        reaction.reactionSummary.LiteralValue = row.RuleMutation;
        reaction.damageMultiplier.Source = FloatBinding.SourceType.Literal;
        reaction.damageMultiplier.LiteralValue = 2.5f;

        Connect(entry, "output", castVfx, "input");
        Connect(castVfx, "output", condition, "input");
        Connect(condition, "truePort", reaction, "input");
        Connect(reaction, "output", reactionVfx, "input");
        Connect(reactionVfx, "output", finisherStaged, "input");
        Connect(finisherStaged, "output", damage, "input");
        Connect(damage, "output", status, "input");
        Connect(status, "output", terrainVfx, "input");
        Connect(terrainVfx, "output", terrain, "input");
        Connect(terrain, "output", postCast, "input");
        Connect(postCast, "output", end, "input");
        Connect(condition, "falsePort", end, "input");
    }

    private static void BuildTrapExecute(SkillGraph graph, RecipeRow row, SkillNode entry, EndNode end, float startX)
    {
        var castVfx = CreateVfxNode(graph, row, startX, 220f, PlayVFXNode.VFXStage.Cast, false);
        var delay = CreateDelayNode(graph, row, startX + 220f, 220f);
        var vfx = CreateVfxNode(graph, row, startX + 460f, 220f, PlayVFXNode.VFXStage.Impact, false);
        var condition = graph.AddNodeToGraph<ConditionNode>();
        var damage = CreateDamageNode(graph, row, startX + 940f, 320f, false);
        var status = CreateStatusNode(graph, row, startX + 1180f, 320f);
        var terrainVfx = CreateVfxNode(graph, row, startX + 1420f, 320f, PlayVFXNode.VFXStage.Terrain, false);
        var terrain = CreateTerrainNode(graph, row, startX + 1660f, 320f);
        var postCast = CreatePostCastNode(graph, row, startX + 1900f, 320f);

        condition.position = new Vector2(startX + 700f, 220f);
        condition.mode = ConditionMode.Random;
        condition.threshold.Source = FloatBinding.SourceType.SkillConfig;
        condition.threshold.SkillField = SkillFloatField.CritChance;

        Connect(entry, "output", castVfx, "input");
        Connect(castVfx, "output", delay, "input");
        Connect(delay, "output", vfx, "input");
        Connect(vfx, "output", condition, "input");
        Connect(condition, "truePort", damage, "input");
        Connect(damage, "output", status, "input");
        Connect(status, "output", terrainVfx, "input");
        Connect(terrainVfx, "output", terrain, "input");
        Connect(terrain, "output", postCast, "input");
        Connect(postCast, "output", end, "input");
        Connect(condition, "falsePort", end, "input");
    }

    // ==================== Node Factory Helpers ====================

    private static PreCastNode CreatePreCastNode(SkillGraph graph, RecipeRow row, float x, float y)
    {
        var node = graph.AddNodeToGraph<PreCastNode>();
        node.position = new Vector2(x, y);
        node.castTime.Source = FloatBinding.SourceType.SkillConfig;
        node.castTime.SkillField = SkillFloatField.CastTime;
        return node;
    }

    private static PostCastNode CreatePostCastNode(SkillGraph graph, RecipeRow row, float x, float y)
    {
        var node = graph.AddNodeToGraph<PostCastNode>();
        node.position = new Vector2(x, y);
        node.postCastTime.Source = FloatBinding.SourceType.SkillConfig;
        node.postCastTime.SkillField = SkillFloatField.PostCastTime;
        return node;
    }

    private static DelayNode CreateDelayNode(SkillGraph graph, RecipeRow row, float x, float y)
    {
        var delay = graph.AddNodeToGraph<DelayNode>();
        delay.position = new Vector2(x, y);
        delay.delaySeconds.Source = FloatBinding.SourceType.SkillConfig;
        delay.delaySeconds.SkillField = SkillFloatField.DelaySeconds;
        delay.delaySeconds.DefaultValue = 0f;
        return delay;
    }

    private static FinisherStagedNode CreateFinisherStagedNode(SkillGraph graph, RecipeRow row, float x, float y)
    {
        var node = graph.AddNodeToGraph<FinisherStagedNode>();
        node.position = new Vector2(x, y);
        node.parentBinding = FinisherStagedNode.TransformBinding.Target;
        node.directionMode = FinisherStagedNode.StagedDirectionMode.CasterToTarget;
        node.absorbDuration.Source = FloatBinding.SourceType.Literal;
        node.absorbDuration.LiteralValue = 0.55f;
        node.burstDuration.Source = FloatBinding.SourceType.SkillConfig;
        node.burstDuration.SkillField = SkillFloatField.VFXDuration;
        node.scaleMultiplier.Source = FloatBinding.SourceType.Literal;
        node.scaleMultiplier.LiteralValue = 1f;
        return node;
    }

    private static PlayVFXNode CreateVfxNode(SkillGraph graph, RecipeRow row, float x, float y,
        PlayVFXNode.VFXStage stage, bool preferBeam)
    {
        var node = graph.AddNodeToGraph<PlayVFXNode>();
        node.position = new Vector2(x, y);
        node.stage = stage;
        node.parentBinding = stage == PlayVFXNode.VFXStage.Terrain ? PlayVFXNode.TransformBinding.World : PlayVFXNode.TransformBinding.World;
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

    private static DamageNode CreateDamageNode(SkillGraph graph, RecipeRow row, float x, float y, bool useOverride)
    {
        var damage = graph.AddNodeToGraph<DamageNode>();
        damage.position = new Vector2(x, y);
        if (useOverride)
        {
            damage.damageAmount.Source = FloatBinding.SourceType.Blackboard;
            damage.damageAmount.BlackboardKey = BBKey.DamageOverride;
            damage.damageAmount.DefaultValue = 0f;
            damage.multiplyByDamageRate = false;
        }
        else
        {
            damage.damageAmount.Source = FloatBinding.SourceType.SkillConfig;
            damage.damageAmount.SkillField = SkillFloatField.Damage;
            damage.damageRate.Source = FloatBinding.SourceType.SkillConfig;
            damage.damageRate.SkillField = SkillFloatField.DamageRate;
            damage.multiplyByDamageRate = true;
        }

        return damage;
    }

    private static ApplyStatusNode CreateStatusNode(SkillGraph graph, RecipeRow row, float x, float y)
    {
        var node = graph.AddNodeToGraph<ApplyStatusNode>();
        node.position = new Vector2(x, y);
        node.statusTags.Source = StringBinding.SourceType.Literal;
        node.statusTags.LiteralValue = row.StatusTags;
        return node;
    }

    private static PaintTerrainNode CreateTerrainNode(SkillGraph graph, RecipeRow row, float x, float y)
    {
        var node = graph.AddNodeToGraph<PaintTerrainNode>();
        node.position = new Vector2(x, y);
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

    private static void Connect(SkillNode fromNode, string fromPort, SkillNode toNode, string toPort)
    {
        if (fromNode == null || toNode == null) return;
        var conn = SkillConnection.Create(fromNode, toNode, fromPort);
        if (conn != null)
        {
            conn.portName = fromPort;
        }
    }

    private static SkillGraph LoadOrCreateGraph(string path, string graphName)
    {
        var graph = AssetDatabase.LoadAssetAtPath<SkillGraph>(path);
        if (graph != null)
        {
            graph.name = graphName;
            return graph;
        }

        graph = ScriptableObject.CreateInstance<SkillGraph>();
        graph.name = graphName;
        AssetDatabase.CreateAsset(graph, path);
        return graph;
    }

    private static void ClearGraph(SkillGraph graph)
    {
        var nodes = graph.allNodes;
        for (var i = nodes.Count - 1; i >= 0; i--)
        {
            var node = nodes[i];
            if (node == null) continue;

            graph.RemoveNode(node, true, false);
        }

        EditorUtility.SetDirty(graph);
    }

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
                Notes = GetValue(row, "notes")
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
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

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
        public string SynergyLogic;
        public string TerrainTags;
    }
}
