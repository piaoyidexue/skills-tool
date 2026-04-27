using System.Collections;
using UnityEngine;

/// <summary>
///     吟唱管理节点 —— 持续引导技能。
///     每 tickInterval 触发一次 tick，总时长 channelDuration。
///     期间可被打断。进度记录在 Blackboard.ChannelProgress。
///     【与 SkillCaster 的关系】ChannelNode 自主管理吟唱 timing，
///     不经过 CastPipeline 等待；CastPipeline 在 ChannelNode 执行期间处于 Executing 阶段。
/// </summary>
public class ChannelNode : SkillNode
{
    /// <summary>总吟唱时长（秒），留空从 SkillConfig.ChannelDuration 读取</summary>
    public FloatBinding channelDuration = new()
    {
        Source = FloatBinding.SourceType.SkillConfig,
        SkillField = SkillFloatField.ChannelDuration
    };

    /// <summary>每次 tick 间隔（秒）</summary>
    public float tickInterval = 0.3f;

    /// <summary>吟唱期间播放的持续特效 Key</summary>
    public StringBinding channelVfxKey = new()
    {
        Source = StringBinding.SourceType.Literal,
        LiteralValue = string.Empty
    };

    /// <summary>吟唱完成后是否自动触发完成特效</summary>
    public StringBinding channelFinishVfxKey = new()
    {
        Source = StringBinding.SourceType.Literal,
        LiteralValue = string.Empty
    };

    /// <summary>每次 tick 的伤害倍率（基于 SkillConfig.Damage）</summary>
    public FloatBinding tickDamageRate = new()
    {
        Source = FloatBinding.SourceType.Literal,
        LiteralValue = 0.3f
    };

    /// <summary>是否在被打断时仍然执行后继节点</summary>
    public bool continueOnInterrupt;

    public override IEnumerator Execute(SkillContext ctx)
    {
        var duration = channelDuration.Resolve(ctx);
        if (duration <= 0f)
        {
            // 非吟唱技能 —— 直接跳过
            yield break;
        }

        ctx.Blackboard.SetValue(BBKey.IsChanneling, true);
        ctx.Blackboard.SetValue(BBKey.ChannelDuration, duration);

        // 播放持续特效
        var vfxKey = channelVfxKey.Resolve(ctx);
        var manager = VFXManager.EnsureInstance();
        var anchor = ctx.Target ?? ctx.Caster;

        if (manager != null && !string.IsNullOrWhiteSpace(vfxKey) && anchor != null)
        {
            manager.Play(new VFXRequest
            {
                VFXKey = vfxKey,
                StyleKey = ctx.Config?.VFXProfileKey,
                Position = anchor.position,
                Parent = anchor,
                Duration = duration
            });
        }

        var elapsed = 0f;
        var nextTickTime = 0f;
        var tickIndex = 0;

        while (elapsed < duration)
        {
            if (ctx.IsInterrupted)
            {
                ctx.Blackboard.SetValue(BBKey.IsChanneling, false);
                ctx.Blackboard.SetValue(BBKey.ChannelProgress, elapsed / Mathf.Max(duration, 0.01f));
                ctx.Blackboard.SetValue(BBKey.ChannelTotalTicks, tickIndex);
                ExecuteInterruptFallback(ctx, tickIndex);
                yield break;
            }

            // tick 触发
            if (elapsed >= nextTickTime)
            {
                nextTickTime += tickInterval;
                tickIndex++;
                ctx.Blackboard.SetValue(BBKey.ChannelTick(tickIndex), true);

                // tick 伤害（直接造成伤害 + 写入 Blackboard 供后续节点引用）
                var tickDamage = (ctx.Config?.Damage ?? 0f) * tickDamageRate.Resolve(ctx);
                ctx.Blackboard.SetValue(BBKey.DamageOverride, tickDamage);
                ctx.Blackboard.SetValue(BBKey.ChannelCurrentTick, tickIndex);

                ApplyTickDamage(ctx, tickDamage);
            }

            elapsed += Time.deltaTime;
            ctx.Blackboard.SetValue(BBKey.ChannelProgress, Mathf.Clamp01(elapsed / Mathf.Max(duration, 0.01f)));
            yield return null;
        }

        // 完成特效
        var finishKey = channelFinishVfxKey.Resolve(ctx);
        if (manager != null && !string.IsNullOrWhiteSpace(finishKey) && anchor != null)
        {
            manager.Play(new VFXRequest
            {
                VFXKey = finishKey,
                StyleKey = ctx.Config?.VFXProfileKey,
                Position = anchor.position,
                Parent = anchor,
                Duration = 0.5f
            });
        }

        ctx.Blackboard.SetValue(BBKey.IsChanneling, false);
        ctx.Blackboard.SetValue(BBKey.ChannelProgress, 1f);
        ctx.Blackboard.SetValue(BBKey.ChannelTotalTicks, tickIndex);
    }

    public override SkillNode ResolveNextNode(SkillContext ctx)
    {
        if (ctx.IsInterrupted && !continueOnInterrupt)
            return null;

        return base.ResolveNextNode(ctx);
    }

    private void ExecuteInterruptFallback(SkillContext ctx, int tickIndex)
    {
        Debug.Log($"[ChannelNode] Channel interrupted at tick #{tickIndex}");
    }

    private static void ApplyTickDamage(SkillContext ctx, float tickDamage)
    {
        if (ctx.Target == null || tickDamage <= 0f) return;

        var damageable = ctx.Target.GetComponent<IDamageable>();
        if (damageable == null)
            damageable = ctx.Target.GetComponentInParent<IDamageable>();

        if (damageable != null)
        {
            damageable.TakeDamage(tickDamage, ctx.Caster);
        }
    }
}
