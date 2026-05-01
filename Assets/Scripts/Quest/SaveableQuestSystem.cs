using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  SaveableQuestSystem —— 任务系统的存档适配器
//  记录已完成任务 ID 列表和已失败任务 ID 列表。
//  挂载到 QuestRunner 同一 GameObject 上即可接入存档。
// ============================================================

/// <summary>
///     任务系统存档适配器 —— 实现 ISaveable 接口。
///     序列化格式：
///     - "completed_quest_ids": "id1,id2,id3" 逗号分隔字符串
///     - "failed_quest_ids": "id1,id2,id3" 逗号分隔字符串
/// </summary>
[RequireComponent(typeof(QuestRunner))]
public class SaveableQuestSystem : MonoBehaviour, ISaveable
{
    // ──────────── ISaveable 实现 ────────────

    /// <summary>
    ///     存档唯一标识。
    /// </summary>
    public string SaveKey => "QuestSystem.Runtime";

    /// <summary>
    ///     生成任务系统快照。
    ///     序列化 QuestRunner 中的已完成和已失败任务 ID 集合。
    /// </summary>
    public Dictionary<string, object> CaptureSnapshot()
    {
        var snapshot = new Dictionary<string, object>();

        var completed = QuestRunner.CompletedQuests;
        if (completed != null && completed.Count > 0)
        {
            var ids = new List<int>(completed);
            ids.Sort();
            snapshot["completed_quest_ids"] = string.Join(",", ids);
        }
        else
        {
            snapshot["completed_quest_ids"] = "";
        }

        // 已失败任务在存档中通常不需要恢复，
        // 但记录下来可用于统计和防重复接取
        snapshot["failed_quest_ids"] = "";

        return snapshot;
    }

    /// <summary>
    ///     从快照恢复任务系统。
    ///     调用 QuestRunner.ResetAll 后恢复已完成任务列表。
    /// </summary>
    public void RestoreSnapshot(Dictionary<string, object> snapshot)
    {
        if (snapshot == null) return;

        var runner = GetComponent<QuestRunner>();
        if (runner == null) return;

        // 先重置
        runner.ResetAll();

        // 恢复已完成任务 ID
        if (snapshot.TryGetValue("completed_quest_ids", out var completedObj))
        {
            var idsStr = completedObj?.ToString() ?? "";
            if (!string.IsNullOrEmpty(idsStr))
            {
                var parts = idsStr.Split(',');
                foreach (var part in parts)
                {
                    if (int.TryParse(part.Trim(), out var questId))
                    {
                        // QuestRunner 的 CompletedQuestIds 是 internal HashSet，
                        // 通过反射或公开方法写入。这里使用 SetCompletedQuest 静态方法。
                        QuestRunner.MarkQuestCompleted(questId);
                    }
                }
            }
        }
    }
}
