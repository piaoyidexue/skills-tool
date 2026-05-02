using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

public static class ConfigLoader
{
    /// <summary>技能配置字典，skill_id → SkillConfig</summary>
    private static readonly Dictionary<int, SkillConfig> SkillConfigs = new();
    
    
    
    private static readonly Dictionary<int, BuffConfig> BuffConfigs = new();
    private static readonly Dictionary<int, EffectConfig> EffectConfigs = new();
    
    /// <summary>反应配置字典，reaction_name → ReactionConfig</summary>
    private static readonly Dictionary<string, ReactionConfig> ReactionConfigs = new(StringComparer.OrdinalIgnoreCase);
    
    /// <summary>地形配置字典，terrain_name → TerrainConfig</summary>
    private static readonly Dictionary<string, TerrainConfig> TerrainConfigs = new(StringComparer.OrdinalIgnoreCase);
    
    /// <summary>VFX艺术配置字典，profile_key → VFXArtProfileConfig</summary>
    private static readonly Dictionary<string, VFXArtProfileConfig> VfxArtProfiles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>物品配置字典，item_id → ItemConfig</summary>
    private static readonly Dictionary<int, ItemConfig> ItemConfigs = new();

    /// <summary>GAS 架构：GameplayEffectData 配置字典，effect_id → GameplayEffectData</summary>
    private static readonly Dictionary<int, GameplayEffectData> GameplayEffectConfigs = new();

    /// <summary>音频配置字典，audio_id → AudioConfig</summary>
    private static readonly Dictionary<int, AudioConfig> AudioConfigs = new();

    /// <summary>投射物配置字典，projectile_id → ProjectileConfig</summary>
    private static readonly Dictionary<string, ProjectileConfig> ProjectileConfigs = new(StringComparer.OrdinalIgnoreCase);

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
        LoadItemsFromCsv();
        LoadAudioFromCsv();
        LoadProjectileConfigsFromCsv();
        Debug.Log(
            $"[ConfigLoader] Reload complete. Skills={SkillConfigs.Count}," +
            $" Buffs={BuffConfigs.Count}, Effects={EffectConfigs.Count}," +
            $" GameplayEffects={GameplayEffectConfigs.Count}," +
            $" Reactions={ReactionConfigs.Count}," +
            $" Terrains={TerrainConfigs.Count}," +
            $" VFXProfiles={VfxArtProfiles.Count}," +
            $" Items={ItemConfigs.Count}," +
            $" Audio={AudioConfigs.Count}");
    }

    private static void LoadProjectileConfigsFromCsv()
    {
        ProjectileConfigs.Clear();

        var csv = LoadConfigText("ProjectileConfig");
        if (csv == null)
        {
            Debug.LogWarning("[ConfigLoader] Missing ProjectileConfig.csv.");
            return;
        }

        ForEachRow(csv.text, row =>
        {
            var cfg = new ProjectileConfig
            {
                ProjectileID = GetString(row, "projectile_id"),
                Name = GetString(row, "name"),
                PrefabKey = GetString(row, "prefab_key"),
                Trajectory = ParseInt(row, "trajectory"),
                HitRadius = ParseFloat(row, "hit_radius", 0.5f),
                Lifetime = ParseFloat(row, "lifetime", 5f),
                Gravity = ParseFloat(row, "gravity", 9.8f),
                Tags = ParseTagList(row, "tags")
            };

            if (!string.IsNullOrWhiteSpace(cfg.ProjectileID))
                ProjectileConfigs[cfg.ProjectileID] = cfg;
        });
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

    public static IReadOnlyList<BuffConfig> GetAllBuffConfigs()
    {
        return BuffConfigs.Values.OrderBy(cfg => cfg.BuffID).ToList();
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

    /// <summary>
    ///     根据 item_id 获取物品配置。
    /// </summary>
    public static ItemConfig GetItemConfig(int itemId)
    {
        ItemConfigs.TryGetValue(itemId, out var cfg);
        return cfg;
    }

    /// <summary>获取所有物品配置</summary>
    public static IReadOnlyList<ItemConfig> GetAllItemConfigs()
    {
        return ItemConfigs.Values.OrderBy(cfg => cfg.ItemID).ToList();
    }

    /// <summary>
    ///     根据 audio_id 获取音频配置。
    /// </summary>
    public static AudioConfig GetAudioConfig(int audioId)
    {
        AudioConfigs.TryGetValue(audioId, out var cfg);
        return cfg;
    }

    /// <summary>获取所有音频配置</summary>
    public static IReadOnlyList<AudioConfig> GetAllAudioConfigs()
    {
        return AudioConfigs.Values.OrderBy(cfg => cfg.AudioId).ToList();
    }

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
                SkillID = ParseInt(row, "skill_id"),           // 技能唯一ID
                SkillName = GetString(row, "name"),             // 技能名称
                GraphPath = GetString(row, "graph_path"),       // 行为图路径
                ImpactVFXKey = GetString(row, "impact_vfx"),   // 命中特效Key
                BeamVFXKey = GetString(row, "beam_vfx"),        // 射线特效Key
                Damage = ParseFloat(row, "damage"),             // 基础伤害值
                DamageRate = ParseFloat(row, "damage_rate"),    // 伤害倍率
                Cooldown = ParseFloat(row, "cooldown"),         // 冷却时间（秒）
                CastRange = ParseFloat(row, "cast_range"),      // 施法距离
                DelaySeconds = ParseFloat(row, "delay_seconds"),// 延迟触发时间（秒）
                CritChance = ParseFloat(row, "crit_chance"),    // 暴击概率（0~1）
                Radius = ParseFloat(row, "radius"),             // 范围半径
                ChainCount = ParseFloat(row, "chain_count"),    // 链式跳跃次数
                VFXDuration = ParseFloat(row, "vfx_duration"),  // 特效持续时间（秒）
                CastTime = ParseFloat(row, "cast_time"),        // 前摇时间（秒）
                ChannelDuration = ParseFloat(row, "channel_duration"), // 引导持续时间（秒）
                PostCastTime = ParseFloat(row, "post_cast_time"),      // 后摇时间（秒）
                IsInterruptible = GetString(row, "interruptible").ToLowerInvariant() == "true", // 是否可被打断
                ProjectileSpeed = ParseFloat(row, "projectile_speed"), // 投射物飞行速度
                ProjectilePrefab = GetString(row, "projectile_prefab"),// 投射物预制体名称
                ProjectileTrajectory = ParseInt(row, "projectile_trajectory"),    // 投射物弹道类型（0=直线, 1=追踪, 2=抛物线）
                ProjectileHitRadius = ParseFloat(row, "projectile_hit_radius", 0.5f), // 投射物命中判定半径
                ProjectileLifetime = ParseFloat(row, "projectile_lifetime", 5f),    // 投射物最大存活时间（秒）
                ProjectileGravity = ParseFloat(row, "projectile_gravity", 9.8f),    // 投射物抛物线重力系数
                ProjectileTags = ParseTagList(row, "projectile_tags"),              // 投射物携带的元素/状态标签
                ResourceCost = ParseFloat(row, "resource_cost")        // 技能资源消耗
            };

            // ===== 投射物配置覆盖逻辑 =====
            // 如果指定了 ProjectileConfigKey，则从 ProjectileConfig.csv 加载并覆盖内联字段
            if (!string.IsNullOrWhiteSpace(cfg.ProjectileConfigKey) && 
                ProjectileConfigs.TryGetValue(cfg.ProjectileConfigKey, out var projectileCfg))
            {
                cfg.ProjectilePrefab = projectileCfg.PrefabKey ?? cfg.ProjectilePrefab;
                cfg.ProjectileTrajectory = projectileCfg.Trajectory;
                cfg.ProjectileHitRadius = projectileCfg.HitRadius > 0f ? projectileCfg.HitRadius : cfg.ProjectileHitRadius;
                cfg.ProjectileLifetime = projectileCfg.Lifetime > 0f ? projectileCfg.Lifetime : cfg.ProjectileLifetime;
                cfg.ProjectileGravity = projectileCfg.Gravity > 0f ? projectileCfg.Gravity : cfg.ProjectileGravity;
                cfg.ProjectileTags = projectileCfg.Tags;
            }

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
                BuffID = ParseInt(row, "buff_id"),         // Buff唯一ID
                BuffType = GetString(row, "type"),          // Buff类型
                Value = ParseFloat(row, "value"),           // 数值
                Duration = ParseFloat(row, "duration"),     // 持续时间（秒）
                IconKey = GetString(row, "icon_key")        // 图标Key
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

            cfg.VFXProfileKey = GetString(row, "profile_key");    // VFX 艺术风格配置Key
            cfg.CastVFXKey = GetString(row, "cast_vfx");          // 施法特效Key
            cfg.ReactionVFXKey = GetString(row, "reaction_vfx");  // 元素反应特效Key
            cfg.TerrainVFXKey = GetString(row, "terrain_vfx");    // 地形特效Key
            cfg.FinisherVFXKey = GetString(row, "finisher_vfx");  // 终结技特效Key
            cfg.VisualTheme = GetString(row, "visual_theme");     // 视觉主题标签
            cfg.VisualHook = GetString(row, "visual_hook");       // 视觉挂点标识
            cfg.VisualNotes = GetString(row, "visual_notes");     // 视觉备注说明
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
                EffectID = ParseInt(row, "effect_id"),           // 特效唯一ID
                EffectKey = GetString(row, "effect_key"),        // 特效Key
                PrefabName = GetString(row, "prefab_name"),      // 预制体名称
                Scale = ParseFloat(row, "scale", 1f),            // 缩放比例
                Duration = ParseFloat(row, "duration", 1f),      // 持续时间（秒）
                WarmupCount = ParseInt(row, "warmup_count")      // 预热次数
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
                EffectId = ParseInt(row, "effect_id"),                               // 效果唯一ID
                EffectName = GetString(row, "effect_name"),                           // 效果名称
                DurationPolicy = (GEDurationPolicy)ParseInt(row, "duration_policy"),  // 持续时间策略
                Duration = ParseFloat(row, "duration"),                               // 持续时间
                Period = ParseFloat(row, "period"),                                   // 周期触发时间
                BaseDamage = ParseFloat(row, "base_damage"),                          // 基础伤害
                CritChanceBonus = ParseFloat(row, "crit_chance_bonus"),               // 暴击率加成
                CritMultiplier = ParseFloat(row, "crit_multiplier", 2f),              // 暴击伤害倍率
                StackPolicy = (GEStackPolicy)ParseInt(row, "stack_policy"),           // 叠加策略
                MaxStacks = ParseInt(row, "max_stacks", 1),                           // 最大叠加层数
                IsAreaOfEffect = GetString(row, "is_area_of_effect").ToLowerInvariant() == "true", // 是否范围效果
                AreaRadius = ParseFloat(row, "area_radius"),                          // 范围半径
                AreaMaxTargets = ParseInt(row, "area_max_targets"),                   // 范围最大目标数
                BypassShields = GetString(row, "bypass_shields").ToLowerInvariant() == "true", // 是否无视护盾
                IgnoreInvulnerability = GetString(row, "ignore_invulnerability").ToLowerInvariant() == "true", // 是否无视无敌
                GrantedTags = ParseTagList(row, "granted_tags"),                      // 赋予的标签列表
                RequiredTargetTags = ParseTagList(row, "required_target_tags"),       // 目标所需标签列表
                ImmuneTags = ParseTagList(row, "immune_tags")                         // 免疫标签列表
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
                ReactionID = GetString(row, "reaction_id"),                 // 反应唯一ID
                ReactionName = GetString(row, "reaction_name"),             // 反应名称
                RequiredStatuses = GetString(row, "required_statuses"),     // 所需状态
                TriggerSource = GetString(row, "trigger_source"),           // 触发源
                EffectSummary = GetString(row, "effect_summary"),           // 效果摘要
                DamageMultiplier = ParseFloat(row, "damage_multiplier", 1f),// 伤害倍率
                CcSeconds = ParseFloat(row, "cc_seconds"),                  // 控制时间（秒）
                TerrainResult = GetString(row, "terrain_result"),           // 地形结果
                Notes = GetString(row, "notes")                             // 备注说明
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
                TerrainID = GetString(row, "terrain_id"),                   // 地形唯一ID
                TerrainName = GetString(row, "terrain_name"),               // 地形名称
                AppliedBy = GetString(row, "applied_by"),                   // 施加者
                BonusEffect = GetString(row, "bonus_effect"),               // 增益效果
                EnemyEffect = GetString(row, "enemy_effect"),               // 减益效果
                DurationSeconds = ParseFloat(row, "duration_seconds", 4f),  // 持续时间（秒）
                StackRule = GetString(row, "stack_rule")                    // 叠加规则
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

    private static void LoadItemsFromCsv()
    {
        ItemConfigs.Clear();

        var csv = LoadConfigText("Item");
        if (csv == null)
        {
            Debug.LogWarning("[ConfigLoader] Missing Item.csv.");
            return;
        }

        ForEachRow(csv.text, row =>
        {
            var cfg = new ItemConfig
            {
                ItemID = ParseInt(row, "item_id"),                           // 物品唯一ID
                ItemName = GetString(row, "item_name"),                     // 物品名称
                Description = GetString(row, "description"),               // 物品描述
                Type = (ItemType)ParseInt(row, "type"),                     // 物品类型
                MaxStack = ParseInt(row, "max_stack", 1),                   // 最大堆叠数
                GameplayEffectID = ParseInt(row, "ge_id"),                  // 关联的GE ID
                SkillGraphID = ParseInt(row, "skill_graph_id"),             // 关联的技能图ID
                EquipSlot = (EquipmentSlot)ParseInt(row, "equip_slot"),     // 装备槽位
                Quality = ParseInt(row, "quality"),                         // 品质
                SellPrice = ParseInt(row, "sell_price"),                    // 出售价格
                IconKey = GetString(row, "icon_key"),                       // 图标Key
                CanDiscard = GetString(row, "can_discard").ToLowerInvariant() == "true" // 是否可丢弃
            };
            cfg.Tags = ParseTagList(row, "tags");                           // 额外标签

            if (cfg.ItemID > 0)
            {
                ItemConfigs[cfg.ItemID] = cfg;
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
                ProfileKey = GetString(row, "profile_key"),                 // 配置Key
                DisplayName = GetString(row, "display_name"),               // 显示名称
                CoreElement = GetString(row, "core_element"),               // 核心元素
                PrimaryColorHex = GetString(row, "primary_color"),          // 主色调十六进制值
                AccentColorHex = GetString(row, "accent_color"),            // 辅助色十六进制值
                ScaleMultiplier = ParseFloat(row, "scale_multiplier", 1f),  // 缩放乘数
                WidthMultiplier = ParseFloat(row, "width_multiplier", 1f),  // 宽度乘数
                Length = ParseFloat(row, "length", 5f),                     // 长度
                DurationMultiplier = ParseFloat(row, "duration_multiplier", 1f), // 持续时间乘数
                Intensity = ParseFloat(row, "intensity", 1f),               // 强度
                Notes = GetString(row, "notes")                             // 备注说明
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

    // ============================================================
    //  Audio.csv 解析
    // ============================================================

    private static void LoadAudioFromCsv()
    {
        AudioConfigs.Clear();

        var csv = LoadConfigText("Audio");
        if (csv == null)
        {
            Debug.LogWarning("[ConfigLoader] Missing Audio.csv.");
            return;
        }

        ForEachRow(csv.text, row =>
        {
            var cfg = new AudioConfig
            {
                AudioId = ParseInt(row, "audio_id"),
                ResourcePath = GetString(row, "resource_path"),
                Category = ParseAudioCategory(GetString(row, "category")),
                VolumeWeight = ParseFloat(row, "volume_weight", 1f),
                Is3D = GetString(row, "is_3d").ToLowerInvariant() == "true",
                MaxDistance = ParseFloat(row, "max_distance", 50f),
                Loop = GetString(row, "loop").ToLowerInvariant() == "true",
                MaxConcurrent = ParseInt(row, "max_concurrent", 0),
                Priority = ParseInt(row, "priority", 0),
                FadeInDuration = ParseFloat(row, "fade_in", 0f),
                FadeOutDuration = ParseFloat(row, "fade_out", 0f),
                Description = GetString(row, "description")
            };

            AudioConfigs[cfg.AudioId] = cfg;
        });
    }

    private static AudioCategory ParseAudioCategory(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return AudioCategory.SFX;
        return raw.ToUpperInvariant() switch
        {
            "BGM" => AudioCategory.BGM,
            "SFX" => AudioCategory.SFX,
            "UI" => AudioCategory.UI,
            "VOICE" => AudioCategory.Voice,
            _ => AudioCategory.SFX
        };
    }
}
