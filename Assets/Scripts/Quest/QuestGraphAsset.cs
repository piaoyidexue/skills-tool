using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
///     任务状态枚举。
/// </summary>
public enum QuestState
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2,
    Failed = 3
}

/// <summary>
///     任务图资产。继承 LogicGraphAsset，作为任务节点图的运行时容器。
/// </summary>
[CreateAssetMenu(fileName = "NewQuestGraph", menuName = "Quest System/Quest Graph")]
public class QuestGraphAsset : LogicGraphAsset
{
    [SerializeField] private int _questId;
    [SerializeField] private string _questName = "New Quest";
    [SerializeField, Multiline(2)] private string _questDescription = string.Empty;
    [SerializeField] private bool _isRepeatable;

    public int QuestId => _questId;
    public string QuestName => _questName;
    public string QuestDescription => _questDescription;
    public bool IsRepeatable => _isRepeatable;

    public override LogicNodeBase StartNode => Nodes.FirstOrDefault(n => n is QuestStartNode);
    public QuestNodeBase QuestStartNode => StartNode as QuestNodeBase;

    public bool HasSingleStartNode()
    {
        return Nodes.Count(n => n is QuestStartNode) == 1;
    }
}
