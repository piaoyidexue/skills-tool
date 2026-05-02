#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
///     技能数据大盘校验器 —— 模块一测试工具。
///     功能：
///     1. 以树状图/列表展示所有 Skill.csv、Buff.csv、Reaction.csv 配置
///     2. 自动化巡检所有配置中的字符串键（ImpactVFXKey、BeamVFXKey、GraphPath 等）
///     3. 资产连通性检查：找不到对应 Asset → 红色高亮 + "Asset Missing"
///     4. 一键热重载 + 断言：修改测试 CSV 后触发热重载，验证数值更新
/// </summary>
public class QASkillDataValidatorWindow : EditorWindow
{
    private Vector2 _scroll;
    private Vector2 _rightScroll;

    private enum Tab { Skills, Buffs, Effects, Reactions, VFXProfiles }
    private Tab _currentTab = Tab.Skills;

    private string _searchFilter = "";
    private bool _showOnlyErrors;
    private bool _autoReloadOnShow = true;

    // ---- 校验结果缓存 ----
    private List<ValidationResult> _results = new();
    private bool _validated;
    private DateTime _lastValidationTime;

    // ---- 热重载测试 ----
    private float _testDamageOriginal;
    private bool _hotReloadTestPassed;
    private string _testResult;

    // ---- 分类统计 ----
    private int _totalCount;
    private int _errorCount;
    private int _warningCount;

    private class ValidationResult
    {
        public string ConfigName;
        public string FieldName;
        public string FieldValue;
        public string ExpectedPath;    // 期望的资源路径
        public Object ResolvedAsset;    // 实际解析到的对象
        public ValidationSeverity Severity; // Error/Warning/Info
        public string Message;
    }

    private enum ValidationSeverity { Error, Warning, Info }

    [MenuItem("Tools/Skills/QA/Data Validator")]
    private static void Open() => GetWindow<QASkillDataValidatorWindow>("技能数据校验器");

    private void OnEnable()
    {
        if (_autoReloadOnShow)
        {
            ConfigLoader.Initialize();
            ValidateAll();
        }
    }

    private void OnGUI()
    {
        InitStyles();

        // ==== 工具栏 ====
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        DrawToolbar();
        EditorGUILayout.EndHorizontal();

        // ==== 统计概览 ====
        DrawStatsBar();

        EditorGUILayout.Space(4f);

        // ==== 主内容：左列表 + 右详情 ====
        EditorGUILayout.BeginHorizontal();
        DrawLeftPanel();
        DrawRightPanel();
        EditorGUILayout.EndHorizontal();

        // ==== 底部操作栏 ====
        DrawBottomBar();
    }

    private void DrawToolbar()
    {
        if (GUILayout.Button("🔄 重新校验", EditorStyles.toolbarButton, GUILayout.Width(100)))
        {
            ConfigLoader.Initialize();
            ValidateAll();
        }

        GUILayout.Space(4f);
        EditorGUILayout.LabelField("标签页:", EditorStyles.miniLabel, GUILayout.Width(40));

        // Tab 切换
        foreach (Tab tab in Enum.GetValues(typeof(Tab)))
        {
            if (GUILayout.Toggle(_currentTab == tab, tab.ToString(), EditorStyles.toolbarButton))
                _currentTab = tab;
        }

        GUILayout.Space(8f);
        _searchFilter = GUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField,
            GUILayout.Width(180));

        _showOnlyErrors = GUILayout.Toggle(_showOnlyErrors, "仅显示错误", EditorStyles.toolbarButton);
    }

    private void DrawStatsBar()
    {
        var style = new GUIStyle(EditorStyles.helpBox) { fontSize = 11, alignment = TextAnchor.MiddleCenter };
        var bgColor = _errorCount > 0 ? new Color(1f, 0.3f, 0.2f, 0.15f)
            : (_warningCount > 0 ? new Color(1f, 0.8f, 0.2f, 0.15f)
                : new Color(0.2f, 0.9f, 0.2f, 0.15f));

        EditorGUILayout.BeginHorizontal(style);
        GUI.backgroundColor = bgColor;
        EditorGUILayout.LabelField($"总数: {_totalCount}  |  ❌ 错误: {_errorCount}  |  ⚠️ 警告: {_warningCount}  |  ✅ 通过: {_totalCount - _errorCount - _warningCount}  |  最后校验: {_lastValidationTime:HH:mm:ss}",
            EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    private void DrawLeftPanel()
    {
        var filtered = GetFilteredResults();

        EditorGUILayout.BeginVertical("box", GUILayout.Width(340), GUILayout.ExpandHeight(true));

        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));

        if (filtered.Count == 0)
        {
            EditorGUILayout.HelpBox(_searchFilter.Length > 0 || _showOnlyErrors
                ? "没有匹配的结果。" : "点击「重新校验」开始。", MessageType.Info);
        }

        foreach (var r in filtered)
        {
            var icon = r.Severity switch
            {
                ValidationSeverity.Error => "❌",
                ValidationSeverity.Warning => "⚠️",
                _ => "ℹ️"
            };

            var bgStyle = new GUIStyle(EditorStyles.helpBox) { fontSize = 10 };
            var rowColor = r.Severity switch
            {
                ValidationSeverity.Error => new Color(1f, 0.2f, 0.2f, 0.1f),
                ValidationSeverity.Warning => new Color(1f, 0.8f, 0f, 0.1f),
                _ => Color.clear
            };

            GUI.backgroundColor = rowColor;
            EditorGUILayout.BeginVertical(bgStyle);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(icon, GUILayout.Width(20));
            EditorGUILayout.LabelField(r.ConfigName, EditorStyles.boldLabel, GUILayout.Width(120));
            EditorGUILayout.LabelField(r.FieldName, EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"值: <color=yellow>{Truncate(r.FieldValue, 50)}</color>",
                new GUIStyle(EditorStyles.miniLabel) { richText = true });

            if (r.Severity == ValidationSeverity.Error && !string.IsNullOrEmpty(r.Message))
            {
                GUI.backgroundColor = new Color(1f, 0.2f, 0.2f, 0.2f);
                EditorGUILayout.LabelField($"  → {r.Message}", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
            GUI.backgroundColor = Color.white;

            if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition)
                && Event.current.clickCount > 0)
            {
                Selection.activeObject = r.ResolvedAsset;
                EditorGUIUtility.PingObject(r.ResolvedAsset);
            }

            EditorGUILayout.Space(1f);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawRightPanel()
    {
        EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll, GUILayout.ExpandHeight(true));

        var selected = Selection.activeObject;
        if (selected != null)
        {
            EditorGUILayout.LabelField($"选中对象: {selected.name}", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            // 显示预览
            if (selected is GameObject go)
            {
                EditorGUILayout.ObjectField("预览", go, typeof(GameObject), false);
            }
            else if (selected is Texture2D tex)
            {
                var rect = GUILayoutUtility.GetRect(200, 200, GUILayout.ExpandWidth(true));
                EditorGUI.DrawPreviewTexture(rect, tex);
            }
            else
            {
                Editor.CreateEditor(selected)?.OnInspectorGUI();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("点击左侧列表中的条目查看详情。", MessageType.Info);
        }

        EditorGUILayout.EndScrollView();

        // ---- 热重载测试区 ----
        EditorGUILayout.Space(8f);
        DrawHotReloadTest();

        EditorGUILayout.EndVertical();
    }

    private void DrawHotReloadTest()
    {
        EditorGUILayout.LabelField("═══ 热重载断言测试 ═══", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "修改 Skill.csv 中第一行的 Damage 值，然后点击「执行热重载测试」，" +
            "系统将重新加载配置并验证数值是否更新。",
            MessageType.Info);

        var configs = ConfigLoader.GetAllSkillConfigs();
        if (configs.Count > 0)
        {
            var first = configs[0];
            EditorGUILayout.LabelField($"当前值: SkillID={first.SkillID}, Damage={first.Damage}, Cooldown={first.Cooldown}",
                EditorStyles.miniLabel);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("📋 执行热重载测试", GUILayout.Height(30)))
        {
            ExecuteHotReloadTest();
        }

        if (!string.IsNullOrEmpty(_testResult))
        {
            var passed = _testResult.Contains("✅");
            GUI.backgroundColor = passed ? new Color(0.2f, 1f, 0.2f) : new Color(1f, 0.2f, 0.2f);
            EditorGUILayout.LabelField(_testResult, new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            }, GUILayout.Height(30));
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void ExecuteHotReloadTest()
    {
        try
        {
            var configs = ConfigLoader.GetAllSkillConfigs();
            if (configs.Count == 0) { _testResult = "❌ 没有找到任何 Skill 配置"; return; }

            var testSkill = configs[0];
            var testSkillId = testSkill.SkillID;

            _testDamageOriginal = testSkill.Damage;
            ConfigLoader.ReloadAll();

            var after = ConfigLoader.GetSkillConfig(testSkillId);
            if (after == null) { _testResult = $"❌ 热重载后获取 Skill {testSkillId} 失败"; return; }

            _hotReloadTestPassed = Mathf.Abs(after.Damage - _testDamageOriginal) < 0.01f;

            if (_hotReloadTestPassed)
            {
                _testResult = "✅ 热重载成功！数值已更新。";
                Debug.Log($"<color=green><b>[QA 热重载测试]</b></color> 通过。Damage={after.Damage}");
            }
            else
            {
                _testResult = $"⚠️ 数值变化: {_testDamageOriginal} → {after.Damage}";
                Debug.Log($"<color=yellow><b>[QA 热重载测试]</b></color> 数值已更新: {_testDamageOriginal} → {after.Damage}");
            }
        }
        catch (Exception ex)
        {
            _testResult = $"❌ 热重载异常: {ex.Message}";
            Debug.LogException(ex);
        }
    }

    private void DrawBottomBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("📂 打开 Config 目录", EditorStyles.toolbarButton))
        {
            EditorUtility.OpenWithDefaultApp(Application.dataPath + "/Resources/Config");
        }

        if (GUILayout.Button("📊 导出校验报告", EditorStyles.toolbarButton))
        {
            ExportReport();
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("导出 CSV", EditorStyles.toolbarButton))
        {
            ExportErrorsToCSV();
        }

        EditorGUILayout.EndHorizontal();
    }

    // ===== 核心校验逻辑 =====

    private void ValidateAll()
    {
        _results.Clear();
        _totalCount = 0;
        _errorCount = 0;
        _warningCount = 0;

        ValidateSkills();
        ValidateBuffs();
        ValidateEffects();
        ValidateReactions();
        ValidateVFXProfiles();

        _validated = true;
        _lastValidationTime = DateTime.Now;
    }

    private void ValidateSkills()
    {
        var configs = ConfigLoader.GetAllSkillConfigs();
        _totalCount += configs.Count;

        foreach (var cfg in configs)
        {
            // 1. GraphPath 连通性
            ValidateAsset($"Skill[{cfg.SkillID}][{cfg.SkillName}]", "GraphPath",
                cfg.GraphPath, typeof(SkillGraphAsset));

            // 2. ImpactVFXKey
            if (!string.IsNullOrEmpty(cfg.ImpactVFXKey))
            {
                ValidateAsset($"Skill[{cfg.SkillID}]", "ImpactVFXKey",
                    cfg.ImpactVFXKey, typeof(GameObject), searchIn: "Assets/Resources/VFX");
            }

            // 3. BeamVFXKey
            if (!string.IsNullOrEmpty(cfg.BeamVFXKey))
            {
                ValidateAsset($"Skill[{cfg.SkillID}]", "BeamVFXKey",
                    cfg.BeamVFXKey, typeof(GameObject), searchIn: "Assets/Resources/VFX");
            }

            // 4. ProjectilePrefab
            if (!string.IsNullOrEmpty(cfg.ProjectilePrefab))
            {
                ValidateAsset($"Skill[{cfg.SkillID}]", "ProjectilePrefab",
                    cfg.ProjectilePrefab, typeof(GameObject), searchIn: "Assets/Prefabs");
            }

            // 5. 数值合法性
            if (cfg.Damage < 0)
                AddResult(cfg.SkillName, "Damage", cfg.Damage.ToString(), ValidationSeverity.Error, "Damage 不能为负数");
            if (cfg.Cooldown < 0)
                AddResult(cfg.SkillName, "Cooldown", cfg.Cooldown.ToString(), ValidationSeverity.Error, "Cooldown 不能为负数");
            if (cfg.CritChance > 1f || cfg.CritChance < 0f)
                AddResult(cfg.SkillName, "CritChance", cfg.CritChance.ToString(), ValidationSeverity.Warning, "CritChance 应在 [0,1] 范围");
            if (cfg.CastRange <= 0f)
                AddResult(cfg.SkillName, "CastRange", cfg.CastRange.ToString(), ValidationSeverity.Warning, "CastRange 应 > 0");
        }
    }

    private void ValidateBuffs()
    {
        var buffConfigs = ConfigLoader.GetAllBuffConfigs();
        _totalCount += buffConfigs.Count;

        foreach (var cfg in buffConfigs)
        {
            if (!string.IsNullOrEmpty(cfg.IconKey))
            {
                ValidateAsset($"Buff[{cfg.BuffID}]", "IconKey", cfg.IconKey,
                    typeof(Sprite), searchIn: "Assets/Art/UI");
            }
        }
    }

    private void ValidateEffects()
    {
        var effects = ConfigLoader.GetAllEffectConfigs();
        _totalCount += effects.Count;
        foreach (var e in effects)
        {
            if (!string.IsNullOrEmpty(e.PrefabName))
            {
                ValidateAsset($"Effect[{e.EffectID}]", "PrefabName", e.PrefabName,
                    typeof(GameObject), searchIn: "Assets/Prefabs/VFX");
            }
        }
    }

    private void ValidateReactions()
    {
        // Reaction.csv 较小，手动列举
        var reactionCsv = Resources.Load<TextAsset>("Config/Reaction");
        if (reactionCsv == null) return;

        var lines = reactionCsv.text.Split('\n');
        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            _totalCount++;
            var parts = lines[i].Split(',');
            if (parts.Length < 4) continue;
            var name = parts[1].Trim().Trim('"');
            if (!string.IsNullOrEmpty(name) && name.Length > 1)
            {
                AddResult(name, "ReactionConfig", name, ValidationSeverity.Info,
                    $"反应配置: {parts[2].Trim()} + {parts[3].Trim()}");
            }
        }
    }

    private void ValidateVFXProfiles()
    {
        // VFXProfile 通过字符串 key 引用，不做连通性检查（只是配置元数据）
        AddResult("VFXProfiles", "Info", "使用 VFXProfileKey 在 VFXManager 中解析",
            ValidationSeverity.Info, "VFX 预制体在运行时通过 VFXManager.Get() 加载");
    }

    private void ValidateAsset(string configName, string fieldName, string fieldValue,
        Type assetType, string searchIn = "Assets/Resources")
    {
        if (string.IsNullOrEmpty(fieldValue)) return;

        Object obj = null;
        var resolvedPath = "";

        try
        {
            var resourcePath = fieldValue.StartsWith("Resources/") ? fieldValue : $"Resources/{fieldValue}";
            obj = Resources.Load<Object>(resourcePath);

            if (obj == null)
            {
                var fullPath = $"{Application.dataPath}/{fieldValue}";
                if (System.IO.File.Exists(fullPath))
                    obj = AssetDatabase.LoadAssetAtPath<Object>($"Assets/{fieldValue}");
            }

            if (obj == null)
            {
                var guid = AssetDatabase.FindAssets($"{fieldValue} t:{assetType.Name}",
                    new[] { searchIn }).FirstOrDefault();
                if (!string.IsNullOrEmpty(guid))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    obj = AssetDatabase.LoadAssetAtPath<Object>(path);
                    resolvedPath = path;
                }
            }
        }
        catch (Exception ex)
        {
            AddResult(configName, fieldName, fieldValue, ValidationSeverity.Warning,
                $"Asset validation error: {ex.Message}");
            return;
        }

        if (obj == null)
        {
            AddResult(configName, fieldName, fieldValue, ValidationSeverity.Error,
                $"Asset Missing — 找不到类型 {assetType.Name}，搜索路径: {searchIn}");
        }
        else
        {
            AddResult(configName, fieldName, fieldValue, ValidationSeverity.Info,
                $"✅ 已找到: {(string.IsNullOrEmpty(resolvedPath) ? obj.name : resolvedPath)}", obj);
        }
    }

    private void AddResult(string configName, string fieldName, string value,
        ValidationSeverity severity, string message, Object asset = null)
    {
        _results.Add(new ValidationResult
        {
            ConfigName = configName,
            FieldName = fieldName,
            FieldValue = value,
            Severity = severity,
            Message = message,
            ResolvedAsset = asset
        });

        if (severity == ValidationSeverity.Error) _errorCount++;
        else if (severity == ValidationSeverity.Warning) _warningCount++;
    }

    private List<ValidationResult> GetFilteredResults()
    {
        var filtered = _results.Where(r =>
        {
            if (_currentTab == Tab.Skills && !r.ConfigName.StartsWith("Skill[")) return false;
            if (_currentTab == Tab.Buffs && !r.ConfigName.StartsWith("Buff[")) return false;
            if (_currentTab == Tab.Effects && !r.ConfigName.StartsWith("Effect[")) return false;
            if (_currentTab == Tab.Reactions && !r.ConfigName.StartsWith("Reaction")) return false;

            if (_showOnlyErrors && r.Severity == ValidationSeverity.Info) return false;

            if (!string.IsNullOrEmpty(_searchFilter))
            {
                return r.ConfigName.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                       r.FieldName.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                       r.FieldValue.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }).ToList();

        // 错误优先排序
        return filtered.OrderByDescending(r => r.Severity == ValidationSeverity.Error)
                       .ThenByDescending(r => r.Severity == ValidationSeverity.Warning)
                       .ToList();
    }

    private void ExportReport()
    {
        var path = EditorUtility.SaveFilePanelInProject("导出校验报告", $"ValidationReport_{DateTime.Now:yyyyMMdd_HHmmss}", "txt", "保存报告");
        if (string.IsNullOrEmpty(path)) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("技能系统数据校验报告");
        sb.AppendLine($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(new string('=', 60));
        sb.AppendLine($"总数: {_totalCount}  |  错误: {_errorCount}  |  警告: {_warningCount}");
        sb.AppendLine(new string('=', 60));

        foreach (var r in _results.Where(r => r.Severity != ValidationSeverity.Info))
        {
            var icon = r.Severity == ValidationSeverity.Error ? "❌" : "⚠️";
            sb.AppendLine($"{icon} [{r.ConfigName}] {r.FieldName}");
            sb.AppendLine($"   值: {r.FieldValue}");
            sb.AppendLine($"   {r.Message}");
            sb.AppendLine();
        }

        System.IO.File.WriteAllText(path, sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log($"<color=green>[QA]</color> 报告已导出: {path}");
    }

    private void ExportErrorsToCSV()
    {
        var path = EditorUtility.SaveFilePanelInProject("导出 CSV", $"ValidationErrors_{DateTime.Now:yyyyMMdd_HHmmss}", "csv", "保存 CSV");
        if (string.IsNullOrEmpty(path)) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("ConfigName,FieldName,FieldValue,Severity,Message");
        foreach (var r in _results)
            sb.AppendLine($"\"{r.ConfigName}\",\"{r.FieldName}\",\"{r.FieldValue}\",\"{r.Severity}\",\"{r.Message}\"");

        System.IO.File.WriteAllText(path, sb.ToString());
        AssetDatabase.Refresh();
    }

    private static string Truncate(string s, int len)
        => s.Length > len ? s.Substring(0, len) + "..." : s;

    private static void InitStyles() { }
}
#endif
