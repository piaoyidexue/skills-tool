#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
///     模块三：技能图死循环防呆检测器。
///     在编辑器后台无头运行所有技能图，检测哪个节点执行超时（>5 秒）。
///     输出可视化堆栈日志，并在 Project 窗口中高亮有问题的图资产。
/// </summary>
public class QADeadlockDetector : EditorWindow
{
    [NonSerialized] private bool _isRunning;
    [NonSerialized] private int _progress;
    [NonSerialized] private int _totalGraphs;
    [NonSerialized] private string _currentGraphName;
    [NonSerialized] private int _timeoutFrames = 300; // 5秒 @ 60fps
    [NonSerialized] private float _timeoutSeconds = 5f;

    private readonly List<DeadlockResult> _results = new();
    private Vector2 _scroll;
    private bool _autoPingOnError = true;

    private class DeadlockResult
    {
        public string GraphName;
        public string GraphPath;
        public string FailedNodeName;
        public float ElapsedSeconds;
        public int ExecutedTicks;
        public string StackTrace;
        public bool HasDeadlock;
    }

    [MenuItem("Tools/Skills/QA/Deadlock Detector")]
    private static void Open() => GetWindow<QADeadlockDetector>("死循环检测");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("═══ 技能图死循环检测器 ═══", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "在编辑器后台无头运行所有技能图，检测哪个节点执行超时（默认 5 秒）。" +
            "通过每帧手动驱动 SkillExecution.Tick() 模拟真实运行时行为。",
            MessageType.Info);

        EditorGUILayout.Space();

        _timeoutSeconds = EditorGUILayout.Slider("超时阈值 (秒)", _timeoutSeconds, 1f, 30f);
        _timeoutFrames = Mathf.RoundToInt(_timeoutSeconds * 60f);

        EditorGUILayout.LabelField($"对应帧数: {_timeoutFrames} 帧（假设 60fps）");

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = !_isRunning;
        if (GUILayout.Button("▶ 运行死循环检测", GUILayout.Height(35)))
            RunDetection();

        if (GUILayout.Button("🗑 清空结果"))
        {
            _results.Clear();
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        if (_isRunning)
        {
            var progressStyle = new GUIStyle(EditorStyles.helpBox) { fontSize = 11 };
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f, 0.3f);
            EditorGUILayout.LabelField($"⏳ 检测中... ({_progress}/{_totalGraphs}) 当前图: {_currentGraphName}",
                progressStyle);
            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space();
            var progress = _totalGraphs > 0 ? (float)_progress / _totalGraphs : 0f;
            EditorGUI.ProgressBar(GUILayoutUtility.GetRect(0, 20), progress,
                $"{progress * 100:F0}%");
        }

        // 结果列表
        var errorCount = _results.Count(r => r.HasDeadlock);
        if (errorCount > 0)
        {
            GUI.backgroundColor = new Color(1f, 0.2f, 0.2f, 0.2f);
            EditorGUILayout.LabelField($"❌ 发现 {errorCount} 个有问题的技能图",
                new GUIStyle(EditorStyles.helpBox) { fontSize = 12, fontStyle = FontStyle.Bold });
            GUI.backgroundColor = Color.white;
        }
        else if (_results.Count > 0)
        {
            GUI.backgroundColor = new Color(0.2f, 1f, 0.2f, 0.2f);
            EditorGUILayout.LabelField("✅ 所有技能图均通过死循环检测！",
                new GUIStyle(EditorStyles.helpBox) { fontSize = 12, fontStyle = FontStyle.Bold });
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("检测结果:", EditorStyles.boldLabel);

        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));

        foreach (var r in _results)
        {
            var icon = r.HasDeadlock ? "❌" : "✅";
            var bgColor = r.HasDeadlock ? new Color(1f, 0.2f, 0.2f, 0.1f) : new Color(0.2f, 1f, 0.2f, 0.08f);

            GUI.backgroundColor = bgColor;
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{icon} {r.GraphName}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"执行 {r.ExecutedTicks} ticks ({r.ElapsedSeconds:F2}s)", EditorStyles.miniLabel);

            if (r.HasDeadlock && GUILayout.Button("📍 Ping", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                var graph = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(r.GraphPath);
                EditorGUIUtility.PingObject(graph);
                Selection.activeObject = graph;
            }

            EditorGUILayout.EndHorizontal();

            if (r.HasDeadlock)
            {
                EditorGUILayout.LabelField($"卡住节点: {r.FailedNodeName}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"堆栈:\n{r.StackTrace}",
                    new GUIStyle(EditorStyles.textArea) { fontSize = 9, wordWrap = true });
            }

            EditorGUILayout.EndVertical();
            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space(2f);
        }

        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("💾 导出报告"))
        {
            ExportReport();
        }
    }

    private void RunDetection()
    {
        _isRunning = true;
        _results.Clear();
        _progress = 0;

        // 获取所有 SkillGraph 资产
        var graphGuids = AssetDatabase.FindAssets("t:SkillGraphAsset", new[] { "Assets" });
        _totalGraphs = graphGuids.Length;

        foreach (var guid in graphGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var graph = AssetDatabase.LoadAssetAtPath<SkillGraphAsset>(path);
            if (graph == null) continue;

            _currentGraphName = graph.name;
            _progress++;

            // 强制刷新（保持 Editor 响应）
            EditorUtility.DisplayProgressBar(
                "死循环检测",
                $"检测 {_progress}/{_totalGraphs}: {graph.name}",
                (float)_progress / _totalGraphs);

            var result = TestGraphForDeadlock(graph, path);
            if (result != null)
                _results.Add(result);

            // 让编辑器喘口气
            if (_progress % 5 == 0)
                System.Threading.Thread.Sleep(10);
        }

        EditorUtility.ClearProgressBar();
        _isRunning = false;

        var errors = _results.Count(r => r.HasDeadlock);
        if (errors > 0)
        {
            UnityEngine.Debug.LogError($"<color=red><b>[QA Deadlock]</b></color> 发现 {errors} 个技能图有死循环问题！详见检测器窗口。");
        }
        else
        {
            UnityEngine.Debug.Log($"<color=green><b>[QA Deadlock]</b></color> 所有 {_results.Count} 个技能图通过死循环检测！");
        }
    }

    private DeadlockResult TestGraphForDeadlock(SkillGraphAsset graph, string path)
    {
        var sw = Stopwatch.StartNew();
        var ticks = 0;
        var startNode = graph.GetStartNode();

        if (startNode == null)
        {
            return new DeadlockResult
            {
                GraphName = graph.name,
                GraphPath = path,
                HasDeadlock = false,
                ElapsedSeconds = 0,
                ExecutedTicks = 0
            };
        }

        // 创建最小化上下文
        var casterGo = new GameObject("DeadlockTestCaster");
        try
        {
            var caster = casterGo.AddComponent<DummySkillCaster>();
            var ctx = new SkillContext(0, casterGo.transform, null);
            var execution = new SkillExecution();
            execution.Initialize(graph, ctx);

            // 手动驱动 Tick（模拟 SkillTickManager）
            var maxTicks = _timeoutFrames;
            var nodeAtTimeout = "";

            while (execution.IsRunning && !execution.IsInterrupted && ticks < maxTicks)
            {
                execution.Tick(1f / 60f);
                ticks++;
            }

            sw.Stop();

            nodeAtTimeout = execution.CurrentNode != null ? execution.CurrentNode.NodeName : "EndNode";

            if (ticks >= maxTicks && execution.IsRunning)
            {
                // 收集堆栈信息
                var stackTrace = new System.Text.StringBuilder();
                stackTrace.AppendLine($"图: {graph.name}");
                stackTrace.AppendLine($"当前节点: {nodeAtTimeout}");
                stackTrace.AppendLine($"执行 tick 数: {ticks}");

                // 尝试追踪当前执行的节点路径
                if (execution.CurrentFrame != null)
                {
                    stackTrace.AppendLine($"图深度: {execution.Context?.ActiveGraphDepth}");
                    stackTrace.AppendLine($"当前图: {execution.Context?.Blackboard?.GetValue<string>(BBKey.CurrentGraph)}");
                }

                return new DeadlockResult
                {
                    GraphName = graph.name,
                    GraphPath = path,
                    FailedNodeName = nodeAtTimeout,
                    ElapsedSeconds = (float)sw.Elapsed.TotalSeconds,
                    ExecutedTicks = ticks,
                    HasDeadlock = true,
                    StackTrace = stackTrace.ToString()
                };
            }

            return new DeadlockResult
            {
                GraphName = graph.name,
                GraphPath = path,
                ElapsedSeconds = (float)sw.Elapsed.TotalSeconds,
                ExecutedTicks = ticks,
                HasDeadlock = false
            };
        }
        finally
        {
            DestroyImmediate(casterGo);
        }
    }

    private void ExportReport()
    {
        var path = EditorUtility.SaveFilePanelInProject("导出死循环报告",
            $"DeadlockReport_{DateTime.Now:yyyyMMdd_HHmmss}", "txt", "保存报告");
        if (string.IsNullOrEmpty(path)) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"死循环检测报告 — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(new string('=', 60));
        sb.AppendLine($"检测图数量: {_results.Count}");
        sb.AppendLine($"超时阈值: {_timeoutSeconds}s ({_timeoutFrames} ticks)");
        sb.AppendLine($"发现问题: {_results.Count(r => r.HasDeadlock)}");
        sb.AppendLine(new string('=', 60));

        foreach (var r in _results.Where(r => r.HasDeadlock))
        {
            sb.AppendLine();
            sb.AppendLine($"❌ {r.GraphName}");
            sb.AppendLine($"   路径: {r.GraphPath}");
            sb.AppendLine($"   卡住节点: {r.FailedNodeName}");
            sb.AppendLine($"   执行 tick: {r.ExecutedTicks} ({r.ElapsedSeconds:F2}s)");
            sb.AppendLine(r.StackTrace);
        }

        System.IO.File.WriteAllText(path, sb.ToString());
        AssetDatabase.Refresh();
        UnityEngine.Debug.Log($"<color=green>[QA]</color> 报告已导出: {path}");
    }

    /// <summary>临时最小化 SkillCaster，用于死循环测试（不依赖 Unity 生命周期）。</summary>
    private class DummySkillCaster : MonoBehaviour { }
}
#endif
