using UnityEngine;

/// <summary>
///     AnimationEventWaitNode —— 替代 DelayNode，等待动画事件推进。
///     不依赖时间，完全由动画帧驱动：
///     - 播放攻击动画 → OnHit 事件触发 → 本节点通过 → 执行后续 DamageNode
///     - 攻速变化时自动适配（动画速度变化，事件时机自动对齐）。
/// </summary>
[CreateAssetMenu(fileName = "AnimEventWaitNode", menuName = "Skill System/Nodes/Animation/AnimEventWait")]
public class AnimationEventWaitNode : SkillNodeBase
{
    /// <summary>等待的动画事件名（如 "OnHit", "OnCastEnd"）</summary>
    public string waitEvent = "OnHit";

    /// <summary>超时时间（秒），0=永不超时</summary>
    public float timeout = 2f;

    /// <summary>匹配模式：Exact=精确匹配 / Contains=包含子串</summary>
    public MatchMode matchMode = MatchMode.Contains;

    [System.NonSerialized] private float _elapsed;

    public enum MatchMode
    {
        Exact,
        Contains
    }

    public override void OnEnter(SkillContext ctx)
    {
        _elapsed = 0f;
        // 清除上一次残留事件
        ctx.Blackboard.SetValue(BBKey.AnimOnHit, false);
        ctx.Blackboard.SetValue(BBKey.AnimOnCastEnd, false);
        ctx.Blackboard.SetValue(BBKey.AnimEvent, string.Empty);
    }

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        _elapsed += deltaTime;

        // 超时检查
        if (timeout > 0f && _elapsed >= timeout)
            return NodeTickResult.Success;

        // 检查黑板中的动画事件
        var currentEvent = ctx.Blackboard.GetString(BBKey.AnimEvent, string.Empty);
        if (string.IsNullOrEmpty(currentEvent))
            return NodeTickResult.Running;

        // 匹配事件
        var isMatch = matchMode == MatchMode.Exact
            ? currentEvent == waitEvent
            : currentEvent.Contains(waitEvent);

        if (isMatch)
        {
            // 消费事件（防止后续节点重复触发）
            ctx.Blackboard.SetValue(BBKey.AnimEvent, string.Empty);
            ctx.Blackboard.SetValue(BBKey.AnimOnHit, false);
            ctx.Blackboard.SetValue(BBKey.AnimOnCastEnd, false);
            return NodeTickResult.Success;
        }

        return NodeTickResult.Running;
    }
}
