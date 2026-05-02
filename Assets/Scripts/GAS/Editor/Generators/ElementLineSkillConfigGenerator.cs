using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ElementLineSkillConfigGenerator
{
    private const string RecipeCsvPath = "Assets/Resources/Config/SkillRecipe.csv";
    private const string RuntimeSkillCsvPath = "Assets/Resources/Config/Skill.csv";

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
        {
            Debug.Log($"[ElementLineSkillConfigGenerator] 已生成 {recipes.Count} 条运行时技能配置。");
        }
    }

    private static string BuildRuntimeSkillCsv(List<RecipeRow> recipes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("skill_id,name,graph_path,impact_vfx,beam_vfx,damage,damage_rate,cooldown,cast_range,delay_seconds,crit_chance,radius,chain_count,vfx_duration,cast_time,channel_duration,post_cast_time,interruptible,projectile_speed,projectile_prefab,resource_cost");

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
                .Append(ResolveResourceCost(recipe).ToString("0.###", CultureInfo.InvariantCulture))
                .AppendLine();
        }

        return sb.ToString();
    }

    // ---- existing resolvers ----

    private static string ResolveImpactVfx(RecipeRow recipe)
    {
        if (recipe.NodePresetId == "Preset_ElementCollapse" ||
            recipe.NodePresetId == "Preset_ChainUltimate" ||
            recipe.CombatRole.Contains("终极") ||
            recipe.RuleMutation.Contains("爆破") ||
            recipe.RuleMutation.Contains("清场"))
        {
            return "ExplosionWave";
        }

        if (recipe.Core == "Ice" || recipe.StatusTags.Contains("freeze") || recipe.TerrainTags.Contains("ice"))
        {
            return "FrostBurst";
        }

        return "HitSpark";
    }

    private static string ResolveBeamVfx(RecipeRow recipe)
    {
        if (!UsesBeam(recipe)) return string.Empty;

        // 墙/堡垒/障壁 → BulwarkBeam 宽厚墙体束
        if (recipe.AttackPattern.Contains("墙") ||
            recipe.NodePresetId == "Preset_ElementCollapse" ||
            recipe.Recipe.Contains("AC-FI-GL-FU") ||
            recipe.Recipe.Contains("FU-FI-GL-AC"))
            return "BulwarkBeam";

        // 棱镜/折线/冰线 → PrismBeam 折线棱镜束
        if (recipe.AttackPattern.Contains("折线") ||
            recipe.AttackPattern.Contains("棱镜") ||
            recipe.NodePresetId == "Preset_RowResonance" ||
            recipe.Core == "Ice" && recipe.AttackPattern.Contains("射线"))
            return "PrismBeam";

        // 雷链/跳链/导电 → ArcBeam 细长多段跳链
        if (recipe.AttackPattern.Contains("雷链") ||
            recipe.AttackPattern.Contains("跳链") ||
            recipe.NodePresetId == "Preset_ConductiveChain" ||
            recipe.NodePresetId == "Preset_ChainUltimate")
            return "ArcBeam";

        // 射线/普通线路 → LightningBeam
        if (recipe.AttackPattern.Contains("射线"))
            return "LightningBeam";

        return "LightningBeam";
    }

    private static bool UsesBeam(RecipeRow recipe)
    {
        return recipe.AttackPattern.Contains("射线") ||
               recipe.AttackPattern.Contains("雷链") ||
               recipe.AttackPattern.Contains("墙") ||
               recipe.AttackPattern.Contains("折线") ||
               recipe.AttackPattern.Contains("棱镜") ||
               recipe.AttackPattern.Contains("跳链") ||
               recipe.NodePresetId == "Preset_BeamLane" ||
               recipe.NodePresetId == "Preset_ConductiveChain" ||
               recipe.NodePresetId == "Preset_RowResonance" ||
               recipe.NodePresetId == "Preset_ChainUltimate" ||
               recipe.NodePresetId == "Preset_ElementCollapse";
    }

    private static float ResolveDamage(RecipeRow recipe)
    {
        var value = recipe.SlotCount switch
        {
            2 => 72f,
            3 => 118f,
            4 => 175f,
            _ => 70f
        };

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
        var value = recipe.SlotCount switch
        {
            2 => 1f,
            3 => 1.25f,
            4 => 1.65f,
            _ => 1f
        };

        if (recipe.CombatRole.Contains("控制")) value -= 0.1f;
        if (recipe.CombatRole.Contains("收割") || recipe.CombatRole.Contains("斩杀")) value += 0.2f;
        return Mathf.Max(0.7f, value);
    }

    private static float ResolveCooldown(RecipeRow recipe)
    {
        var value = recipe.SlotCount switch
        {
            2 => 1.8f,
            3 => 3.4f,
            4 => 6.5f,
            _ => 2f
        };

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
        var value = recipe.SlotCount switch
        {
            2 => 0.08f,
            3 => 0.12f,
            4 => 0.18f,
            _ => 0.1f
        };

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
        return recipe.SlotCount switch
        {
            2 => 0.35f,
            3 => 0.55f,
            4 => 0.85f,
            _ => 0.4f
        };
    }

    // ---- new resolvers: 释放管线 ----

    private static float ResolveCastTime(RecipeRow recipe)
    {
        // cast_time 随槽位级递增，代表高等级技能前摇更长
        var value = recipe.SlotCount switch
        {
            2 => 0.12f,
            3 => 0.18f,
            4 => 0.28f,
            _ => 0.15f
        };

        if (recipe.AttackPattern.Contains("重型")) value += 0.06f;
        if (recipe.AttackPattern.Contains("高速")) value -= 0.04f;
        if (recipe.CombatRole.Contains("终极")) value += 0.06f;
        return Mathf.Max(0.04f, value);
    }

    private static float ResolveChannelDuration(RecipeRow recipe)
    {
        // 大多数技能非吟唱，只有部分持续性技能才有吟唱
        if (recipe.AttackPattern.Contains("射线") && recipe.NodePresetId == "Preset_BeamLane")
            return 1.2f;
        if (recipe.AttackPattern.Contains("雷链") && recipe.SlotCount >= 3)
            return 1.5f;
        if (recipe.CombatRole.Contains("引导") || recipe.AttackPattern.Contains("引导"))
            return 2f;
        return 0f;
    }

    private static float ResolvePostCastTime(RecipeRow recipe)
    {
        var value = recipe.SlotCount switch
        {
            2 => 0.08f,
            3 => 0.12f,
            4 => 0.2f,
            _ => 0.1f
        };

        if (recipe.AttackPattern.Contains("重型")) value += 0.06f;
        if (recipe.AttackPattern.Contains("高速")) value -= 0.03f;
        return Mathf.Max(0f, value);
    }

    private static string ResolveInterruptible(RecipeRow recipe)
    {
        // 大多数技能可打断，只有"终极"级或"不可打断"标记的不可打断
        if (recipe.CombatRole.Contains("终极") && recipe.SlotCount >= 4)
            return "false";
        if (recipe.RuleMutation.Contains("不可打断"))
            return "false";
        return "true";
    }

    private static float ResolveProjectileSpeed(RecipeRow recipe)
    {
        // 只有"地刺"/"投掷"/"射击"类技能才有投射物
        if (recipe.AttackPattern.Contains("地刺") || recipe.AttackPattern.Contains("地雷"))
            return 10f;
        if (recipe.Core == "Metal" && recipe.AttackPattern.Contains("投掷"))
            return 14f;
        if (recipe.AttackPattern.Contains("射击") || recipe.AttackPattern.Contains("轨迹"))
            return 12f;
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
        var value = recipe.SlotCount switch
        {
            2 => 15f,
            3 => 28f,
            4 => 45f,
            _ => 20f
        };

        if (recipe.CombatRole.Contains("终极")) value += 10f;
        if (recipe.CombatRole.Contains("控制")) value -= 3f;
        return Mathf.Max(5f, value);
    }

    // ---- CSV 读取 ----

    private static List<RecipeRow> ReadRecipes()
    {
        var text = AssetDatabase.LoadAssetAtPath<TextAsset>(RecipeCsvPath);
        var result = new List<RecipeRow>();
        if (text == null)
        {
            return result;
        }

        var lines = text.text.Replace("\r", string.Empty).Split('\n');
        if (lines.Length <= 1)
        {
            return result;
        }

        var headers = SplitCsvLine(lines[0]);
        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            var values = SplitCsvLine(lines[i]);
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < headers.Count; c++)
            {
                row[headers[c]] = c < values.Count ? values[c] : string.Empty;
            }

            result.Add(new RecipeRow
            {
                RecipeId = GetValue(row, "recipe_id"),
                SlotCount = int.TryParse(GetValue(row, "slot_count"), out var slotCount) ? slotCount : 2,
                Core = GetValue(row, "core"),
                Recipe = GetValue(row, "recipe"),
                Name = GetValue(row, "name"),
                CombatRole = GetValue(row, "combat_role"),
                AttackPattern = GetValue(row, "attack_pattern"),
                RuleMutation = GetValue(row, "rule_mutation"),
                StatusTags = GetValue(row, "status_tags"),
                TerrainTags = GetValue(row, "terrain_tags"),
                NodePresetId = GetValue(row, "node_preset_id")
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

    private sealed class RecipeRow
    {
        public string RecipeId;
        public int SlotCount;
        public string Core;
        public string Recipe;
        public string Name;
        public string CombatRole;
        public string AttackPattern;
        public string RuleMutation;
        public string StatusTags;
        public string TerrainTags;
        public string NodePresetId;
    }
}
