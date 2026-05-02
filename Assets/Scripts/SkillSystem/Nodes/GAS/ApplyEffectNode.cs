using UnityEngine;

// ============================================================
//  应用效果节点 (ApplyEffectNode)
//  唯一与战斗相关的标准节点。
//  无任何逻辑参数，仅接受 GameplayEffectData 配置。
//  继承 SkillNode 以兼容 SkillGraph 节点图系统。
//
//  维度5核心原则：技能图只管"行为触发"，不管是"规则结算"。
//  本节点只是简单地丢出一个携带 tags 的原始伤害事件，
//  具体的翻倍/暴击/反应逻辑交给 DamagePipeline + ReactionEngine。
// ============================================================

/// <summary>
///     应用效果节点。
///     将 GameplayEffectData 投递到 EffectSystem 进行处理。
///     继承 SkillNode，通过 SkillContext 获取施法者与目标。
/// </summary>
[CreateAssetMenu(fileName = "ApplyEffectNode", menuName = "Skill System/Nodes/GAS/ApplyEffect")]
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



    /// <summary>
    ///     额外标签（分号分隔），投递到 DamagePipeline 供 GE 事件拦截。
    ///     维度5核心：技能图通过标签声明"我是什么属性"，
    ///     管线自动处理元素反应、暴击、被动等规则。
    ///     示例："element.fire" → 管线自动检测目标 status.chill → 触发融化 x2.0
    /// </summary>
    [Tooltip("额外标签（分号分隔，如 element.fire;element.ice）")] public string extraTags = string.Empty;

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

        // 维度5：额外标签透传（技能图行为触发 + 标签传递）
        if (!string.IsNullOrEmpty(extraTags))
        {
            var extraTagArray = extraTags.Split(';');
            foreach (var tag in extraTagArray)
            {
                var trimmed = tag.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    context.AddSourceTag(trimmed);
            }
        }

        // 添加额外标签（维度5：标签驱动规则）
        if (!string.IsNullOrWhiteSpace(extraTags))
        {
            foreach (var tag in extraTags.Split(';'))
            {
                var trimmed = tag.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    context.AddSourceTag(trimmed);
            }
        }

        // 投递到 EffectSystem
        var success = EffectSystem.ApplyEffect(context, effectData);

        // 调试输出（通过预览获取计算值，不重复应用）
        LastCalculatedDamage = EffectSystem.PreviewDamage(context, effectData);
        LastResult = success ? "Applied" : "Failed";

        return NodeTickResult.Success;
    }

    public override bool CanCompile => true;

    public override System.Collections.Generic.List<SkillEffectData> Compile(SkillContext ctx = null)
    {
        var effectData = ResolveEffectData();

        string buffKey = effectData != null
            ? (effectData.EffectId > 0 ? effectData.EffectId.ToString() : effectData.EffectName)
            : (EffectId > 0 ? EffectId.ToString() : string.Empty);

        var effect = SkillEffectData.CreateApplyBuff(
            buffKey,
            effectData?.Duration ?? 0f,
            SkillEffectTargetMode.PrimaryTarget,
            AbilityLevel
        );

        if (effectData?.GrantedTags != null)
        {
            effect.TagsToApply = new System.Collections.Generic.List<string>(effectData.GrantedTags);
        }

        return new System.Collections.Generic.List<SkillEffectData> { effect };
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
