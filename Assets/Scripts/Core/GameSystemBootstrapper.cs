using UnityEngine;

// ============================================================
//  游戏系统初始化引导器 (GameSystemBootstrapper)
//  在场景加载时自动注册所有管线组件：TagDamageRule、ReactionHandler 等。
//  确保技能 → GAS → 伤害管线 → 表现层的全链路连通。
//
//  自动初始化链：
//  ConfigLoader         → BeforeSceneLoad 加载 CSV
//  ReactionEngineGlobal → BeforeSceneLoad 注册 ReactionEngine 到 EffectSystem
//  GameSystemBootstrapper → AfterSceneLoad 注册 TagDamageRule 到 DamagePipeline
// ============================================================

/// <summary>
///     游戏系统初始化引导器。
///     在场景加载后自动注册 DamagePipeline 的标签伤害规则。
///     无需手动挂载，通过 [RuntimeInitializeOnLoadMethod] 自动执行。
/// </summary>
public static class GameSystemBootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void Initialize()
    {
        RegisterDefaultTagDamageRules();
        Debug.Log("[GameSystemBootstrapper] Pipeline initialization complete. TagDamageRules registered.");
    }

    /// <summary>
    ///     注册默认的标签伤害规则到 DamagePipeline。
    ///     规则来源：ReactionConfig.csv + 硬编码兜底。
    ///     技能图只需在 ApplyEffectNode 中携带标签，管线自动处理。
    /// </summary>
    private static void RegisterDefaultTagDamageRules()
    {
        // 尝试从 CSV 配置加载
        var loaded = LoadFromConfig();
        if (loaded > 0) return;

        // 兜底：硬编码默认规则
        RegisterFallbackRules();
    }

    private static int LoadFromConfig()
    {
        var count = 0;
        var allReactions = ConfigLoader.GetAllReactionConfigs();
        if (allReactions == null || allReactions.Count == 0) return 0;

        foreach (var kvp in allReactions)
        {
            var cfg = kvp.Value;
            if (cfg == null || string.IsNullOrEmpty(cfg.TriggerSource)) continue;

            var rule = new TagDamageRule
            {
                RuleName = cfg.ReactionName ?? cfg.ReactionID,
                RequiredSourceTag = cfg.TriggerSource,
                RequiredTargetTag = cfg.RequiredStatuses,
                DamageMultiplier = cfg.DamageMultiplier > 0f ? cfg.DamageMultiplier : 1f,
                BonusDamage = 0f
            };

            DamagePipeline.RegisterTagRule(rule);
            count++;
        }

        return count;
    }

    private static void RegisterFallbackRules()
    {
        // 融化：火打冰 → x2.0
        DamagePipeline.RegisterTagRule(new TagDamageRule
        {
            RuleName = "融化",
            RequiredSourceTag = "element.fire",
            RequiredTargetTag = "status.chill",
            DamageMultiplier = 2.0f
        });

        // 蒸发：火打水 → x1.5
        DamagePipeline.RegisterTagRule(new TagDamageRule
        {
            RuleName = "蒸发",
            RequiredSourceTag = "element.fire",
            RequiredTargetTag = "status.wet",
            DamageMultiplier = 1.5f
        });

        // 超载：雷打火 → x1.5 + 额外 10
        DamagePipeline.RegisterTagRule(new TagDamageRule
        {
            RuleName = "超载",
            RequiredSourceTag = "element.lightning",
            RequiredTargetTag = "status.burning",
            DamageMultiplier = 1.5f,
            BonusDamage = 10f
        });

        // 感电：雷打水 → x1.2
        DamagePipeline.RegisterTagRule(new TagDamageRule
        {
            RuleName = "感电",
            RequiredSourceTag = "element.lightning",
            RequiredTargetTag = "status.wet",
            DamageMultiplier = 1.2f
        });

        // 脆弱：受伤 +30%
        DamagePipeline.RegisterTagRule(new TagDamageRule
        {
            RuleName = "脆弱",
            RequiredSourceTag = "element.fire",
            RequiredTargetTag = "status.vulnerable",
            DamageMultiplier = 1.3f
        });
    }
}
