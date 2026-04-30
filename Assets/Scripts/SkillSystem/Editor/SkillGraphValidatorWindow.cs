using UnityEditor;
using UnityEngine;

public class SkillGraphValidatorWindow : EditorWindow
{
    private void OnGUI()
    {
        var graph = Selection.activeObject as SkillGraphAsset;
        if (graph == null)
        {
            EditorGUILayout.HelpBox("Select a SkillGraph asset to validate it.", MessageType.Info);
            return;
        }

        if (GUILayout.Button("Run Validation")) Validate(graph);
    }

    [MenuItem("Tools/Skills/Validate Graph")]
    private static void Open()
    {
        GetWindow<SkillGraphValidatorWindow>("Skill Graph Validator");
    }

    private static void Validate(SkillGraphAsset graph)
    {
        if (!graph.HasSingleStartNode())
        {
            Debug.LogError($"[SkillGraphValidator] Graph {graph.name} must contain exactly one StartNode.");
            return;
        }

        var hasEndNode = false;
        foreach (var rawNode in graph.Nodes)
        {
            if (rawNode is EndNode) hasEndNode = true;

            if (rawNode is SubGraphNode subGraphNode && subGraphNode.subGraph == graph)
            {
                Debug.LogError($"[SkillGraphValidator] Graph {graph.name} contains a self-referencing SubGraphNode.");
                return;
            }

            if (rawNode is SkillNodeBase skillNode && !(rawNode is EndNode))
            {
                var hasPrimaryOutput = skillNode.GetOutputEdges().Count > 0;
                if (!hasPrimaryOutput && !(rawNode is ConditionNode) && !(rawNode is ParallelNode))
                    Debug.LogWarning(
                        $"[SkillGraphValidator] Node {skillNode.NodeName} in graph {graph.name} has no outgoing edge.");
            }
        }

        if (!hasEndNode)
        {
            Debug.LogError($"[SkillGraphValidator] Graph {graph.name} must contain at least one EndNode.");
            return;
        }

        Debug.Log($"[SkillGraphValidator] Graph {graph.name} passed the basic structural validation.");
    }
}