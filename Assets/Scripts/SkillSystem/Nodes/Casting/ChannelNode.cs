using UnityEngine;

/// <summary>
///     吟唱管理节点 —— 持续引导技能。
///     每 tickInterval 触发一次 tick，总时长 channelDuration。
///     期间可被打断。进度记录在 Blackboard.ChannelProgress。
/// </summary>
[CreateAssetMenu(fileName = "ChannelNode", menuName = "Skill System/Nodes/Casting/Channel")]
public class ChannelNode : SkillNodeBase
{
    public FloatBinding channelDuration = new()
    {
        Source = FloatBinding.SourceType.SkillConfig,
        SkillField = SkillFloatField.ChannelDuration
    };

    public float tickInterval = 0.3f;

    public StringBinding channelVfxKey = new()
    {
        Source = StringBinding.SourceType.Literal,
        LiteralValue = string.Empty
    };

    public StringBinding channelFinishVfxKey = new()
    {
        Source = StringBinding.SourceType.Literal,
        LiteralValue = string.Empty
    };

    public FloatBinding tickDamageRate = new()
    {
        Source = FloatBinding.SourceType.Literal,
        LiteralValue = 0.3f
    };

    public bool continueOnInterrupt;

    // ---- Tick 状态 ----
    [System.NonSerialized] private float _elapsed;
    [System.NonSerialized] private float _nextTickTime;
    [System.NonSerialized] private int _tickIndex;
    [System.NonSerialized] private float _totalDuration;
    [System.NonSerialized] private bool _vfxPlayed;

    public override void OnEnter(SkillContext ctx)
    {
        _totalDuration = channelDuration.Resolve(ctx);
        if (_totalDuration <= 0f) return;

        _elapsed = 0f;
        _nextTickTime = 0f;
        _tickIndex = 0;
        _vfxPlayed = false;

        ctx.Blackboard.SetValue(BBKey.IsChanneling, true);
        ctx.Blackboard.SetValue(BBKey.ChannelDuration, _totalDuration);
    }

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        if (_totalDuration <= 0f) return NodeTickResult.Success;

        if (ctx.IsInterrupted)
        {
            ctx.Blackboard.SetValue(BBKey.IsChanneling, false);
            ctx.Blackboard.SetValue(BBKey.ChannelProgress, _elapsed / Mathf.Max(_totalDuration, 0.01f));
            ctx.Blackboard.SetValue(BBKey.ChannelTotalTicks, _tickIndex);
            ExecuteInterruptFallback(ctx, _tickIndex);
            return continueOnInterrupt ? NodeTickResult.Success : NodeTickResult.Failure;
        }

        // 播放持续特效（仅首次）
        if (!_vfxPlayed)
        {
            _vfxPlayed = true;
            PlayChannelVFX(ctx);
        }

        // Tick 触发
        if (_elapsed >= _nextTickTime)
        {
            _nextTickTime += tickInterval;
            _tickIndex++;
            ctx.Blackboard.SetValue(BBKey.ChannelTick(_tickIndex), true);
            ctx.Blackboard.SetValue(BBKey.ChannelCurrentTick, _tickIndex);

            // GAS架构：tick 伤害通过 DamagePipeline 投递，不再写入黑板
            var tickDamage = (ctx.Config?.Damage ?? 0f) * tickDamageRate.Resolve(ctx);
            ApplyTickDamage(ctx, tickDamage);
        }

        _elapsed += deltaTime;
        ctx.Blackboard.SetValue(BBKey.ChannelProgress, Mathf.Clamp01(_elapsed / Mathf.Max(_totalDuration, 0.01f)));

        if (_elapsed >= _totalDuration)
        {
            // 完成
            PlayFinishVFX(ctx);
            ctx.Blackboard.SetValue(BBKey.IsChanneling, false);
            ctx.Blackboard.SetValue(BBKey.ChannelProgress, 1f);
            ctx.Blackboard.SetValue(BBKey.ChannelTotalTicks, _tickIndex);
            return NodeTickResult.Success;
        }

        return NodeTickResult.Running;
    }

    public override void OnExit(SkillContext ctx)
    {
        ctx.Blackboard.SetValue(BBKey.IsChanneling, false);
    }

    public override SkillNodeBase ResolveNextNode(SkillContext ctx)
    {
        if (ctx.IsInterrupted && !continueOnInterrupt)
            return null;

        return base.ResolveNextNode(ctx);
    }

    private void PlayChannelVFX(SkillContext ctx)
    {
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
                Duration = _totalDuration
            });
        }
    }

    private void PlayFinishVFX(SkillContext ctx)
    {
        var finishKey = channelFinishVfxKey.Resolve(ctx);
        var manager = VFXManager.EnsureInstance();
        var anchor = ctx.Target ?? ctx.Caster;

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
    }

    private void ExecuteInterruptFallback(SkillContext ctx, int tickIndex)
    {
        Debug.Log($"[ChannelNode] Channel interrupted at tick #{tickIndex}");
    }

    private static void ApplyTickDamage(SkillContext ctx, float tickDamage)
    {
        if (ctx.Target == null || tickDamage <= 0f) return;

        // GAS架构：通过 DamagePipeline 投递，不再直接调用 IDamageable
        DamagePipeline.CalculateAndApply(tickDamage, ctx.Target, ctx.Caster, null);
    }
}
