using System.Collections.Generic;
using UnityEditor;

public class BreakpointDatabase : ScriptableSingleton<BreakpointDatabase>
{
    public List<string> nodeGuids = new();

    public bool Contains(string nodeGuid)
    {
        return nodeGuids.Contains(nodeGuid);
    }

    public void Set(string nodeGuid, bool enabled)
    {
        if (enabled)
        {
            if (!nodeGuids.Contains(nodeGuid)) nodeGuids.Add(nodeGuid);
        }
        else
        {
            nodeGuids.Remove(nodeGuid);
        }

        Save(true);
    }
}