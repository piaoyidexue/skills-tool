using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
///     元素阵线技能图生成器（GAS 架构版）。
///     生成极简线性流程：PreCastVFX → Delay → ApplyEffectNode → PostCastVFX。
///     复杂的元素反应判定、暴击分支、数值覆写全部由 EffectSystem + ReactionEngine + Modifier Pipeline 接管。
/// </summary>
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

    /// <summary>
    ///     公共子图：命中伤害。
    ///     GAS架构版：VFX → ApplyEffectNode，所有伤害/状态由 EffectSystem 接管。
    /// </summary>
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

    /// <summary>
    ///     公共子图：状态挂载。
    ///     GAS架构版：ApplyEffectNode → Log，状态施加由 EffectSystem 接管。
    /// </summary>
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

    /// <summary>
    ///     公共子图：处决校验。
    ///     GAS架构版：Condition → ApplyEffectNode，暴击/反应由 EffectSystem 接管。
    /// </summary>
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

    private static void BuildRecipeGraph(RecipeRow row, IReadOnlyDictionary<string, SkillGraphAsset> commonGraphs)
    {
        var safeName = SanitizeFileName(row.RecipeId + "_" + row.Recipe);
        var graph = LoadOrCreateGraph($"{RecipeFolder}/{safeName}.asset", $"Skill_{row.Name}");
        ClearGraph(graph);

        // ============================================================
        //  GAS 架构版：极简线性流程
        //  PreCastVFX → Delay → ApplyEffectNode → PostCastVFX
        //  所有害数计算、反应判定、暴击分支由 EffectSystem 接管
        // ============================================================

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
        var delay = CreateDelayNode(graph, row, cursorX, 220f);
        cursorX += 220f;

        // ★ ApplyEffectNode —— 唯一战斗结算节点
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

        // TerrainVFX + Terrain（可选）
        var terrainVfx = CreateVfxNode(graph, row, cursorX, 220f, PlayVFXNode.VFXStage.Terrain, false);
        cursorX += 240f;
        var terrain = CreateTerrainNode(graph, row, cursorX, 220f);
        cursorX += 240f;

        // PostCastVFX
        var postCast = CreatePostCastNode(graph, row, cursorX, 220f);
        cursorX += 220f;

        var end = graph.AddNode<EndNode>();
        end.Position = new Vector2(cursorX, 220f);

        // ---- 连线：线性流程 ----
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

    private static SkillNodeBase InsertPreCastNode(SkillGraphAsset graph, RecipeRow row, SkillNodeBase entry, ref float cursorX)
    {
        var preCast = graph.AddNode<PreCastNode>();
        preCast.Position = new Vector2(cursorX, 140f);
        preCast.castTime.Source = FloatBinding.SourceType.SkillConfig;
        preCast.castTime.SkillField = SkillFloatField.CastTime;

        Connect(entry, "output", preCast, "input");
        cursorX += 140f;
        return preCast;
    }

    private static SkillNodeBase InsertChannelNode(SkillGraphAsset graph, RecipeRow row, SkillNodeBase entry, ref float cursorX)
    {
        var channel = graph.AddNode<ChannelNode>();
        channel.Position = new Vector2(cursorX, 180f);
        channel.channelDuration.Source = FloatBinding.SourceType.SkillConfig;
        channel.channelDuration.SkillField = SkillFloatField.ChannelDuration;

        Connect(entry, "output", channel, "input");
        cursorX += 160f;
        return channel;
    }

    // ==================== Node Factory Helpers ====================

    private static PreCastNode CreatePreCastNode(SkillGraphAsset graph, RecipeRow row, float x, float y)
    {
        var node = graph.AddNode<PreCastNode>();
        node.Position = new Vector2(x, y);
        node.castTime.Source = FloatBinding.SourceType.SkillConfig;
        node.castTime.SkillField = SkillFloatField.CastTime;
        return node;
    }

    private static PostCastNode CreatePostCastNode(SkillGraphAsset graph, RecipeRow row, float x, float y)
    {
        var node = graph.AddNode<PostCastNode>();
        node.Position = new Vector2(x, y);
        node.postCastTime.Source = FloatBinding.SourceType.SkillConfig;
        node.postCastTime.SkillField = SkillFloatField.PostCastTime;
        return node;
    }

    private static DelayNode CreateDelayNode(SkillGraphAsset graph, RecipeRow row, float x, float y)
    {
        var delay = graph.AddNode<DelayNode>();
        delay.Position = new Vector2(x, y);
        delay.delaySeconds.Source = FloatBinding.SourceType.SkillConfig;
        delay.delaySeconds.SkillField = SkillFloatField.DelaySeconds;
        delay.delaySeconds.DefaultValue = 0f;
        return delay;
    }

    private static FinisherStagedNode CreateFinisherStagedNode(SkillGraphAsset graph, RecipeRow row, float x, float y)
    {
        var node = graph.AddNode<FinisherStagedNode>();
        node.Position = new Vector2(x, y);
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

    private static PlayVFXNode CreateVfxNode(SkillGraphAsset graph, RecipeRow row, float x, float y,
        PlayVFXNode.VFXStage stage, bool preferBeam)
    {
        var node = graph.AddNode<PlayVFXNode>();
        node.Position = new Vector2(x, y);
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

    private static void Connect(SkillNodeBase fromNode, string fromPort, SkillNodeBase toNode, string toPort)
    {
        if (fromNode == null || toNode == null) return;
        var graph = fromNode.OwningGraph;
        if (graph == null) return;
        graph.AddEdge(new SkillEdge(fromNode.NodeGuid, fromPort, toNode.NodeGuid, toPort));
    }

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
        public string TerrainTags;
        public string SynergyLogic;
        /// <summary>GAS架构：关联的 GameplayEffectData ID</summary>
        public int EffectId;
    }
}
