using System;
using UnityEngine;

// ============================================================
//  SkillAnimatorController —— 动画驱动技能同步
//  替代 DelayNode，动画事件驱动技能逻辑推进。
// ============================================================

/// <summary>
///     SkillAnimatorController —— 动画与技能图之间的桥接层。
///     
///     工作流：
///     1. 技能节点调用 PlaySkillClip → 播放动画
///     2. 动画中的 AnimationEvent 触发 OnAnimationEvent
///     3. 黑板写入事件标志，技能节点通过轮询/回调获知
///     
///     攻速变化：动画速度 = 基础动画速度 * 攻速倍率，逻辑自动对齐。
/// </summary>
[RequireComponent(typeof(Animator))]
public class SkillAnimatorController : MonoBehaviour
{
    [Header("Animation Speed")]
    [Tooltip("基础动画速度倍率（默认 1.0）")]
    [SerializeField] private float _baseSpeed = 1f;

    [Tooltip("从 AttributeSet 读取攻速倍率（AttackSpeed）")]
    [SerializeField] private bool _useAttributeAttackSpeed = true;

    [Header("Event Keys")]
    [Tooltip("匹配的动画事件字符串前缀")]
    [SerializeField] private string _eventPrefix = "Skill:";

    // ---- internal state ----
    private Animator _animator;
    private AttributeSet _attributeSet;
    private SkillContext _currentSkillContext;

    /// <summary>当前播放的动画名称</summary>
    public string CurrentAnimation { get; private set; }

    /// <summary>当前动画的标准化时间 (0~1)</summary>
    public float NormalizedTime
    {
        get
        {
            if (_animator == null) return 0f;
            var state = _animator.GetCurrentAnimatorStateInfo(0);
            return state.normalizedTime;
        }
    }

    /// <summary>动画事件回调（非 AnimationEvent 路径，供代码侧订阅）</summary>
    public event Action<string> OnAnimationEventTriggered;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _attributeSet = GetComponent<AttributeSet>();
    }

    private void Update()
    {
        // 持续同步攻速
        if (_useAttributeAttackSpeed && _animator != null)
        {
            var atkSpeed = _attributeSet != null
                ? _attributeSet.FinalAttackSpeed
                : 1f;
            _animator.speed = _baseSpeed * atkSpeed;
        }
    }

    /// <summary>
    ///     播放技能动画并建立上下文绑定。
    ///     技能节点调用此方法，传入当前 SkillContext。
    /// </summary>
    /// <param name="animationName">Animator 中的 State 名称或 Trigger</param>
    /// <param name="ctx">当前技能上下文（用于黑板通信）</param>
    /// <param name="crossFadeTime">过渡时间</param>
    /// <param name="layer">动画层</param>
    public void PlaySkillClip(string animationName, SkillContext ctx,
        float crossFadeTime = 0.1f, int layer = 0)
    {
        if (_animator == null) return;

        _currentSkillContext = ctx;
        CurrentAnimation = animationName;

        // 清除上一次事件残留
        if (ctx != null)
        {
            ctx.Blackboard.SetValue(BBKey.AnimEvent, string.Empty);
            ctx.Blackboard.SetValue(BBKey.AnimNormalizedTime, 0f);
            ctx.Blackboard.SetValue(BBKey.AnimIsPlaying, true);
        }

        // 优先尝试 Trigger，失败则 Play
        try
        {
            _animator.SetTrigger(animationName);
        }
        catch
        {
            // 回退：直接播放 AnimationClip 名称（兼容旧版）
            try
            {
                _animator.Play(animationName, layer, 0f);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SkillAnimator] Failed to play '{animationName}': {e.Message}");
            }
        }
    }

    /// <summary>
    ///     停止当前技能动画并清理上下文。
    /// </summary>
    public void StopSkillClip(SkillContext ctx)
    {
        if (ctx != null)
        {
            ctx.Blackboard.SetValue(BBKey.AnimIsPlaying, false);
            ctx.Blackboard.SetValue(BBKey.AnimEvent, "Stopped");
        }

        CurrentAnimation = null;
        _currentSkillContext = null;
    }

    /// <summary>
    ///     Unity AnimationEvent 回调 —— 由动画片段中的 Event 触发。
    ///     动画中配置的 StringParameter 格式："Skill:OnHit" / "Skill:OnCastStart" / "Skill:OnCastEnd"
    /// </summary>
    public void OnAnimationEvent(string eventKey)
    {
        // 过滤非技能事件
        if (!string.IsNullOrEmpty(_eventPrefix) && !eventKey.StartsWith(_eventPrefix))
            return;

        var cleanKey = eventKey;
        if (!string.IsNullOrEmpty(_eventPrefix))
            cleanKey = eventKey.Substring(_eventPrefix.Length);

        // 写入当前技能上下文黑板（供技能节点轮询）
        if (_currentSkillContext != null)
        {
            _currentSkillContext.Blackboard.SetValue(BBKey.AnimEvent, cleanKey);
            _currentSkillContext.Blackboard.SetValue(BBKey.AnimLastEventTime, Time.time);
        }

        // 通知代码侧订阅者
        OnAnimationEventTriggered?.Invoke(cleanKey);

        // 特殊事件处理
        switch (cleanKey)
        {
            case "OnHit":
                HandleHitEvent();
                break;
            case "OnCastEnd":
                HandleCastEnd();
                break;
        }
    }

    /// <summary>
    ///     AnimationEvent 的 integer 重载（兼容更简洁的配置方式）。
    /// </summary>
    public void OnAnimationEventInt(int eventId)
    {
        var eventKey = eventId switch
        {
            0 => "OnCastStart",
            1 => "OnHit",
            2 => "OnCastEnd",
            _ => $"Custom{eventId}"
        };
        OnAnimationEvent(_eventPrefix + eventKey);
    }

    /// <summary>
    ///     手动触发动画事件（代码侧调用，用于无需实际 Animation 的场景）。
    /// </summary>
    public void TriggerEvent(string eventKey, SkillContext ctx)
    {
        _currentSkillContext = ctx ?? _currentSkillContext;
        OnAnimationEvent(_eventPrefix + eventKey);
    }

    // ---- internal ----

    private void HandleHitEvent()
    {
        // 命中帧：黑板写入命中标记
        if (_currentSkillContext != null)
        {
            _currentSkillContext.Blackboard.SetValue(BBKey.AnimOnHit, true);
        }
    }

    private void HandleCastEnd()
    {
        if (_currentSkillContext != null)
        {
            _currentSkillContext.Blackboard.SetValue(BBKey.AnimOnCastEnd, true);
            _currentSkillContext.Blackboard.SetValue(BBKey.AnimIsPlaying, false);
        }
    }
}

/// <summary>
///     AnimationEvent 接收器 —— 挂载在角色上，由 AnimationClip 上的 Event 调用。
///     Unity AnimationEvent 只能调用目标 GameObject 上组件的方法，
///     此组件作为入口，转发事件到 SkillAnimatorController。
/// </summary>
public class AnimationEventReceiver : MonoBehaviour
{
    private SkillAnimatorController _controller;

    private void Awake()
    {
        _controller = GetComponent<SkillAnimatorController>();
    }

    /// <summary>Animation 调用此方法（StringParameter = "Skill:OnHit" 等）</summary>
    public void OnSkillAnimationEvent(string eventKey)
    {
        _controller?.OnAnimationEvent(eventKey);
    }

    /// <summary>Animation 调用此方法（IntParameter = 0/1/2 对应 Start/Hit/End）</summary>
    public void OnSkillAnimationEventInt(int eventId)
    {
        _controller?.OnAnimationEventInt(eventId);
    }
}
