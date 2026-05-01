using System;
using UnityEngine;

/// <summary>
///     对话触发节点。抛出 UI 事件打开对话框，挂起当前节点执行，
///     等待 UI 回传"对话结束"指令后再继续 Tick。
/// </summary>
public class DialogueNode : QuestNodeBase
{
    [Header("=== 对话配置 ===")]
    [SerializeField] private int _dialogueId;
    [SerializeField] private string _speakerName = "NPC";
    [SerializeField, Multiline(3)] private string _content = "...";

    public override void OnEnter()
    {
        IsSuspended = true;

        GlobalEventBus.Publish(new DialogueTriggeredEvent
        {
            DialogueId = _dialogueId,
            Content = _content,
            SpeakerName = _speakerName,
            OnDialogueComplete = OnDialogueComplete
        });

        Debug.Log($"[DialogueNode] Triggered: {_speakerName}: {_content}");
    }

    public override QuestNodeResult Tick(float deltaTime)
    {
        return IsSuspended ? QuestNodeResult.Suspended : QuestNodeResult.Success;
    }

    private void OnDialogueComplete()
    {
        IsSuspended = false;
        Debug.Log($"[DialogueNode] Dialogue {_dialogueId} completed.");
    }

    public override void OnReset()
    {
        base.OnReset();
        IsSuspended = false;
    }
}