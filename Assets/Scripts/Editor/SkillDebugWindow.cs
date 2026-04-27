using UnityEditor;
using UnityEngine;

public class SkillDebugWindow : EditorWindow
{
    private Vector2 _scroll;
    private int _selectedRecord;

    private void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to debug skill execution.", MessageType.Info);
            return;
        }

        var runner = SkillRunner.Instance;
        if (runner == null)
        {
            EditorGUILayout.HelpBox("SkillRunner instance not found.", MessageType.Warning);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Pause")) runner.Pause();
        if (GUILayout.Button("Step")) runner.Step();
        if (GUILayout.Button("Continue")) runner.Continue();
        runner.IsDebugMode = GUILayout.Toggle(runner.IsDebugMode, "Debug Mode");
        EditorGUILayout.EndHorizontal();

        var frame = runner.CurrentFrame;
        var ctx = runner.CurrentContext;
        if (frame == null || ctx == null)
        {
            EditorGUILayout.HelpBox("No active skill execution.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Graph", frame.Graph != null ? frame.Graph.name : "<None>");
        EditorGUILayout.LabelField("Node", frame.CurrentNode != null ? frame.CurrentNode.name : "<None>");
        EditorGUILayout.LabelField("Skill", ctx.Config != null ? ctx.Config.SkillName : "<Unknown>");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Blackboard", EditorStyles.boldLabel);
        foreach (var pair in ctx.Blackboard.GetAllData())
            EditorGUILayout.LabelField(pair.Key, pair.Value != null ? pair.Value.ToString() : "<null>");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Execution Log", EditorStyles.boldLabel);

        var records = ctx.Recorder.Entries;
        if (records.Count == 0)
        {
            EditorGUILayout.HelpBox("No execution records yet.", MessageType.None);
            return;
        }

        _selectedRecord = Mathf.Clamp(EditorGUILayout.IntSlider("Record", _selectedRecord, 0, records.Count - 1), 0,
            records.Count - 1);
        var record = records[_selectedRecord];
        EditorGUILayout.LabelField("Step", record.StepIndex.ToString());
        EditorGUILayout.LabelField("Event", record.EventType);
        EditorGUILayout.LabelField("Node", record.NodeName);

        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(180f));
        foreach (var snapshot in record.BlackboardSnapshot) EditorGUILayout.LabelField(snapshot.Key, snapshot.Value);

        EditorGUILayout.EndScrollView();
    }

    private void OnInspectorUpdate()
    {
        Repaint();
    }

    [MenuItem("Tools/Skills/Debug Window")]
    private static void Open()
    {
        GetWindow<SkillDebugWindow>("Skill Debug");
    }
}