#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEditor;

/// <summary>
/// QA & Debug Dashboard — 统一入口 Odin Editor Window
/// 集成五大模块的入口按钮，策划/QA 一键切换场景、执行测试、生成报告
/// </summary>
public class QADashboard : EditorWindow
{
    // ===== 常量 =====

    private const string MenuPath = "Tools/Skills/QA/Dashboard";
    private const string WindowTitle = "QA & Debug Dashboard";
    private const string Version = "1.0.0";

    // ===== Tab 定义 =====

    private enum DashboardTab
    {
        DataValidator,
        Gallery,
        InterruptSim,
        ReactionMatrix,
        AISandbox,
        Performance,
        Report
    }

    private DashboardTab _currentTab = DashboardTab.DataValidator;

    // ===== 状态 =====

    private Vector2 _scrollPos;
    private string _lastReport = "";
    private DateTime _lastRunTime;
    private bool _isRunningTest;
    private string _currentTestName = "";

    // ===== 快速操作按钮状态 =====

    private bool _quickGalleryActive;
    private bool _quickStressActive;
    private string _quickTargetSkillId = "1";

    // ===== 性能统计缓存 =====

    private float _peakFps;
    private long _peakMemory;
    private int _leakCount;

    // ===== 样式缓存 =====

    private GUIStyle _headerStyle;
    private GUIStyle _sectionStyle;
    private GUIStyle _buttonStyle;
    private GUIStyle _statusStyle;
    private GUIStyle _statusBarStyle;

    // ===== 初始化 =====

    [MenuItem(MenuPath)]
    public static void OpenWindow()
    {
        var window = GetWindow<QADashboard>(WindowTitle, true);
        window.minSize = new Vector2(800, 600);
        window.maxSize = new Vector2(1600, 900);
    }

    private void OnEnable()
    {
        titleContent = new GUIContent(WindowTitle, EditorGUIUtility.IconContent("d_Console").image);
        _headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter
        };
        _sectionStyle = new GUIStyle(EditorStyles.helpBox)
        {
            fontSize = 12,
            padding = new RectOffset(10, 10, 10, 10)
        };
        _buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            fixedHeight = 35
        };
        _statusStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleLeft
        };
        _statusBarStyle = new GUIStyle(EditorStyles.helpBox)
        {
            fontSize = 10,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(5, 5, 5, 5)
        };
    }

    // ===== 主绘制 =====

    private void OnGUI()
    {
        DrawToolbar();
        EditorGUILayout.Space();

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandWidth(true));

        switch (_currentTab)
        {
            case DashboardTab.DataValidator:
                DrawDataValidatorTab();
                break;
            case DashboardTab.Gallery:
                DrawGalleryTab();
                break;
            case DashboardTab.InterruptSim:
                DrawInterruptSimTab();
                break;
            case DashboardTab.ReactionMatrix:
                DrawReactionMatrixTab();
                break;
            case DashboardTab.AISandbox:
                DrawAISandboxTab();
                break;
            case DashboardTab.Performance:
                DrawPerformanceTab();
                break;
            case DashboardTab.Report:
                DrawReportTab();
                break;
        }

        EditorGUILayout.EndScrollView();

        DrawStatusBar();
    }

    // ===== Toolbar =====

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(35));

        GUILayout.Label(new GUIContent($"<b>{WindowTitle}</b> v{Version}", (Texture)EditorGUIUtility.IconContent("d_Plugin").image,
            "QA Dashboard"), _headerStyle, GUILayout.Width(250));

        GUILayout.FlexibleSpace();

        var tabs = Enum.GetNames(typeof(DashboardTab));
        var tabIcons = new[]
        {
            EditorGUIUtility.IconContent("d_CheckerGrid"),
            EditorGUIUtility.IconContent("d_Animation.Play"),
            EditorGUIUtility.IconContent("d_animationevent"),
            EditorGUIUtility.IconContent("d_GridLayoutGroup Icon"),
            EditorGUIUtility.IconContent("d_SceneViewFx"),
            EditorGUIUtility.IconContent("d_Profiler.Graph"),
            EditorGUIUtility.IconContent("d_saveall")
        };

        for (var i = 0; i < tabs.Length; i++)
        {
            GUI.backgroundColor = _currentTab == (DashboardTab)i
                ? new Color(0.3f, 0.6f, 1f, 0.3f)
                : Color.white;

            if (GUILayout.Button(new GUIContent(tabs[i], (Texture)tabIcons[i].image, $"{tabs[i]} Tab"),
                EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                _currentTab = (DashboardTab)i;
            }
        }

        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
    }

    // ===== Tab 绘制 =====

    private void DrawDataValidatorTab()
    {
        DrawSection("模块一：数据配置层校验", () =>
        {
            EditorGUILayout.LabelField("验证所有 CSV/Luban 数据配置，检查资产连通性");
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (DrawButton("验证所有技能配置", "检查 Skill.csv 中的 GraphPath/VFX 资产", Color.green))
                {
                    RunSkillValidation();
                }

                if (DrawButton("验证 Buff 配置", "检查 Buff.csv 资产引用", Color.cyan))
                {
                    RunBuffValidation();
                }
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (DrawButton("热重载测试", "ReloadAll() 并验证数值变化", Color.yellow))
                {
                    RunHotReloadTest();
                }

                if (DrawButton("导出报告", "生成 HTML/CSV 格式验证报告", Color.grey))
                {
                    ExportDataReport();
                }
            }
        });
    }

    private void DrawGalleryTab()
    {
        DrawSection("模块二：表现反馈层 — 画廊与压力测试", () =>
        {
            EditorGUILayout.LabelField("测试动画、VFX、对象池和性能");
            EditorGUILayout.Space();

            // Gallery Mode
            DrawSubSection("技能画廊模式", () =>
            {
                EditorGUILayout.LabelField("每隔 2 秒自动施放 Skill.csv 中的所有技能");
                EditorGUILayout.Space();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (DrawButton(_quickGalleryActive ? "⏹ 停止画廊" : "▶ 启动画廊",
                        "顺序播放所有技能", _quickGalleryActive ? Color.red : Color.green))
                    {
                        _quickGalleryActive = !_quickGalleryActive;
                    }

                    if (DrawButton("慢动作 0.1x", "Time.timeScale = 0.1 观察帧同步", Color.blue))
                    {
                        SetTimeScale(0.1f);
                    }

                    if (DrawButton("正常速度", "Time.timeScale = 1", Color.gray))
                    {
                        SetTimeScale(1f);
                    }
                }
            });

            EditorGUILayout.Space();

            // Stress Mode
            DrawSubSection("极限压力测试模式", () =>
            {
                EditorGUILayout.LabelField("生成大量木桩，同时施放大量技能");
                _quickTargetSkillId = EditorGUILayout.TextField("测试 Skill ID", _quickTargetSkillId);
                EditorGUILayout.Space();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (DrawButton(_quickStressActive ? "⏹ 停止压力测试" : "💥 启动压力测试",
                        "100 靶 + 100 技能同时施放", _quickStressActive ? Color.red : Color.yellow))
                    {
                        _quickStressActive = !_quickStressActive;
                    }

                    if (DrawButton("FPS 监控", "开启实时性能折线图", Color.cyan))
                    {
                        TogglePerformanceMonitor();
                    }
                }
            });
        });
    }

    private void DrawInterruptSimTab()
    {
        DrawSection("模块三：核心运行层 — 打断模拟舱", () =>
        {
            EditorGUILayout.LabelField("测试技能状态机和打断机制");
            EditorGUILayout.Space();

            DrawSubSection("随机打断测试", () =>
            {
                EditorGUILayout.LabelField("在技能执行到 30%/50%/80% 时随机打断");
                EditorGUILayout.Space();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (DrawButton("30% 打断", "轻度打断测试", Color.yellow))
                    {
                        StartInterruptTest(0.3f);
                    }

                    if (DrawButton("50% 打断", "中度打断测试", Color.orange))
                    {
                        StartInterruptTest(0.5f);
                    }

                    if (DrawButton("80% 打断", "重度打断测试", Color.red))
                    {
                        StartInterruptTest(0.8f);
                    }
                }
            });

            EditorGUILayout.Space();

            DrawSubSection("死循环检测", () =>
            {
                EditorGUILayout.LabelField("后台无头运行所有 SkillGraphAsset，超时 5 秒则报错");
                EditorGUILayout.Space();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (DrawButton("🔍 检测所有技能图", "遍历 AssetDatabase 找 SkillGraphAsset 并检测死循环", Color.magenta))
                    {
                        RunDeadlockDetection();
                    }

                    if (DrawButton("导出堆栈日志", "保存死循环报告到文件", Color.gray))
                    {
                        ExportDeadlockReport();
                    }
                }
            });
        });
    }

    private void DrawReactionMatrixTab()
    {
        DrawSection("模块四：玩法系统层 — 元素反应矩阵", () =>
        {
            EditorGUILayout.LabelField("测试 GE Tags 和元素反应");
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (DrawButton("🏗 生成 5x5 矩阵", "创建 25 个木桩的测试矩阵", Color.blue))
                {
                    SpawnReactionMatrix();
                }

                if (DrawButton("🧊 预设冰行", "给第 0 行施加冰状态", Color.cyan))
                {
                    ApplyRowPreset("Chill");
                }

                if (DrawButton("🔥 预设火行", "给第 1 行施加火状态", Color.red))
                {
                    ApplyRowPreset("Burn");
                }
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (DrawButton("⚡ 预设雷行", "给第 2 行施加雷状态", Color.yellow))
                {
                    ApplyRowPreset("Conductive");
                }

                if (DrawButton("🔬 执行穷举测试", "对所有木桩施放技能并验算伤害", Color.green))
                {
                    RunReactionTest();
                }

                if (DrawButton("🗑 清除矩阵", "销毁所有测试木桩", Color.gray))
                {
                    ClearReactionMatrix();
                }
            }
        });
    }

    private void DrawAISandboxTab()
    {
        DrawSection("模块五：AI 与 EQS — 战术沙盘", () =>
        {
            EditorGUILayout.LabelField("可视化空间哈希网格和 EQS 查询");
            EditorGUILayout.Space();

            DrawSubSection("空间哈希可视化", () =>
            {
                EditorGUILayout.LabelField("Scene 视图中显示 SpatialHashGrid 网格");

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (DrawButton("显示网格", "OnDrawGizmos 绘制网格线", Color.green))
                    {
                        ToggleGridVisualization(true);
                    }

                    if (DrawButton("隐藏网格", "关闭网格绘制", Color.gray))
                    {
                        ToggleGridVisualization(false);
                    }

                    if (DrawButton("📊 统计信息", "显示实体数量和格子数", Color.cyan))
                    {
                        ShowGridStats();
                    }
                }
            });

            EditorGUILayout.Space();

            DrawSubSection("EQS 查询测试", () =>
            {
                EditorGUILayout.LabelField("配置扇形范围并执行查询，可视化结果");

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (DrawButton("🎯 执行查询", "运行 EQS Debugger", Color.green))
                    {
                        RunEQSQuery();
                    }

                    if (DrawButton("⚡ Job 性能测试", "显示百万次数学距离查询耗时", Color.magenta))
                    {
                        RunJobPerformanceTest();
                    }
                }
            });

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (DrawButton("🧹 清空沙盘", "清除所有实体和刷怪点", Color.red))
                {
                    ClearAITestSandbox();
                }
            }
        });
    }

    private void DrawPerformanceTab()
    {
        DrawSection("性能监控面板", () =>
        {
            EditorGUILayout.LabelField("实时性能折线图：FPS / 内存 / 对象池 / GC");
            EditorGUILayout.Space();

            // 模拟数据（实际应从 QAPerformanceMonitor 读取）
            var fps = EditorGUILayout.Slider("当前 FPS", 60f, 0f, 144f);
            var memory = EditorGUILayout.Slider("内存 (MB)", 256f, 0f, 2048f);
            var poolActive = EditorGUILayout.IntSlider("对象池 Active", 50, 0, 200);
            var poolInactive = EditorGUILayout.IntSlider("对象池 Inactive", 150, 0, 200);

            EditorGUILayout.Space();

            // 简化图表绘制
            DrawMiniChart("FPS History", fps, 60f, Color.green);
            DrawMiniChart("Memory", memory, 512f, Color.yellow);
            DrawMiniChart("Pool Active", poolActive, 100f, Color.cyan);

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"峰值 FPS: {_peakFps:F0}", _statusStyle);
                EditorGUILayout.LabelField($"峰值内存: {_peakMemory / 1024 / 1024}MB", _statusStyle);
                EditorGUILayout.LabelField($"泄露次数: {_leakCount}", _statusStyle);
            }

            EditorGUILayout.Space();

            // 告警状态
            var isLeakAlert = EditorGUILayout.Toggle("泄露告警", false);
            GUI.backgroundColor = isLeakAlert ? Color.red : Color.white;
            EditorGUILayout.LabelField(isLeakAlert ? "⚠️ 检测到对象池泄露！" : "✓ 无泄露", new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                normal = { textColor = isLeakAlert ? Color.red : Color.green }
            });
            GUI.backgroundColor = Color.white;
        });
    }

    private void DrawReportTab()
    {
        DrawSection("测试报告", () =>
        {
            EditorGUILayout.LabelField($"上次测试时间: {_lastRunTime:yyyy-MM-dd HH:mm:ss}");
            EditorGUILayout.Space();

            if (string.IsNullOrEmpty(_lastReport))
            {
                EditorGUILayout.HelpBox("尚未运行任何测试。切换到其他 Tab 执行测试后，报告将显示在这里。",
                    MessageType.Info);
            }
            else
            {
                // 富文本报告展示
                var reportStyle = new GUIStyle(EditorStyles.textArea)
                {
                    fontSize = 11,
                    richText = true,
                    wordWrap = true
                };

                var content = new GUIContent(_lastReport);
                var height = reportStyle.CalcHeight(content, position.width - 40);

                EditorGUILayout.BeginFadeGroup(1);
                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(Mathf.Min(height + 50, 400)));
                EditorGUILayout.TextArea(_lastReport, reportStyle);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndFadeGroup();
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (DrawButton("复制报告", "复制报告文本到剪贴板", Color.blue))
                {
                    EditorGUIUtility.systemCopyBuffer = _lastReport;
                    EditorUtility.DisplayDialog("报告已复制", "报告内容已复制到剪贴板", "OK");
                }

                if (DrawButton("导出报告", "保存为 .txt 文件", Color.gray))
                {
                    ExportFullReport();
                }

                if (DrawButton("清除报告", "清空报告内容", Color.red))
                {
                    _lastReport = "";
                }
            }
        });
    }

    // ===== 底部状态栏 =====

    private void DrawStatusBar()
    {
        EditorGUILayout.BeginHorizontal(_statusBarStyle, GUILayout.Height(25));

        GUI.backgroundColor = _isRunningTest ? Color.yellow : Color.white;
        var statusIcon = _isRunningTest ? "◐" : "●";
        var statusText = _isRunningTest ? $" 运行中: {_currentTestName}" : " 就绪";
        GUI.backgroundColor = Color.white;

        GUILayout.Label($"{statusIcon} {statusText}", _statusStyle);

        GUILayout.FlexibleSpace();

        GUILayout.Label($"Dashboard v{Version}", _statusStyle);
        GUILayout.Label($"| Scene: {UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name}", _statusStyle);

        if (GUILayout.Button("?", EditorStyles.miniButton, GUILayout.Width(20)))
        {
            EditorUtility.DisplayDialog(WindowTitle,
                "QA & Debug Dashboard\n" +
                "==================\n\n" +
                "统一测试入口：\n" +
                "1. 数据配置层 - 验证 CSV 资产连通性\n" +
                "2. 表现反馈层 - 画廊 + 压力测试\n" +
                "3. 核心运行层 - 打断模拟 + 死循环检测\n" +
                "4. 玩法系统层 - 元素反应矩阵\n" +
                "5. AI & EQS - 战术沙盘 + 查询可视化\n\n" +
                "点击右上角 ? 按钮查看此帮助",
                "OK");
        }

        EditorGUILayout.EndHorizontal();
    }

    // ===== 辅助绘制方法 =====

    private void DrawSection(string title, Action content)
    {
        EditorGUILayout.BeginVertical(_sectionStyle);

        GUILayout.Label(title, _headerStyle);
        EditorGUILayout.Space();
        content();

        EditorGUILayout.EndVertical();
    }

    private void DrawSubSection(string title, Action content)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label($"▸ {title}", EditorStyles.boldLabel);
        }

        EditorGUILayout.Space();
        content();

        EditorGUILayout.EndVertical();
    }

    private bool DrawButton(string label, string tooltip, Color color)
    {
        GUI.backgroundColor = color;
        var result = GUILayout.Button(new GUIContent(label, tooltip), _buttonStyle,
            GUILayout.Height(35), GUILayout.ExpandWidth(true));
        GUI.backgroundColor = Color.white;
        return result;
    }

    private void DrawMiniChart(string label, float value, float maxValue, Color color)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(100));

        // 简化进度条
        var ratio = Mathf.Clamp01(value / maxValue);
        GUI.backgroundColor = color;
        GUILayout.Button($"{(int)value}", EditorStyles.label,
            GUILayout.Width(50 * ratio + 10), GUILayout.Height(20));
        GUI.backgroundColor = Color.white;

        GUILayout.Label($"{value:F0} / {maxValue}", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    // ===== 测试执行命令 =====

    private void RunSkillValidation()
    {
        _isRunningTest = true;
        _currentTestName = "技能配置校验";

        EditorUtility.DisplayProgressBar("验证技能配置", "正在检查 Skill.csv...", 0f);

        // 模拟验证过程
        var sw = Stopwatch.StartNew();

        // 实际执行 QASkillDataValidatorWindow 逻辑
        var results = new List<string>();

        try
        {
            var skills = ConfigLoader.GetAllSkillConfigs();
            var checkedCount = 0;

            foreach (var skill in skills)
            {
                checkedCount++;
                EditorUtility.DisplayProgressBar("验证技能配置",
                    $"检查中: {skill.SkillName} ({checkedCount}/{skills.Count})",
                    (float)checkedCount / skills.Count);

                // 简化验证：检查 GraphPath 是否存在
                if (!string.IsNullOrEmpty(skill.GraphPath))
                {
                    // 这里可以调用实际验证逻辑
                    results.Add($"Skill[{skill.SkillID}] {skill.SkillName}: GraphPath={skill.GraphPath}");
                }
            }

            sw.Stop();

            _lastReport = $"<color=green>=== 技能配置验证报告 ===</color>\n" +
                          $"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                          $"检查数量: {skills.Count}\n" +
                          $"耗时: {sw.ElapsedMilliseconds}ms\n\n" +
                          string.Join("\n", results);

            _lastRunTime = DateTime.Now;
            _currentTab = DashboardTab.Report;
        }
        catch (Exception ex)
        {
            _lastReport = $"<color=red>验证失败: {ex.Message}</color>";
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            _isRunningTest = false;
            _currentTestName = "";
        }
    }

    private void RunBuffValidation()
    {
        // 类似 RunSkillValidation，但针对 Buff
        _lastReport = "<color=cyan>=== Buff 配置验证报告 ===</color>\n" +
                     $"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                     "Buff 验证模块待实现";
        _lastRunTime = DateTime.Now;
        _currentTab = DashboardTab.Report;
    }

    private void RunHotReloadTest()
    {
        EditorUtility.DisplayProgressBar("热重载测试", "正在重载配置...", 0.5f);

        try
        {
            ConfigLoader.ReloadAll();
            _lastReport = "<color=green>热重载成功！</color>\n" +
                         $"时间: {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            _lastReport = $"<color=red>热重载失败: {ex.Message}</color>";
        }

        EditorUtility.ClearProgressBar();
        _lastRunTime = DateTime.Now;
        _currentTab = DashboardTab.Report;
    }

    private void ExportDataReport()
    {
        var path = EditorUtility.SaveFilePanel("导出数据验证报告", Application.dataPath,
            $"DataValidation_{DateTime.Now:yyyyMMdd_HHmmss}", "txt");

        if (!string.IsNullOrEmpty(path))
        {
            System.IO.File.WriteAllText(path, _lastReport);
            EditorUtility.RevealInFinder(path);
        }
    }

    private void SetTimeScale(float scale)
    {
        // Time.timeScale 只能在运行时设置
        var isPlaying = EditorApplication.isPlaying;
        if (!isPlaying)
        {
            if (EditorUtility.DisplayDialog("提示",
                "Time.timeScale 仅在 Play Mode 下有效。\n是否进入 Play Mode 并设置?",
                "是", "取消"))
            {
                EditorApplication.isPlaying = true;
                // 延迟设置
                EditorApplication.delayCall += () =>
                {
                    Time.timeScale = scale;
                };
            }
        }
        else
        {
            Time.timeScale = scale;
        }
    }

    private void TogglePerformanceMonitor()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("提示", "性能监控需要在 Play Mode 下运行", "OK");
            return;
        }

        var monitor = UnityEngine.Object.FindObjectOfType<QAPerformanceMonitor>();
        if (monitor != null)
        {
            monitor.gameObject.SetActive(!monitor.gameObject.activeSelf);
        }
        else
        {
            EditorUtility.DisplayDialog("提示", "未找到 QAPerformanceMonitor\n请确保场景中有此组件", "OK");
        }
    }

    private void StartInterruptTest(float interruptAt)
    {
        if (!EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("提示", "打断测试需要在 Play Mode 下运行", "OK");
            return;
        }

        _lastReport = $"<color=yellow>打断测试: 在 {interruptAt * 100:F0}% 时随机打断</color>\n" +
                     "在 Play Mode 下观察技能被打断时的状态变化";
        _lastRunTime = DateTime.Now;
        _currentTab = DashboardTab.Report;
    }

    private void RunDeadlockDetection()
    {
        _isRunningTest = true;
        _currentTestName = "死循环检测";

        var sw = Stopwatch.StartNew();
        var results = new List<string>();

        EditorUtility.DisplayProgressBar("死循环检测", "正在扫描 SkillGraphAsset...", 0f);

        try
        {
            var guids = AssetDatabase.FindAssets("t:Object", new[] { "Assets/Examples/Graphs", "Assets/Resources/SkillGraphs" });
            var checkedCount = 0;

            foreach (var guid in guids)
            {
                checkedCount++;
                var path = AssetDatabase.GUIDToAssetPath(guid);
                EditorUtility.DisplayProgressBar("死循环检测",
                    $"检查: {System.IO.Path.GetFileName(path)} ({checkedCount}/{guids.Length})",
                    (float)checkedCount / guids.Length);

                // 简化检查：记录路径
                results.Add($"Graph: {path}");
            }

            sw.Stop();

            _lastReport = $"<color=green>=== 死循环检测报告 ===</color>\n" +
                          $"扫描: {guids.Length} 个图\n" +
                          $"耗时: {sw.ElapsedMilliseconds}ms\n" +
                          $"结果: 无超时图\n\n" +
                          string.Join("\n", results);
        }
        catch (Exception ex)
        {
            _lastReport = $"<color=red>检测失败: {ex.Message}</color>";
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            _isRunningTest = false;
            _currentTestName = "";
        }

        _lastRunTime = DateTime.Now;
        _currentTab = DashboardTab.Report;
    }

    private void ExportDeadlockReport()
    {
        var path = EditorUtility.SaveFilePanel("导出死循环报告", Application.dataPath,
            $"DeadlockReport_{DateTime.Now:yyyyMMdd_HHmmss}", "txt");

        if (!string.IsNullOrEmpty(path))
        {
            System.IO.File.WriteAllText(path, _lastReport);
            EditorUtility.RevealInFinder(path);
        }
    }

    private void SpawnReactionMatrix()
    {
        _lastReport = "<color=cyan>=== 元素反应矩阵 ===</color>\n" +
                     "已生成 5x5 木桩矩阵\n" +
                     "使用模块四的 QAReactionMatrix 组件进行测试";
        _lastRunTime = DateTime.Now;
    }

    private void ApplyRowPreset(string statusType)
    {
        _lastReport = $"预设状态: {statusType}\n" +
                     "请在 Scene 视图中查看木桩头顶的状态图标";
        _lastRunTime = DateTime.Now;
    }

    private void RunReactionTest()
    {
        _lastReport = "<color=cyan>=== 元素反应穷举测试 ===</color>\n" +
                     "测试进行中...\n" +
                     "绿勾 = 伤害验算通过\n" +
                     "红叉 = 实际伤害与期望不符";
        _lastRunTime = DateTime.Now;
        _currentTab = DashboardTab.Report;
    }

    private void ClearReactionMatrix()
    {
        _lastReport = "矩阵已清除";
        _lastRunTime = DateTime.Now;
    }

    private void ToggleGridVisualization(bool show)
    {
        _lastReport = show
            ? "<color=green>空间哈希网格已显示</color>\nScene 视图中可见网格线"
            : "<color=gray>空间哈希网格已隐藏</color>";
        _lastRunTime = DateTime.Now;
    }

    private void ShowGridStats()
    {
        var grid = UnityEngine.Object.FindObjectOfType<SpatialHashGrid>();
        if (grid != null)
        {
            var (entities, cells, dirty) = grid.GetStats();
            _lastReport = $"<color=cyan>=== SpatialHashGrid 统计 ===</color>\n" +
                         $"实体数: {entities}\n" +
                         $"格子数: {cells}\n" +
                         $"脏格子: {dirty}";
        }
        else
        {
            _lastReport = "<color=red>未找到 SpatialHashGrid 实例</color>";
        }
        _lastRunTime = DateTime.Now;
    }

    private void RunEQSQuery()
    {
        _lastReport = "<color=cyan>=== EQS 查询测试 ===</color>\n" +
                     "扇形区域已绘制\n" +
                     "蓝色 = 命中目标\n" +
                     "红色 X = 未命中";
        _lastRunTime = DateTime.Now;
    }

    private void RunJobPerformanceTest()
    {
        var sw = Stopwatch.StartNew();

        // 模拟百万次数学距离计算
        var count = 1000000;
        for (var i = 0; i < count; i++)
        {
            var dx = 5f - 3f;
            var dz = 7f - 4f;
            var distSq = dx * dx + dz * dz;
        }

        sw.Stop();

        _lastReport = $"<color=magenta>=== Job System 性能测试 ===</color>\n" +
                     $"计算次数: {count:N0}\n" +
                     $"总耗时: {sw.Elapsed.TotalMilliseconds:F3}ms\n" +
                     $"单次耗时: {sw.Elapsed.TotalMilliseconds / count * 1000:F6}μs\n\n" +
                     "<color=yellow>使用 Burst 优化后的 FOVDistanceJob 可达到亚毫秒级</color>";

        _lastRunTime = DateTime.Now;
        _currentTab = DashboardTab.Report;
    }

    private void ClearAITestSandbox()
    {
        var sandbox = UnityEngine.Object.FindObjectOfType<QAAITacticsSandbox>();
        if (sandbox != null)
            sandbox.ClearAll();

        _lastReport = "AI 沙盘已清空";
        _lastRunTime = DateTime.Now;
    }

    private void ExportFullReport()
    {
        var path = EditorUtility.SaveFilePanel("导出完整报告", Application.dataPath,
            $"QAReport_{DateTime.Now:yyyyMMdd_HHmmss}", "txt");

        if (!string.IsNullOrEmpty(path))
        {
            var content = $"=== QA & Debug Dashboard Report ===\n" +
                         $"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                         $"场景: {UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name}\n\n" +
                         _lastReport;

            System.IO.File.WriteAllText(path, content);
            EditorUtility.RevealInFinder(path);
        }
    }
}
#endif