using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

public static class ConfigLoader
{
    private static readonly Dictionary<int, SkillConfig> SkillConfigs = new();
    private static readonly Dictionary<int, BuffConfig> BuffConfigs = new();
    private static readonly Dictionary<int, EffectConfig> EffectConfigs = new();
    private static readonly Dictionary<string, ReactionConfig> ReactionConfigs = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, TerrainConfig> TerrainConfigs = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, VFXArtProfileConfig> VfxArtProfiles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>GAS 架构：GameplayEffectData 配置字典，effect_id → GameplayEffectData</summary>
    private static readonly Dictionary<int, GameplayEffectData> GameplayEffectConfigs = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        ReloadAll();
    }

    public static void ReloadAll()
    {
        LoadSkillsFromCsv();
        LoadSkillVisualsFromCsv();
        LoadBuffsFromCsv();
        LoadEffectsFromCsv();
        LoadGameplayEffectsFromCsv();
        LoadReactionsFromCsv();
        LoadTerrainsFromCsv();
        LoadVfxArtProfilesFromCsv();
        Debug.Log(
            $"[ConfigLoader] Reload complete. Skills={SkillConfigs.Count}, Buffs={BuffConfigs.Count}, Effects={EffectConfigs.Count}, GameplayEffects={GameplayEffectConfigs.Count}, Reactions={ReactionConfigs.Count}, Terrains={TerrainConfigs.Count}, VFXProfiles={VfxArtProfiles.Count}");
    }

    public static SkillConfig GetSkillConfig(int id)
    {
        SkillConfigs.TryGetValue(id, out var cfg);
        return cfg;
    }

    /// <summary>
    ///     获取所有技能配置（按 SkillID 排序）。
    ///     用于编辑器工具、自动化测试、沙盒控制器等需要遍历全部技能的场合。
    /// </summary>
    public static IReadOnlyList<SkillConfig> GetAllSkillConfigs()
    {
        return SkillConfigs.Values.OrderBy(cfg => cfg.SkillID).ToList();
    }

    public static BuffConfig GetBuffConfig(int id)
    {
        BuffConfigs.TryGetValue(id, out var cfg);
        return cfg;
    }

    public static EffectConfig GetEffectConfig(int id)
    {
        EffectConfigs.TryGetValue(id, out var cfg);
        return cfg;
    }

    public static IReadOnlyList<EffectConfig> GetAllEffectConfigs()
    {
        return EffectConfigs.Values.OrderBy(cfg => cfg.EffectID).ToList();
    }

    public static ReactionConfig GetReactionConfig(string reactionNameOrId)
    {
        if (string.IsNullOrWhiteSpace(reactionNameOrId))
        {
            return null;
        }

        ReactionConfigs.TryGetValue(reactionNameOrId, out var cfg);
        return cfg;
    }

    public static TerrainConfig GetTerrainConfig(string terrainNameOrId)
    {
        if (string.IsNullOrWhiteSpace(terrainNameOrId))
        {
            return null;
        }

        TerrainConfigs.TryGetValue(terrainNameOrId, out var cfg);
        return cfg;
    }

    public static VFXArtProfileConfig GetVfxArtProfile(string profileKey)
    {
        if (string.IsNullOrWhiteSpace(profileKey))
        {
            return null;
        }

        VfxArtProfiles.TryGetValue(profileKey, out var cfg);
        return cfg;
    }

    /// <summary>
    ///     根据 effect_id 获取 GameplayEffectData 配置。
    ///     GAS 架构核心接口：ApplyEffectNode 通过此方法将 CSV 配置解析为运行时数据。
    /// </summary>
    public static GameplayEffectData GetGameplayEffectData(int effectId)
    {
        GameplayEffectConfigs.TryGetValue(effectId, out var data);
        return data;
    }

    /// <summary>获取所有 GameplayEffectData 配置</summary>
    public static IReadOnlyList<GameplayEffectData> GetAllGameplayEffectDatas()
    {
        return GameplayEffectConfigs.Values.OrderBy(d => d.EffectId).ToList();
    }

    /// <summary>公开的反应配置字典（用于编辑器工具和测试沙盒）。</summary>
    public static IReadOnlyDictionary<string, ReactionConfig> GetAllReactionConfigs() => ReactionConfigs;

    private static void LoadSkillsFromCsv()
    {
        SkillConfigs.Clear();

        var csv = LoadConfigText("Skill");
        if (csv == null)
        {
            Debug.LogError(
                "[ConfigLoader] Missing Skill.csv. The skill system requires CSV as the single source of numeric data.");
            return;
        }

        ForEachRow(csv.text, row =>
        {
            var cfg = new SkillConfig
            {
                SkillID = ParseInt(row, "skill_id"),
                SkillName = GetString(row, "name"),
                GraphPath = GetString(row, "graph_path"),
                ImpactVFXKey = GetString(row, "impact_vfx"),
                BeamVFXKey = GetString(row, "beam_vfx"),
                Damage = ParseFloat(row, "damage"),
                DamageRate = ParseFloat(row, "damage_rate"),
                Cooldown = ParseFloat(row, "cooldown"),
                CastRange = ParseFloat(row, "cast_range"),
                DelaySeconds = ParseFloat(row, "delay_seconds"),
                CritChance = ParseFloat(row, "crit_chance"),
                Radius = ParseFloat(row, "radius"),
                ChainCount = ParseFloat(row, "chain_count"),
                VFXDuration = ParseFloat(row, "vfx_duration"),
                CastTime = ParseFloat(row, "cast_time"),
                ChannelDuration = ParseFloat(row, "channel_duration"),
                PostCastTime = ParseFloat(row, "post_cast_time"),
                IsInterruptible = GetString(row, "interruptible").ToLowerInvariant() == "true",
                ProjectileSpeed = ParseFloat(row, "projectile_speed"),
                ProjectilePrefab = GetString(row, "projectile_prefab"),
                ResourceCost = ParseFloat(row, "resource_cost")
            };

            SkillConfigs[cfg.SkillID] = cfg;
        });
    }

    private static void LoadBuffsFromCsv()
    {
        BuffConfigs.Clear();

        var csv = LoadConfigText("Buff");
        if (csv == null)
        {
            Debug.LogWarning("[ConfigLoader] Missing Buff.csv.");
            return;
        }

        ForEachRow(csv.text, row =>
        {
            var cfg = new BuffConfig
            {
                BuffID = ParseInt(row, "buff_id"),
                BuffType = GetString(row, "type"),
                Value = ParseFloat(row, "value"),
                Duration = ParseFloat(row, "duration"),
                IconKey = GetString(row, "icon_key")
            };

            BuffConfigs[cfg.BuffID] = cfg;
        });
    }

    private static void LoadSkillVisualsFromCsv()
    {
        var csv = LoadConfigText("SkillVisual");
        if (csv == null)
        {
            Debug.LogWarning("[ConfigLoader] Missing SkillVisual.csv.");
            return;
        }

        ForEachRow(csv.text, row =>
        {
            var skillId = ParseInt(row, "skill_id");
            if (skillId <= 0 || !SkillConfigs.TryGetValue(skillId, out var cfg))
            {
                return;
            }

            cfg.VFXProfileKey = GetString(row, "profile_key");
            cfg.CastVFXKey = GetString(row, "cast_vfx");
            cfg.ReactionVFXKey = GetString(row, "reaction_vfx");
            cfg.TerrainVFXKey = GetString(row, "terrain_vfx");
            cfg.FinisherVFXKey = GetString(row, "finisher_vfx");
            cfg.VisualTheme = GetString(row, "visual_theme");
            cfg.VisualHook = GetString(row, "visual_hook");
            cfg.VisualNotes = GetString(row, "visual_notes");
        });
    }

    private static void LoadEffectsFromCsv()
    {
        EffectConfigs.Clear();

        var csv = LoadConfigText("Effect");
        if (csv == null)
        {
            Debug.LogWarning("[ConfigLoader] Missing Effect.csv.");
            return;
        }

        ForEachRow(csv.text, row =>
        {
            var cfg = new EffectConfig
            {
                EffectID = ParseInt(row, "effect_id"),
                EffectKey = GetString(row, "effect_key"),
                PrefabName = GetString(row, "prefab_name"),
                Scale = ParseFloat(row, "scale", 1f),
                Duration = ParseFloat(row, "duration", 1f),
                WarmupCount = ParseInt(row, "warmup_count")
            };

            EffectConfigs[cfg.EffectID] = cfg;
        });
    }

    private static void LoadGameplayEffectsFromCsv()
    {
        GameplayEffectConfigs.Clear();

        var csv = LoadConfigText("GameplayEffect");
        if (csv == null)
        {
            Debug.LogWarning("[ConfigLoader] Missing GameplayEffect.csv.");
            return;
        }

        ForEachRow(csv.text, row =>
        {
            var data = new GameplayEffectData
            {
                EffectId = ParseInt(row, "effect_id"),
                EffectName = GetString(row, "effect_name"),
                DurationPolicy = (GEDurationPolicy)ParseInt(row, "duration_policy"),
                Duration = ParseFloat(row, "duration"),
                Period = ParseFloat(row, "period"),
                BaseDamage = ParseFloat(row, "base_damage"),
                CritChanceBonus = ParseFloat(row, "crit_chance_bonus"),
                CritMultiplier = ParseFloat(row, "crit_multiplier", 2f),
                StackPolicy = (GEStackPolicy)ParseInt(row, "stack_policy"),
                MaxStacks = ParseInt(row, "max_stacks", 1),
                IsAreaOfEffect = GetString(row, "is_area_of_effect").ToLowerInvariant() == "true",
                AreaRadius = ParseFloat(row, "area_radius"),
                AreaMaxTargets = ParseInt(row, "area_max_targets"),
                BypassShields = GetString(row, "bypass_shields").ToLowerInvariant() == "true",
                IgnoreInvulnerability = GetString(row, "ignore_invulnerability").ToLowerInvariant() == "true",
                GrantedTags = ParseTagList(row, "granted_tags"),
                RequiredTargetTags = ParseTagList(row, "required_target_tags"),
                ImmuneTags = ParseTagList(row, "immune_tags")
            };

            if (data.EffectId > 0)
            {
                GameplayEffectConfigs[data.EffectId] = data;
            }
        });
    }

    /// <summary>解析管道符分隔的标签列表（如 burn|chill|conductive）</summary>
    private static List<string> ParseTagList(IReadOnlyDictionary<string, string> row, string key)
    {
        var raw = GetString(row, key);
        if (string.IsNullOrWhiteSpace(raw)) return new List<string>();

        var tags = raw.Split('|', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();
        foreach (var tag in tags)
        {
            var trimmed = tag.Trim();
            if (!string.IsNullOrEmpty(trimmed)) result.Add(trimmed);
        }

        return result;
    }

    private static void LoadReactionsFromCsv()
    {
        ReactionConfigs.Clear();

        var csv = LoadConfigText("Reaction");
        if (csv == null)
        {
            Debug.LogWarning("[ConfigLoader] Missing Reaction.csv.");
            return;
        }

        ForEachRow(csv.text, row =>
        {
            var cfg = new ReactionConfig
            {
                ReactionID = GetString(row, "reaction_id"),
                ReactionName = GetString(row, "reaction_name"),
                RequiredStatuses = GetString(row, "required_statuses"),
                TriggerSource = GetString(row, "trigger_source"),
                EffectSummary = GetString(row, "effect_summary"),
                DamageMultiplier = ParseFloat(row, "damage_multiplier", 1f),
                CcSeconds = ParseFloat(row, "cc_seconds"),
                TerrainResult = GetString(row, "terrain_result"),
                Notes = GetString(row, "notes")
            };

            if (!string.IsNullOrWhiteSpace(cfg.ReactionID))
            {
                ReactionConfigs[cfg.ReactionID] = cfg;
            }

            if (!string.IsNullOrWhiteSpace(cfg.ReactionName))
            {
                ReactionConfigs[cfg.ReactionName] = cfg;
            }
        });
    }

    private static void LoadTerrainsFromCsv()
    {
        TerrainConfigs.Clear();

        var csv = LoadConfigText("Terrain");
        if (csv == null)
        {
            Debug.LogWarning("[ConfigLoader] Missing Terrain.csv.");
            return;
        }

        ForEachRow(csv.text, row =>
        {
            var cfg = new TerrainConfig
            {
                TerrainID = GetString(row, "terrain_id"),
                TerrainName = GetString(row, "terrain_name"),
                AppliedBy = GetString(row, "applied_by"),
                BonusEffect = GetString(row, "bonus_effect"),
                EnemyEffect = GetString(row, "enemy_effect"),
                DurationSeconds = ParseFloat(row, "duration_seconds", 4f),
                StackRule = GetString(row, "stack_rule")
            };

            if (!string.IsNullOrWhiteSpace(cfg.TerrainID))
            {
                TerrainConfigs[cfg.TerrainID] = cfg;
            }

            if (!string.IsNullOrWhiteSpace(cfg.TerrainName))
            {
                TerrainConfigs[cfg.TerrainName] = cfg;
            }
        });
    }

    private static void LoadVfxArtProfilesFromCsv()
    {
        VfxArtProfiles.Clear();

        var csv = LoadConfigText("VFXArtProfile");
        if (csv == null)
        {
            Debug.LogWarning("[ConfigLoader] Missing VFXArtProfile.csv.");
            return;
        }

        ForEachRow(csv.text, row =>
        {
            var cfg = new VFXArtProfileConfig
            {
                ProfileKey = GetString(row, "profile_key"),
                DisplayName = GetString(row, "display_name"),
                CoreElement = GetString(row, "core_element"),
                PrimaryColorHex = GetString(row, "primary_color"),
                AccentColorHex = GetString(row, "accent_color"),
                ScaleMultiplier = ParseFloat(row, "scale_multiplier", 1f),
                WidthMultiplier = ParseFloat(row, "width_multiplier", 1f),
                Length = ParseFloat(row, "length", 5f),
                DurationMultiplier = ParseFloat(row, "duration_multiplier", 1f),
                Intensity = ParseFloat(row, "intensity", 1f),
                Notes = GetString(row, "notes")
            };

            if (string.IsNullOrWhiteSpace(cfg.ProfileKey))
            {
                return;
            }

            VfxArtProfiles[cfg.ProfileKey] = cfg;
        });
    }

    private static TextAsset LoadConfigText(string fileName)
    {
        return Resources.Load<TextAsset>($"Config/{fileName}") ?? Resources.Load<TextAsset>(fileName);
    }

    private static void ForEachRow(string csvText, Action<Dictionary<string, string>> onRow)
    {
        var lines = csvText.Replace("\r", string.Empty).Split('\n');
        if (lines.Length == 0) return;

        var headers = SplitCsvLine(lines[0]);
        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            var cols = SplitCsvLine(lines[i]);
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < headers.Count; c++) row[headers[c]] = c < cols.Count ? cols[c] : string.Empty;

            onRow(row);
        }
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

    private static string GetString(IReadOnlyDictionary<string, string> row, string key, string defaultValue = "")
    {
        return row.TryGetValue(key, out var value) ? value : defaultValue;
    }

    private static int ParseInt(IReadOnlyDictionary<string, string> row, string key, int defaultValue = 0)
    {
        if (!row.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) return defaultValue;

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : defaultValue;
    }

    private static float ParseFloat(IReadOnlyDictionary<string, string> row, string key, float defaultValue = 0f)
    {
        if (!row.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) return defaultValue;

        return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : defaultValue;
    }
}
