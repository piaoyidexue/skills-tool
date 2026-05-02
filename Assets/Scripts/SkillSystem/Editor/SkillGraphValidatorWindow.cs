using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class SkillGraphValidatorWindow : EditorWindow
{
    private struct ValidationError
    {
        public string Message;
        public string NodeName;
        public ValidationError(string message, string nodeName = null)
        {
            Message = message;
            NodeName = nodeName;
        }
    }

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
        var errors = new List<ValidationError>();
        var warnings = new List<ValidationError>();

        if (!graph.HasSingleStartNode())
        {
            errors.Add(new ValidationError($"Graph {graph.name} must contain exactly one StartNode."));
        }

        var startNode = graph.GetStartNode();
        var hasEndNode = false;
        var nodeOutputWarnings = new HashSet<string>();

        foreach (var rawNode in graph.Nodes)
        {
            if (rawNode is EndNode)
            {
                hasEndNode = true;
                continue;
            }

            if (rawNode is SubGraphNode subGraphNode && subGraphNode.subGraph == graph)
            {
                errors.Add(new ValidationError(
                    $"Graph {graph.name} contains a self-referencing SubGraphNode.",
                    subGraphNode.NodeName));
            }

            if (rawNode is SkillNodeBase skillNode)
            {
                var outEdges = skillNode.GetOutputEdges();
                var isExemptNode = rawNode is ConditionNode || rawNode is ParallelNode || rawNode is DelayNode || rawNode is ChannelNode;

                if (outEdges.Count == 0 && !isExemptNode && !nodeOutputWarnings.Contains(skillNode.NodeName))
                {
                    warnings.Add(new ValidationError(
                        $"Node {skillNode.NodeName} in graph {graph.name} has no outgoing edge.",
                        skillNode.NodeName));
                    nodeOutputWarnings.Add(skillNode.NodeName);
                }
            }
        }

        if (!hasEndNode)
        {
            errors.Add(new ValidationError($"Graph {graph.name} must contain at least one EndNode."));
        }

        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                if (!string.IsNullOrEmpty(error.NodeName))
                    Debug.LogError($"[SkillGraphValidator] {error.Message} (Node: {error.NodeName})");
                else
                    Debug.LogError($"[SkillGraphValidator] {error.Message}");
            }
            return;
        }

        if (warnings.Count > 0)
        {
            foreach (var warning in warnings)
            {
                if (!string.IsNullOrEmpty(warning.NodeName))
                    Debug.LogWarning($"[SkillGraphValidator] {warning.Message} (Node: {warning.NodeName})");
                else
                    Debug.LogWarning($"[SkillGraphValidator] {warning.Message}");
            }
        }

        Debug.Log($"[SkillGraphValidator] Graph {graph.name} passed validation. Found {warnings.Count} warning(s).");
    }
}