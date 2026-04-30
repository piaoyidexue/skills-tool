using UnityEngine;

// ============================================================
//  应用效果节点 (ApplyEffectNode)
//  唯一与战斗相关的标准节点。
//  无任何逻辑参数，仅接受 GameplayEffectData 配置。
//  继承 SkillNode 以兼容 SkillGraph 节点图系统。
// ============================================================

/// <summary>
///     应用效果节点。
///     将 GameplayEffectData 投递到 EffectSystem 进行处理。
///     继承 SkillNode，通过 SkillContext 获取施法者与目标。
/// </summary>
public class ApplyEffectNode : SkillNodeBase
{
    /// <summary>效果数据（直接在 Inspector 中配置）</summary>
    [Tooltip("效果数据配置")] public GameplayEffectData EffectData;

    /// <summary>效果ID（从配置表查找，当 EffectData 为空时使用）</summary>
    [Tooltip("效果ID")] public int EffectId;

    /// <summary>技能等级（用于数值缩放）</summary>
    [Tooltip("技能等级")] public int AbilityLevel = 1;

    /// <summary>是否使用黑板中的 CurrentTarget 作为目标</summary>
    [Tooltip("使用黑板目标")] public bool UseBlackboardTarget = true;

    // ---- 运行时输出（调试用，可选） ----

    /// <summary>最近一次计算伤害值（调试输出）</summary>
    [HideInInspector] public float LastCalculatedDamage;

    /// <summary>最近一次是否暴击（调试输出）</summary>
    [HideInInspector] public bool LastWasCriticalHit;

    /// <summary>最近一次执行结果</summary>
    [HideInInspector] public string LastResult = string.Empty;

    // ============================================================
    //  SkillNode 抽象实现
    // ============================================================

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        if (ctx == null) return NodeTickResult.Success;

        // 解析目标
        var target = ResolveTarget(ctx);

        // 获取效果数据
        var effectData = ResolveEffectData();
        if (effectData == null)
        {
            Debug.LogError($"[ApplyEffectNode] No EffectData found for EffectId={EffectId}");
            LastResult = "NoData";
            return NodeTickResult.Success;
        }

        // 解析目标点
        var targetPoint = target != null ? target.position : Vector3.zero;

        // 构建上下文
        var context = EffectContext.Create(ctx.Caster, target, targetPoint,
            AbilityLevel, effectData.EffectId);

        // 添加源标签
        if (effectData.GrantedTags != null)
        {
            foreach (var tag in effectData.GrantedTags)
                context.AddSourceTag(tag);
        }

        // 投递到 EffectSystem
        var success = EffectSystem.ApplyEffect(context, effectData);

        // 调试输出（通过预览获取计算值，不重复应用）
        LastCalculatedDamage = EffectSystem.PreviewDamage(context, effectData);
        LastResult = success ? "Applied" : "Failed";

        return NodeTickResult.Success;
    }

    // ============================================================
    //  内部方法
    // ============================================================

    private Transform ResolveTarget(SkillContext ctx)
    {
        // 优先使用 SkillContext 中的目标
        if (ctx.Target != null) return ctx.Target;

        // 尝试从黑板获取 CurrentTarget
        if (UseBlackboardTarget)
        {
            var bbTarget = ctx.Blackboard.GetValue<Transform>(BBKey.CurrentTarget, null);
            if (bbTarget != null) return bbTarget;
        }

        return null;
    }

    private GameplayEffectData ResolveEffectData()
    {
        // 优先使用直接配置的数据
        if (EffectData != null) return EffectData;

        // 从 GAS 配置表查找（GameplayEffect.csv）
        if (EffectId > 0)
        {
            return ConfigLoader.GetGameplayEffectData(EffectId);
        }

        return null;
    }
}
