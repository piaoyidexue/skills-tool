using System;
using UnityEngine;

// ============================================================
//  事件等待节点 (EventWaitNode)
//  监听指定的全局事件，当事件触发时节点状态推进。
//  依赖 GlobalEventBus（方向一），实现"事件驱动"的任务推进。
//
//  设计准则：
//  - OnEnter 时订阅事件，OnExit/OnGraphStopped 时注销
//  - 事件触发后设置 _eventReceived = true，下一帧 Tick 返回 Success
//  - 泛型模式：通过配置指定要监听的事件类型（字符串匹配）
// ============================================================

/// <summary>
///     事件等待节点。
///     监听指定的全局事件，当事件触发时节点推进。
///     在 OnEnter 订阅，OnExit 注销，防止内存泄漏。
/// </summary>
public class EventWaitNode : QuestNodeBase
{
    // ──────────── 配置 ────────────

    /// <summary>要监听的事件类型名称（如 "EntityDeathEvent", "ReactionTriggeredEvent"）</summary>
    [SerializeField] private string _eventTypeName = "EntityDeathEvent";

    /// <summary>额外的过滤条件（如指定实体名称、指定反应类型等），空=不过滤</summary>
    [SerializeField] private string _filterValue = string.Empty;

    // ──────────── 运行时状态 ────────────

    [NonSerialized] private bool _eventReceived;

    // ──────────── 生命周期 ────────────

    public override void OnEnter()
    {
        _eventReceived = false;
        SubscribeToEvent();
    }

    public override QuestNodeResult Tick(float deltaTime)
    {
        if (IsSuspended) return QuestNodeResult.Suspended;
        return _eventReceived ? QuestNodeResult.Success : QuestNodeResult.Running;
    }

    public override void OnExit()
    {
        UnsubscribeFromEvent();
        _eventReceived = false;
    }

    // ──────────── 事件订阅 ────────────

    private void SubscribeToEvent()
    {
        switch (_eventTypeName)
        {
            case "EntityDeathEvent":
                GlobalEventBus.Subscribe<EntityDeathEvent>(OnEntityDeath);
                break;
            case "ReactionTriggeredEvent":
                GlobalEventBus.Subscribe<ReactionTriggeredEvent>(OnReactionTriggered);
                break;
            case "ItemAcquiredEvent":
                GlobalEventBus.Subscribe<ItemAcquiredEvent>(OnItemAcquired);
                break;
            case "SkillCastCompleteEvent":
                GlobalEventBus.Subscribe<SkillCastCompleteEvent>(OnSkillCastComplete);
                break;
            case "DialogueTriggeredEvent":
                GlobalEventBus.Subscribe<DialogueTriggeredEvent>(OnDialogueTriggered);
                break;
        }
    }

    private void UnsubscribeFromEvent()
    {
        switch (_eventTypeName)
        {
            case "EntityDeathEvent":
                GlobalEventBus.Unsubscribe<EntityDeathEvent>(OnEntityDeath);
                break;
            case "ReactionTriggeredEvent":
                GlobalEventBus.Unsubscribe<ReactionTriggeredEvent>(OnReactionTriggered);
                break;
            case "ItemAcquiredEvent":
                GlobalEventBus.Unsubscribe<ItemAcquiredEvent>(OnItemAcquired);
                break;
            case "SkillCastCompleteEvent":
                GlobalEventBus.Unsubscribe<SkillCastCompleteEvent>(OnSkillCastComplete);
                break;
            case "DialogueTriggeredEvent":
                GlobalEventBus.Unsubscribe<DialogueTriggeredEvent>(OnDialogueTriggered);
                break;
        }
    }

    // ──────────── 事件处理器 ────────────

    private void OnEntityDeath(EntityDeathEvent evt)
    {
        if (!string.IsNullOrEmpty(_filterValue))
        {
            if (evt.Entity != null && !evt.Entity.name.Contains(_filterValue) &&
                (evt.Killer == null || !evt.Killer.name.Contains(_filterValue)))
                return;
        }
        _eventReceived = true;
    }

    private void OnReactionTriggered(ReactionTriggeredEvent evt)
    {
        if (!string.IsNullOrEmpty(_filterValue) &&
            !string.Equals(evt.ReactionName, _filterValue, StringComparison.OrdinalIgnoreCase))
            return;
        _eventReceived = true;
    }

    private void OnItemAcquired(ItemAcquiredEvent evt)
    {
        if (!string.IsNullOrEmpty(_filterValue))
        {
            if (!int.TryParse(_filterValue, out var targetId) || evt.ItemId != targetId)
                return;
        }
        _eventReceived = true;
    }

    private void OnSkillCastComplete(SkillCastCompleteEvent evt)
    {
        if (!string.IsNullOrEmpty(_filterValue))
        {
            if (!int.TryParse(_filterValue, out var targetId) || evt.SkillId != targetId)
                return;
        }
        _eventReceived = true;
    }

    private void OnDialogueTriggered(DialogueTriggeredEvent evt)
    {
        if (!string.IsNullOrEmpty(_filterValue))
        {
            if (!int.TryParse(_filterValue, out var targetId) || evt.DialogueId != targetId)
                return;
        }
        _eventReceived = true;
    }

    // ──────────── 重置 ────────────

    public override void OnReset()
    {
        base.OnReset();
        _eventReceived = false;
    }
}
