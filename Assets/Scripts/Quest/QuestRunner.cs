using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     运行时任务实例。
/// </summary>
public class QuestInstance
{
    public QuestGraphAsset Graph;
    public QuestState State;
    public QuestNodeBase CurrentNode;
    public Transform Owner;
    public float LastTickTime;
}

/// <summary>
///     任务执行引擎。独立于技能 Tick 的低频任务状态机。
/// </summary>
[DefaultExecutionOrder(-90)]
public class QuestRunner : MonoBehaviour
{
    [Header("=== Tick 配置 ===")]
    [Tooltip("任务 Tick 间隔（秒），0=每帧 Tick")]
    [SerializeField] private float _tickInterval = 0.5f;

    [Header("=== 调试 ===")]
    [SerializeField] private bool _showDebugInfo;

    private readonly List<QuestInstance> _activeQuests = new(16);
    private static readonly HashSet<int> CompletedQuestIds = new();
    private static readonly HashSet<int> FailedQuestIds = new();

    [NonSerialized] private static Transform _currentOwner;
    public static Transform CurrentOwner => _currentOwner;

    public int ActiveQuestCount => _activeQuests.Count;
    public static IReadOnlyCollection<int> CompletedQuests => CompletedQuestIds;
    public static QuestRunner Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update() { TickAll(); }

    public QuestInstance AcceptQuest(QuestGraphAsset graph, Transform owner)
    {
        if (graph == null) return null;
        if (CompletedQuestIds.Contains(graph.QuestId) && !graph.IsRepeatable) return null;

        foreach (var q in _activeQuests)
            if (q.Graph == graph) return null;

        graph.StartGraph();

        var instance = new QuestInstance
        {
            Graph = graph,
            State = QuestState.InProgress,
            CurrentNode = graph.QuestStartNode,
            Owner = owner,
            LastTickTime = Time.time
        };

        if (instance.CurrentNode != null)
        {
            instance.CurrentNode.HasEntered = true;
            instance.CurrentNode.OnEnter();
        }

        _activeQuests.Add(instance);

        GlobalEventBus.Publish(new QuestStateChangedEvent
        {
            QuestId = graph.QuestId,
            QuestName = graph.QuestName,
            OldState = QuestState.NotStarted,
            NewState = QuestState.InProgress
        });

        Debug.Log($"[QuestRunner] Quest accepted: {graph.QuestName}");
        return instance;
    }

    public void AbandonQuest(QuestInstance instance)
    {
        if (instance == null) return;
        instance.Graph.StopGraph();
        _activeQuests.Remove(instance);

        GlobalEventBus.Publish(new QuestStateChangedEvent
        {
            QuestId = instance.Graph.QuestId,
            QuestName = instance.Graph.QuestName,
            OldState = instance.State,
            NewState = QuestState.Failed
        });
    }

    public static bool IsQuestCompleted(int questId) => CompletedQuestIds.Contains(questId);
    public static bool IsQuestFailed(int questId) => FailedQuestIds.Contains(questId);

    /// <summary>
    ///     标记任务为已完成（仅供存档恢复使用）。
    ///     不触发 GlobalEventBus 事件，避免加载存档时产生副作用。
    /// </summary>
    public static void MarkQuestCompleted(int questId)
    {
        CompletedQuestIds.Add(questId);
    }

    private void TickAll()
    {
        if (_activeQuests.Count == 0) return;

        for (var i = _activeQuests.Count - 1; i >= 0; i--)
        {
            var quest = _activeQuests[i];
            if (quest.State != QuestState.InProgress) continue;
            if (_tickInterval > 0f && Time.time - quest.LastTickTime < _tickInterval) continue;
            quest.LastTickTime = Time.time;

            _currentOwner = quest.Owner;
            TickQuest(quest);
            _currentOwner = null;
        }
    }

    private void TickQuest(QuestInstance quest)
    {
        if (quest.CurrentNode == null) { CompleteQuest(quest); return; }

        var result = quest.CurrentNode.Tick(Time.deltaTime);

        switch (result)
        {
            case QuestNodeResult.Running: break;
            case QuestNodeResult.Success: AdvanceToNextNode(quest); break;
            case QuestNodeResult.Failure: FailQuest(quest); break;
            case QuestNodeResult.Suspended: break;
        }
    }

    private void AdvanceToNextNode(QuestInstance quest)
    {
        quest.CurrentNode?.OnExit();
        var nextNode = quest.CurrentNode?.ResolveNextNode();
        quest.CurrentNode = nextNode;

        if (nextNode == null) { CompleteQuest(quest); return; }

        nextNode.HasEntered = true;
        nextNode.OnEnter();

        GlobalEventBus.Publish(new QuestProgressEvent
        {
            QuestId = quest.Graph.QuestId,
            Description = $"Executing: {nextNode.NodeName}",
            Current = 0,
            Target = 0
        });
    }

    private void CompleteQuest(QuestInstance quest)
    {
        quest.State = QuestState.Completed;
        quest.Graph.StopGraph();
        CompletedQuestIds.Add(quest.Graph.QuestId);
        _activeQuests.Remove(quest);

        GlobalEventBus.Publish(new QuestStateChangedEvent
        {
            QuestId = quest.Graph.QuestId,
            QuestName = quest.Graph.QuestName,
            OldState = QuestState.InProgress,
            NewState = QuestState.Completed
        });

        Debug.Log($"[QuestRunner] Quest completed: {quest.Graph.QuestName}");
    }

    private void FailQuest(QuestInstance quest)
    {
        quest.State = QuestState.Failed;
        quest.Graph.StopGraph();
        FailedQuestIds.Add(quest.Graph.QuestId);
        _activeQuests.Remove(quest);

        GlobalEventBus.Publish(new QuestStateChangedEvent
        {
            QuestId = quest.Graph.QuestId,
            QuestName = quest.Graph.QuestName,
            OldState = QuestState.InProgress,
            NewState = QuestState.Failed
        });
    }

    public void ResetAll()
    {
        foreach (var quest in _activeQuests) quest.Graph.StopGraph();
        _activeQuests.Clear();
        CompletedQuestIds.Clear();
        FailedQuestIds.Clear();
    }

    private void OnDestroy()
    {
        ResetAll();
        if (Instance == this) Instance = null;
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (!_showDebugInfo) return;
        GUILayout.BeginArea(new Rect(370, 10, 350, 500));
        GUILayout.Label("<b>Quest Runner</b>", new GUIStyle(GUI.skin.label) { richText = true });
        GUILayout.Label($"Active: {_activeQuests.Count}  Completed: {CompletedQuestIds.Count}");
        foreach (var quest in _activeQuests)
        {
            var node = quest.CurrentNode != null ? quest.CurrentNode.NodeName : "End";
            GUILayout.Label($"  {quest.Graph.QuestName} @ {node} [{quest.State}]");
        }
        GUILayout.EndArea();
    }
#endif
}