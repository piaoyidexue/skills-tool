using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
///     模块四：元素反应矩阵测试沙盒。
///     功能：
///     1. 在场景中生成 5x5 木桩方阵
///     2. Odin Inspector 实时显示选中木桩的 GE Tags 和战斗状态
///     3. 自动化穷举测试：给每排挂载不同状态 → 向所有木桩发射元素技能
///     4. 反应验算可视化：伤害结算对比理论期望值，绿色勾或红色叉显示
/// </summary>
public class QAReactionMatrix : MonoBehaviour
{
    [Header("=== 木桩阵列配置 ===")]
    [SerializeField] private GameObject _targetPrefab;
    [SerializeField] private int _rows = 5;
    [SerializeField] private int _cols = 5;
    [SerializeField] private float _spacing = 2.5f;
    [SerializeField] private Vector3 _origin = new(0, 0, 5f);

    [Header("=== 测试配置 ===")]
    [SerializeField] private int _triggerSkillId = 11001;  // 链燃技能
    [SerializeField] private bool _autoVerify = true;

    // ---- 木桩管理 ----
    private QATargetDummy[,] _matrix;
    private readonly List<QATargetDummy> _allTargets = new();

    // ---- 反应配置缓存 ----
    private IReadOnlyDictionary<string, ReactionConfig> _reactions;

    // ---- 验算结果 ----
    private readonly Dictionary<int, ReactionVerification> _verifications = new();
    private int _currentTestId;

    [Serializable]
    public class ReactionVerification
    {
        public int TargetIndex;
        public string InitialStatus;
        public string ReactionName;
        public float ExpectedDamage;
        public float ActualDamage;
        public bool Passed;
        public string Notes;
    }

    // ---- GUI ----
    private bool _showMatrix = true;
    private QATargetDummy _selectedDummy;
    private Vector2 _scroll;
    private bool _testRunning;

    private void Start()
    {
        ConfigLoader.Initialize();
        _reactions = ConfigLoader.GetAllReactionConfigs();
        BuildMatrix();
        Log("✅ 反应矩阵已初始化");
    }

    // ============================================================
    //  矩阵构建
    // ============================================================

    [ContextMenu("生成 5x5 反应矩阵")]
    public void BuildMatrix()
    {
        ClearMatrix();

        if (_targetPrefab == null)
            _targetPrefab = CreateDefaultTarget();

        _matrix = new QATargetDummy[_rows, _cols];
        _allTargets.Clear();

        for (var r = 0; r < _rows; r++)
        {
            for (var c = 0; c < _cols; c++)
            {
                var pos = _origin + new Vector3(c * _spacing, 0, r * _spacing);
                var t = Instantiate(_targetPrefab, pos, Quaternion.identity);
                t.name = $"Matrix_[{r},{c}]";

                SetupTarget(t);

                var qa = t.GetComponent<QATargetDummy>();
                _matrix[r, c] = qa;
                _allTargets.Add(qa);

                qa.OnDamaged += (final, src, raw) => OnTargetDamaged(qa, final, raw);
            }
        }

        Log($"✅ 矩阵已生成: {_rows}x{_cols} = {_allTargets.Count} 个木桩");
    }

    [ContextMenu("清空矩阵")]
    public void ClearMatrix()
    {
        foreach (var t in _allTargets.Where(t => t != null))
        {
            DestroyImmediate(t.gameObject);
        }
        _allTargets.Clear();
        _verifications.Clear();
        _matrix = null;
    }

    private void SetupTarget(GameObject t)
    {
        if (t.GetComponent<GEHost>() == null) t.AddComponent<GEHost>();
        if (t.GetComponent<HealthComponent>() == null) t.AddComponent<HealthComponent>();
        if (t.GetComponent<QATargetDummy>() == null) t.AddComponent<QATargetDummy>();

        var spatial = t.AddComponent<SandboxSpatialEntity>();
        SpatialHashGrid.Instance?.Register(spatial);
    }

    private GameObject CreateDefaultTarget()
    {
        var go = new GameObject("QATargetDummy_Default");
        go.AddComponent<GEHost>();
        go.AddComponent<HealthComponent>();
        go.AddComponent<QATargetDummy>();
        return go;
    }

    // ============================================================
    //  预设状态注入
    // ============================================================

    /// <summary>给第 row 排的木桩批量挂载指定状态（通过 GEHost 施加 GE）。</summary>
    public void ApplyRowPreset(int row, StatusType status, float duration = 10f)
    {
        if (row < 0 || row >= _rows || _matrix == null) return;

        for (var c = 0; c < _cols; c++)
        {
            var dummy = _matrix[row, c];
            if (dummy == null) continue;

            // 通过 GEHost 施加状态标签对应的 GE
            ApplyGERowPreset(dummy, row, status, duration);

            Log($"[{row},{c}] 挂载状态: {status}");
        }
    }

    private void ApplyGERowPreset(QATargetDummy dummy, int row, StatusType status, float duration)
    {
        var geHost = dummy.GEHostComponent;
        if (geHost == null) return;

        // 将 StatusType 转换为标签名
        var tag = status.ToString().ToLowerInvariant();
        if (status == StatusType.None || string.IsNullOrEmpty(tag)) return;

        // 构造 GE 配置，施加状态标签
        var cfg = new GEConfig
        {
            GEId = tag.GetHashCode(),
            Name = tag,
            DurationPolicy = GEDurationPolicy.HasDuration,
            Duration = duration,
            MaxStacks = 10,
            StackPolicy = GEStackPolicy.Add
        };
        cfg.GrantedTags.Add(tag);

        // 添加伤害修正 Modifier（根据行预设）
        var magnitude = row % 5 switch
        {
            0 => 0.1f,   // 冰：+10% 受伤
            1 => 0.2f,   // 火：+20% 受伤
            2 => 0.15f,  // 雷：+15% 受伤
            3 => 0.3f,   // 混合：+30% 受伤
            _ => 0f
        };
        if (magnitude > 0f)
        {
            cfg.Modifiers.Add(new GEModifier
            {
                Attribute = GEAttribute.DamageTakenMultiplier,
                Operation = GEModOp.Multiply,
                Magnitude = 1f + magnitude
            });
        }

        geHost.ApplyEffect(cfg, null);
    }

    // ============================================================
    //  自动化穷举测试
    // ============================================================

    [ContextMenu("执行自动化反应穷举测试")]
    public void ExecuteReactionTest()
    {
        if (_allTargets.Count == 0) { Log("❌ 请先生成矩阵"); return; }

        _testRunning = true;
        _verifications.Clear();
        _currentTestId++;

        Log("🔬 自动化穷举测试开始...");
        Log($"📊 行预设: 0=冰, 1=火, 2=雷, 3=混合, 4=无");

        // 重置所有木桩
        ResetAll();

        // 注入预设状态
        for (var r = 0; r < _rows; r++)
            ApplyRowPreset(r, (StatusType)(r + 1), 15f);

        // 施放触发技能
        var casterGo = new GameObject("ReactionCaster");
        casterGo.transform.position = _origin + new Vector3(_cols * _spacing / 2f, 0, -3f);
        var caster = casterGo.AddComponent<SkillCaster>();
        caster.GetComponent<GEHost>();

        // 延迟施法
        StartCoroutine(DelayedCast(casterGo));
    }

    private System.Collections.IEnumerator DelayedCast(GameObject casterGo)
    {
        yield return new WaitForSeconds(1f);

        var caster = casterGo.GetComponent<SkillCaster>();
        if (_allTargets.Count > 0)
            caster.TryCast(_triggerSkillId, _allTargets[0].transform);

        yield return new WaitForSeconds(2f);
        _testRunning = false;
        SummarizeTest();

        DestroyImmediate(casterGo);
    }

    [ContextMenu("重置所有木桩")]
    public void ResetAll()
    {
        foreach (var t in _allTargets)
            t?.ResetQA();
        _verifications.Clear();
        Log("🔄 所有木桩已重置");
    }

    private void OnTargetDamaged(QATargetDummy dummy, float finalDamage, float rawDamage)
    {
        var idx = _allTargets.IndexOf(dummy);
        if (idx < 0) return;

        // 查找是否有触发反应的 GE
        var reactionName = DetectReaction(dummy);
        var expectedDamage = ComputeExpectedDamage(dummy, rawDamage, reactionName);

        var verification = new ReactionVerification
        {
            TargetIndex = idx,
            InitialStatus = GetInitialStatus(dummy),
            ReactionName = reactionName,
            ExpectedDamage = expectedDamage,
            ActualDamage = finalDamage,
            Passed = Mathf.Abs(finalDamage - expectedDamage) < 0.5f,
            Notes = reactionName ?? "无反应"
        };

        _verifications[idx] = verification;

        // QA 浮动文字反馈
        var text = Instantiate(Resources.Load<QAFloatingText>("Prefabs/QAFloatingText"),
            dummy.transform.position + Vector3.up * 2.5f, Quaternion.identity);
        if (verification.Passed)
        {
            text.ShowCustom($"✅ {verification.ReactionName}\n{verification.ActualDamage:F0}",
                Color.green, 18);
        }
        else
        {
            text.ShowCustom($"❌ {verification.ReactionName}\n实际:{verification.ActualDamage:F0}/期望:{verification.ExpectedDamage:F0}",
                Color.red, 14);
        }
    }

    private string DetectReaction(QATargetDummy dummy)
    {
        var geHost = dummy.GEHostComponent;
        if (geHost == null) return null;

        var tags = new HashSet<string>();
        foreach (var ge in geHost.ActiveEffects)
        {
            foreach (var tag in ge.GrantedTags)
                tags.Add(tag);
        }

        // 简单反应检测（通过 Reaction.csv）
        foreach (var kvp in _reactions)
        {
            var cfg = kvp.Value;
            if (string.IsNullOrEmpty(cfg.RequiredStatuses)) continue;

            // 检查是否满足反应条件（如 "Burn+Crit"）
            var required = cfg.RequiredStatuses.Split('+')
                .Select(s => s.Trim().ToLowerInvariant()).ToHashSet();

            if (required.All(r => tags.Contains(r)))
            {
                return $"{cfg.ReactionName} (x{cfg.DamageMultiplier:F1})";
            }
        }

        return null;
    }

    private float ComputeExpectedDamage(QATargetDummy dummy, float rawDamage, string reactionName)
    {
        var geHost = dummy.GEHostComponent;
        if (geHost == null) return rawDamage;

        var multiplier = 1f;
        foreach (var ge in geHost.ActiveEffects)
        {
            foreach (var mod in ge.Modifiers)
            {
                if (mod.Attribute == GEAttribute.DamageTakenMultiplier && mod.Operation == GEModOp.Multiply)
                    multiplier *= mod.Magnitude;
            }
        }

        if (!string.IsNullOrEmpty(reactionName))
        {
            var parts = reactionName.Split('x');
            if (parts.Length > 1 && float.TryParse(parts[1].Trim().Trim(')'), out var rm))
                multiplier *= rm;
        }

        return Mathf.Max(1f, rawDamage * multiplier);
    }

    private string GetInitialStatus(QATargetDummy dummy)
    {
        var tags = new List<string>();
        if (dummy.GEHostComponent != null)
            foreach (var ge in dummy.GEHostComponent.ActiveEffects)
                tags.Add(ge.Name ?? $"GE_{ge.GEId}");
        return tags.Count > 0 ? string.Join(", ", tags) : "无";
    }

    private void SummarizeTest()
    {
        var total = _verifications.Count;
        var passed = _verifications.Values.Count(v => v.Passed);
        var failed = total - passed;

        var summary = $"═══════════════════════════\n" +
                      $"🔬 反应穷举测试完成\n" +
                      $"✅ 通过: {passed}/{total}\n" +
                      $"❌ 失败: {failed}/{total}\n" +
                      $"═══════════════════════════";

        Log(summary);

        foreach (var kvp in _verifications.Where(v => !v.Value.Passed))
        {
            var v = kvp.Value;
            Log($"  ❌ 木桩[{kvp.Key}]: 状态={v.InitialStatus}, 期望={v.ExpectedDamage:F1}, 实际={v.ActualDamage:F1}");
        }
    }

    // ============================================================
    //  GUI 面板
    // ============================================================
    //  GUI 面板（运行时版本）
    // ============================================================

    private void OnGUI()
    {
        if (!_showMatrix) return;

        GUILayout.BeginArea(new Rect(Screen.width - 350, 10, 340, Screen.height - 20));
        var boxStyle = new GUIStyle(GUI.skin.box) { fontSize = 11 };
        GUILayout.BeginVertical(boxStyle, GUILayout.Width(330));

        GUILayout.Label("═══ 元素反应矩阵 ═══", new GUIStyle(GUI.skin.label)
        {
            fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
        });

        GUILayout.Space(5);

        // 控制按钮
        GUILayout.Label("木桩控制:", new GUIStyle(GUI.skin.label) { fontSize = 10 });
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("🔨 生成矩阵"))
            BuildMatrix();
        if (GUILayout.Button("🔄 重置"))
            ResetAll();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("🧊 冰"))
            ApplyRowPreset(0, StatusType.Chill);
        if (GUILayout.Button("🔥 火"))
            ApplyRowPreset(1, StatusType.Burn);
        if (GUILayout.Button("⚡ 雷"))
            ApplyRowPreset(2, StatusType.Conductive);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("🧊🔥 混合"))
            ApplyRowPreset(3, StatusType.Chill);
        if (GUILayout.Button("❌ 清除"))
            ApplyRowPreset(4, StatusType.None);
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        GUILayout.Label($"触发 Skill ID: {_triggerSkillId}", new GUIStyle(GUI.skin.label) { fontSize = 10 });

        GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
        if (GUILayout.Button(_testRunning ? "⏳ 测试中..." : "🔬 执行穷举测试", GUILayout.Height(30)))
            ExecuteReactionTest();
        GUI.backgroundColor = Color.white;

        GUILayout.Space(5);

        // 矩阵可视化
        DrawMatrixViewRuntime();

        GUILayout.Space(5);

        // 选中木桩详情
        GUILayout.Label("═══ 选中木桩详情 ═══", GUI.skin.box);

        if (_selectedDummy != null)
        {
            var snap = _selectedDummy.GetSnapshot();
            GUILayout.Label($"木桩: {_selectedDummy.name}", new GUIStyle(GUI.skin.label) { fontSize = 11 });
            GUILayout.Label($"HP: {snap.Health:F0}");
            GUILayout.Label($"存活: {!snap.IsDead}");

            if (snap.GETags.Count > 0)
            {
                GUILayout.Label("GE Tags:", new GUIStyle(GUI.skin.label) { fontSize = 10 });
                foreach (var tag in snap.GETags)
                    GUILayout.Label($"  • {tag}");
            }

            if (snap.StatusTypes.Count > 0)
            {
                GUILayout.Label("战斗状态:", new GUIStyle(GUI.skin.label) { fontSize = 10 });
                for (var i = 0; i < snap.StatusTypes.Count; i++)
                    GUILayout.Label($"  • {snap.StatusTypes[i]}: {snap.StatusValues[i]:F0}");
            }

            var v = _verifications.TryGetValue(_allTargets.IndexOf(_selectedDummy), out var vr) ? vr : null;
            if (v != null)
            {
                GUILayout.Space(5);
                GUI.backgroundColor = v.Passed ? new Color(0.2f, 1f, 0.2f, 0.2f) : new Color(1f, 0.2f, 0.2f, 0.2f);
                GUILayout.Label($"{(v.Passed ? "✅" : "❌")} 反应: {v.ReactionName ?? "无"} | 期望:{v.ExpectedDamage:F0} 实际:{v.ActualDamage:F0}");
                GUI.backgroundColor = Color.white;
            }
        }
        else
        {
            GUILayout.Label("点击场景中的木桩球体选中");
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private void DrawMatrixViewRuntime()
    {
        if (_matrix == null) return;

        GUILayout.Label("矩阵视图（行=预设状态，列=靶子）:", new GUIStyle(GUI.skin.label) { fontSize = 10 });

        for (var r = 0; r < _rows; r++)
        {
            GUILayout.BeginHorizontal();
            var rowLabel = r switch { 0 => "冰", 1 => "火", 2 => "雷", 3 => "混", _ => "无" };
            GUILayout.Label($"[{rowLabel}]", GUILayout.Width(22));

            for (var c = 0; c < _cols; c++)
            {
                var dummy = _matrix[r, c];
                var isSelected = _selectedDummy == dummy;

                if (dummy != null)
                {
                    var snap = dummy.GetSnapshot();
                    var isDead = snap.IsDead;
                    var hasGE = snap.GETags.Count > 0;
                    var v = _verifications.TryGetValue(_allTargets.IndexOf(dummy), out var vr) ? vr : null;

                    Color bgColor;
                    if (v != null)
                        bgColor = v.Passed ? new Color(0.2f, 1f, 0.2f, 0.4f) : new Color(1f, 0.2f, 0.2f, 0.4f);
                    else if (isDead)
                        bgColor = Color.gray;
                    else if (hasGE)
                        bgColor = new Color(0.3f, 0.6f, 1f, 0.4f);
                    else
                        bgColor = new Color(0.8f, 0.8f, 0.8f, 0.3f);

                    if (isSelected) bgColor = new Color(1f, 1f, 0f, 0.6f);

                    GUI.backgroundColor = bgColor;
                    var label = isDead ? "X" : (v != null ? (v.Passed ? "✓" : "✗") : "●");
                    var labelStyle = new GUIStyle(GUI.skin.button)
                    {
                        fontSize = 10,
                        fontStyle = FontStyle.Bold
                    };
                    if (GUILayout.Button(label, labelStyle, GUILayout.Width(22), GUILayout.Height(22)))
                        _selectedDummy = dummy;
                    GUI.backgroundColor = Color.white;
                }
            }

            GUILayout.EndHorizontal();
        }
    }

    private void Log(string msg)
    {
        UnityEngine.Debug.Log($"<color=cyan><b>[QA ReactionMatrix]</b></color> {msg}");
    }

    private void OnDestroy()
    {
        ClearMatrix();
    }
}
