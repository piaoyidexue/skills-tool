using UnityEngine;

/// <summary>
///     技能时间轴 —— 通过 Animation Event 驱动技能图节点推进。
///     替代纯 DelayNode，实现"表现即逻辑"的一致性。
///     在 Animation Clip 的特定帧上添加 Event 回调，
///     触发时通知当前执行中的节点。
/// </summary>
public class SkillTimeline : MonoBehaviour
{
    /// <summary>当前等待触发的事件名</summary>
    [System.NonSerialized] private string _pendingEvent;

    /// <summary>事件是否已触发</summary>
    [System.NonSerialized] private bool _eventTriggered;

    /// <summary>关联的技能执行实例</summary>
    [System.NonSerialized] private SkillExecution _execution;

    /// <summary>
    ///     初始化时间轴，绑定技能执行实例。
    /// </summary>
    public void Bind(SkillExecution execution)
    {
        _execution = execution;
        _eventTriggered = false;
        _pendingEvent = null;
    }

    /// <summary>
    ///     等待指定 Animation Event 触发。
    /// </summary>
    public bool WaitForEvent(string eventName)
    {
        _pendingEvent = eventName;
        _eventTriggered = false;
        return false; // 不等待
    }

    /// <summary>
    ///     检查指定事件是否已触发（由节点 Tick 中调用）。
    /// </summary>
    public bool HasEventFired(string eventName)
    {
        return _eventTriggered && _pendingEvent == eventName;
    }

    /// <summary>
    ///     重置事件状态。
    /// </summary>
    public void ResetEvent()
    {
        _eventTriggered = false;
        _pendingEvent = null;
    }

    // ============================================================
    //  Animation Event 回调（在 Animation Clip 中配置）
    // ============================================================

    /// <summary>
    ///     施法关键帧：发射投射物 / 造成伤害的时机。
    ///     在 Animation Clip 上添加此 Event。
    /// </summary>
    public void OnAnimEvent_Fire()
    {
        _eventTriggered = true;
        _pendingEvent = "Fire";
    }

    /// <summary>
    ///     命中关键帧：受击反馈的时机。
    /// </summary>
    public void OnAnimEvent_Hit()
    {
        _eventTriggered = true;
        _pendingEvent = "Hit";
    }

    /// <summary>
    ///     收招关键帧：技能结束、进入后摇的时机。
    /// </summary>
    public void OnAnimEvent_Recover()
    {
        _eventTriggered = true;
        _pendingEvent = "Recover";
    }

    /// <summary>
    ///     自定义事件：可扩展的其他动画事件。
    /// </summary>
    public void OnAnimEvent_Custom(string eventName)
    {
        _eventTriggered = true;
        _pendingEvent = eventName;
    }
}
