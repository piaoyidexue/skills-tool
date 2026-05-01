using System;
using UnityEngine;

// ============================================================
//  任务节点基类 (QuestNodeBase)
//  继承 LogicNodeBase，扩展任务特定的 Tick 驱动模型。
//  任务节点由 QuestRunner 低频 Tick 驱动，返回 QuestNodeResult。
//
//  设计准则：
//  - Tick 驱动，0 GC（同技能系统的 NodeTickResult 模式）
//  - 任务节点可挂起（Suspended），等待外部事件恢复
//  - 事件等待节点通过 GlobalEventBus 监听事件
// ============================================================

/// <summary>
///     任务节点 Tick 返回值。
/// </summary>
public enum QuestNodeResult
{
    /// <summary>本帧未完成，下一帧继续 Tick</summary>
    Running,

    /// <summary>执行成功，推进到下一节点</summary>
    Success,

    /// <summary>执行失败</summary>
    Failure,

    /// <summary>挂起等待（等待外部事件/对话完成等）</summary>
    Suspended
}

/// <summary>
///     任务节点基类。
///     继承 LogicNodeBase，扩展 Tick 驱动和挂起机制。
/// </summary>
public abstract class QuestNodeBase : LogicNodeBase
{
    // ──────────── 任务节点状态 ────────────

    /// <summary>节点是否已进入</summary>
    [NonSerialized] public bool HasEntered;

    /// <summary>节点是否被挂起（等待外部恢复）</summary>
    [NonSerialized] public bool IsSuspended;

    // ──────────── 生命周期 ────────────

    /// <summary>
    ///     节点首次进入时调用（在 Tick 之前）。
    /// </summary>
    public virtual void OnEnter() { }

    /// <summary>
    ///     每帧 Tick 驱动（由 QuestRunner 调用）。
    ///     返回节点当前状态。
    /// </summary>
    public abstract QuestNodeResult Tick(float deltaTime);

    /// <summary>
    ///     节点离开时调用。
    /// </summary>
    public virtual void OnExit() { }

    /// <summary>
    ///     恢复挂起的节点（由外部事件触发）。
    /// </summary>
    public virtual void Resume() { IsSuspended = false; }

    /// <summary>
    ///     获取下一个节点（沿 "output" 端口）。
    ///     子类可 override 实现自定义导航（如条件分支）。
    /// </summary>
    public virtual QuestNodeBase ResolveNextNode()
    {
        return GetConnectedNode("output") as QuestNodeBase;
    }

    // ──────────── 重置 ────────────

    public override void OnReset()
    {
        HasEntered = false;
        IsSuspended = false;
    }

    public override void OnGraphStopped()
    {
        if (HasEntered) OnExit();
        OnReset();
    }
}
