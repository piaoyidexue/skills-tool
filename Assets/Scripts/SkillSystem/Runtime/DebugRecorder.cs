using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DebugRecordEntry
{
    public int StepIndex;
    public string GraphName;
    public string NodeName;
    public string EventType;
    public float TimeStamp;
    public Dictionary<string, string> BlackboardSnapshot;
}

public class DebugRecorder
{
    private readonly List<DebugRecordEntry> _entries = new();

    public IReadOnlyList<DebugRecordEntry> Entries => _entries;

    public void Clear()
    {
        _entries.Clear();
    }

    public void Record(string graphName, string nodeName, string eventType, Blackboard blackboard)
    {
        _entries.Add(new DebugRecordEntry
        {
            StepIndex = _entries.Count,
            GraphName = graphName,
            NodeName = nodeName,
            EventType = eventType,
            TimeStamp = Time.realtimeSinceStartup,
            BlackboardSnapshot = blackboard != null ? blackboard.GetSnapshotStrings() : new Dictionary<string, string>()
        });
    }
}