using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
///     模块二：画廊与压力测试控制器。
///     Gallery Mode：按顺序每隔 2 秒释放 Skill.csv 中每个技能，可在 Scene 视图肉眼观察同步性。
///     Stress Mode：生成 N 个木桩 + 发射 N 个雷链/AOE 技能，监控性能。
///     实时性能监控集成 QAPerformanceMonitor。
/// </summary>
public class QAGalleryTestController : MonoBehaviour
{
    // ---- 模式枚举 ----
    public enum TestMode { Idle, Gallery, Stress }
    public enum GallerySpeed { Normal, SlowMotion01, SlowMotion02 }

    [Header("=== 靶子配置 ===")]
    [Tooltip("测试用靶子预制体（推荐 QATargetDummy）")]
    [SerializeField] private GameObject _targetPrefab;

    [Tooltip("靶子生成阵型间隔")]
    [SerializeField] private float _targetSpacing = 3f;

    [Tooltip("靶子初始数量（画廊模式）")]
    [SerializeField] private int _galleryTargetCount = 5;

    [Header("=== Gallery 模式配置 ===")]
    [Tooltip("技能切换间隔（秒）")]
    [SerializeField] private float _galleryInterval = 2f;

    [Tooltip("画廊速度")]
    [SerializeField] private GallerySpeed _gallerySpeed = GallerySpeed.SlowMotion01;

    [Header("=== Stress 模式配置 ===")]
    [Tooltip("压力测试靶子数量")]
    [SerializeField] private int _stressTargetCount = 100;

    [Tooltip("每次施法间隔（秒）")]
    [SerializeField] private float _stressCastInterval = 0.05f;

    [Tooltip("压力测试目标技能 ID（默认链燃 11002）")]
    [SerializeField] private int _stressSkillId = 11002;

    [Header("=== 性能监控 ===")]
    [SerializeField] private bool _autoStartMonitor = true;

    // ---- 运行时状态 ----
    private TestMode _currentMode = TestMode.Idle;
    private float _galleryTimer;
    private int _gallerySkillIndex;
    private float _stressTimer;
    private int _stressCastCount;
    private int _stressFrameCount;

    private GameObject _caster;
    private SkillCaster _casterComponent;
    private readonly List<GameObject> _targets = new();
    private readonly List<IInterruptible> _interruptibleTargets = new();

    // ---- 性能记录 ----
    private float _peakFPS = float.MaxValue;
    private int _peakGC;
    private int _leakAlertCount;
    private float _testDuration;
    private int _totalDamageDealt;

    // ---- 统计 ----
    private int _gallerySkillsTested;
    private int _stressSkillsCast;

    // ---- GUI ----
    private bool _showPanel = true;
    private Vector2 _logScroll;
    private readonly List<string> _logLines = new();

    private void Awake()
    {
        EnsureManagers();
        if (_autoStartMonitor && QAPerformanceMonitor.Instance == null)
        {
            var pm = new GameObject("QAPerformanceMonitor").AddComponent<QAPerformanceMonitor>();
            pm.transform.SetParent(transform);
        }
    }

    private void EnsureManagers()
    {
        if (SkillTickManager.Instance == null)
        {
            var stm = new GameObject("SkillTickManager").AddComponent<SkillTickManager>();
            DontDestroyOnLoad(stm);
        }

        if (SpatialHashGrid.Instance == null)
        {
            var shg = new GameObject("SpatialHashGrid").AddComponent<SpatialHashGrid>();
            DontDestroyOnLoad(shg);
        }

        ConfigLoader.Initialize();
    }

    private void Start()
    {
        CreateCaster();
        SpawnGalleryTargets();
    }

    private void Update()
    {
        ApplyTimeScale();
        UpdateTimers();
    }

    private void ApplyTimeScale()
    {
        var scale = _currentMode switch
        {
            TestMode.Gallery when _gallerySpeed == GallerySpeed.SlowMotion01 => 0.1f,
            TestMode.Gallery when _gallerySpeed == GallerySpeed.SlowMotion02 => 0.05f,
            _ => 1f
        };
        Time.timeScale = Mathf.Lerp(Time.timeScale, scale, Time.deltaTime * 5f);
    }

    private void UpdateTimers()
    {
        switch (_currentMode)
        {
            case TestMode.Gallery:
                _galleryTimer += Time.unscaledDeltaTime; // 使用非缩放时间
                if (_galleryTimer >= _galleryInterval)
                {
                    _galleryTimer = 0f;
                    AdvanceGallerySkill();
                }
                break;

            case TestMode.Stress:
                _stressTimer += Time.unscaledDeltaTime;
                if (_stressTimer >= _stressCastInterval)
                {
                    _stressTimer = 0f;
                    CastStressSkill();
                    _stressFrameCount++;

                    // 动态添加靶子（每 200 帧加一批）
                    if (_stressFrameCount % 200 == 0 && _targets.Count < _stressTargetCount)
                        SpawnMoreTargets(Mathf.Min(10, _stressTargetCount - _targets.Count));
                }
                break;
        }

        // 记录性能
        if (QAPerformanceMonitor.Instance != null)
        {
            var fps = QAPerformanceMonitor.Instance.CurrentFPS;
            if (fps < _peakFPS && fps > 0) _peakFPS = fps;

            var mem = (int)(GC.GetTotalMemory(false) / 1024);
            if (mem > _peakGC) _peakGC = mem;

            if (QAPerformanceMonitor.Instance.IsLeakAlert) _leakAlertCount++;
        }
    }

    // ============================================================
    //  Gallery Mode
    // ============================================================

    [ContextMenu("启动 Gallery 模式")]
    public void StartGallery()
    {
        StopAllTests();
        _currentMode = TestMode.Gallery;
        _gallerySkillIndex = 0;
        _galleryTimer = 0f;
        _gallerySkillsTested = 0;

        Time.timeScale = _gallerySpeed switch
        {
            GallerySpeed.SlowMotion01 => 0.1f,
            GallerySpeed.SlowMotion02 => 0.05f,
            _ => 1f
        };

        Log("🎬 Gallery 模式已启动");
        Log($"⏱ TimeScale = {Time.timeScale}，切换间隔 {_galleryInterval}s");
        Log($"📋 测试技能列表（{ConfigLoader.GetAllSkillConfigs().Count} 个）...");

        CastCurrentGallerySkill();
    }

    [ContextMenu("停止 Gallery 模式")]
    public void StopGallery()
    {
        if (_currentMode != TestMode.Gallery) return;
        StopAllTests();
        Log("🛑 Gallery 模式已停止");
    }

    private void AdvanceGallerySkill()
    {
        var skills = ConfigLoader.GetAllSkillConfigs();
        if (skills.Count == 0) { StopGallery(); return; }

        _gallerySkillIndex = (_gallerySkillIndex + 1) % skills.Count;
        CastCurrentGallerySkill();
    }

    private void CastCurrentGallerySkill()
    {
        var skills = ConfigLoader.GetAllSkillConfigs();
        if (_gallerySkillIndex >= skills.Count) return;

        var skill = skills[_gallerySkillIndex];
        var target = GetNextTarget();

        Log($"▶ [{_gallerySkillIndex + 1}/{skills.Count}] 释放: {skill.SkillName}(ID={skill.SkillID})");

        CastSkill(skill.SkillID, target);
        _gallerySkillsTested++;
    }

    // ============================================================
    //  Stress Mode
    // ============================================================

    [ContextMenu("启动 Stress 模式")]
    public void StartStress()
    {
        StopAllTests();
        _currentMode = TestMode.Stress;
        _stressTimer = 0f;
        _stressCastCount = 0;
        _stressFrameCount = 0;
        _testDuration = 0f;
        _peakFPS = float.MaxValue;
        _peakGC = 0;
        _leakAlertCount = 0;
        _totalDamageDealt = 0;

        // 批量生成靶子
        SpawnMoreTargets(_stressTargetCount);

        Log("💥 Stress 模式已启动！");
        Log($"🎯 靶子数量: {_targets.Count}");
        Log($"⚡ 施法间隔: {_stressCastInterval}s");

        Time.timeScale = 1f;
    }

    [ContextMenu("停止 Stress 模式")]
    public void StopStress()
    {
        if (_currentMode != TestMode.Stress) return;
        StopAllTests();
        GenerateStressReport();
        Log("🛑 Stress 模式已停止");
    }

    private void SpawnMoreTargets(int count)
    {
        if (_targetPrefab == null)
        {
            _targetPrefab = CreateDefaultTarget();
        }

        var existingCount = _targets.Count;
        for (var i = 0; i < count; i++)
        {
            var idx = existingCount + i;
            var x = (idx % 10) * _targetSpacing;
            var z = (idx / 10) * _targetSpacing;
            var pos = new Vector3(x, 0, z + 10f);

            var t = Instantiate(_targetPrefab, pos, Quaternion.identity);
            t.name = $"StressTarget_{idx}";
            SetupTarget(t);
            _targets.Add(t);
        }

        Log($"📦 新增 {count} 个靶子，当前总数: {_targets.Count}");
    }

    private void CastStressSkill()
    {
        if (_targets.Count == 0) return;

        // 随机选择目标（模拟真实战斗）
        var targetIdx = UnityEngine.Random.Range(0, _targets.Count);
        var target = _targets[targetIdx];
        if (target == null) { _targets.RemoveAt(targetIdx); return; }

        CastSkill(_stressSkillId, target.transform);
        _stressCastCount++;
        _testDuration = Time.unscaledTime;

        if (_stressCastCount % 50 == 0)
        {
            Log($"⚡ 已施法 {_stressCastCount} 次，FPS: {QAPerformanceMonitor.Instance?.CurrentFPS:F0}");
        }
    }

    // ============================================================
    //  打断测试
    // ============================================================

    [ContextMenu("执行随机打断测试")]
    public void StartRandomInterruptTest()
    {
        Log("⚡ 随机打断测试已启动");

        // 选中一个当前有施法者的靶子
        var skill = ConfigLoader.GetSkillConfig(11001);
        if (skill == null) { Log("❌ Skill 11001 不存在"); return; }

        var target = GetNextTarget();
        var exec = CastSkill(skill.SkillID, target);

        if (exec != null)
        {
            // 在 30%~80% 之间的随机时刻打断
            var interruptDelay = UnityEngine.Random.Range(0.3f, 0.8f) * skill.CastTime;
            this.Invoke(() =>
            {
                Log($"⚡ 模拟打断！延迟 {interruptDelay:F2}s");
                _casterComponent?.Interrupt(InterruptReason.Stunned);
                Log("✅ 打断已触发，观察节点状态是否立即终止");
            }, interruptDelay);
        }
    }

    private System.Collections.IEnumerator Invoke(Action action, float delay)
    {
        yield return new WaitForSeconds(delay);
        action();
    }

    // ============================================================
    //  核心工具方法
    // ============================================================

    private GameObject CastSkill(int skillId, Transform target)
    {
        if (_casterComponent == null) CreateCaster();

        var success = _casterComponent.TryCast(skillId, target);
        if (!success)
        {
            Log($"❌ 施法失败: SkillID={skillId}, Target={target?.name ?? "null"}");
        }
        return target?.gameObject;
    }

    private void CreateCaster()
    {
        _caster = new GameObject("GalleryCaster");
        _caster.transform.position = new Vector3(0, 0, -3f);
        _casterComponent = _caster.AddComponent<SkillCaster>();

        _caster.AddComponent<GEHost>();
        var spatial = _caster.AddComponent<SandboxSpatialEntity>();
        SpatialHashGrid.Instance?.Register(spatial);

        _casterComponent.OnInterrupted += reason => Log($"⚡ caster interrupted: {reason}");
        _casterComponent.OnStageChanged += (from, to) =>
        {
            if (QAPerformanceMonitor.Instance != null)
            {
                QAPerformanceMonitor.Instance.SetCounterValue("SkillStage", (int)to);
            }
        };
    }

    private void SpawnGalleryTargets()
    {
        if (_targetPrefab == null)
            _targetPrefab = CreateDefaultTarget();

        for (var i = 0; i < _galleryTargetCount; i++)
        {
            var pos = new Vector3((i % 5) * _targetSpacing, 0, 5f);
            var t = Instantiate(_targetPrefab, pos, Quaternion.identity);
            t.name = $"GalleryTarget_{i}";
            SetupTarget(t);
            _targets.Add(t);
        }

        Log($"✅ 画廊靶子已生成 {_galleryTargetCount} 个");
    }

    private void SetupTarget(GameObject t)
    {
        if (t.GetComponent<GEHost>() == null) t.AddComponent<GEHost>();
        if (t.GetComponent<HealthComponent>() == null) t.AddComponent<HealthComponent>();
        if (t.GetComponent<QATargetDummy>() == null) t.AddComponent<QATargetDummy>();

        var spatial = t.AddComponent<SandboxSpatialEntity>();
        SpatialHashGrid.Instance?.Register(spatial);

        // 记录总伤害
        var qa = t.GetComponent<QATargetDummy>();
        if (qa != null)
        {
            qa.OnDamaged += (final, src, raw) => _totalDamageDealt += Mathf.RoundToInt(final);
        }
    }

    private GameObject CreateDefaultTarget()
    {
        var go = new GameObject("QATargetDummy_Auto");
        go.AddComponent<GEHost>();
        go.AddComponent<HealthComponent>();
        go.AddComponent<QATargetDummy>();
        go.AddComponent<SandboxSpatialEntity>();
        return go;
    }

    private Transform GetNextTarget()
    {
        if (_targets.Count == 0) return null;

        // 轮询
        var target = _targets[0];
        _targets.RemoveAt(0);
        _targets.Add(target);
        return target.transform;
    }

    private void StopAllTests()
    {
        _currentMode = TestMode.Idle;
        Time.timeScale = 1f;
        _casterComponent?.ForceIdle();
        Log("⏹ 所有测试已停止");
    }

    // ============================================================
    //  报告
    // ============================================================

    private void GenerateStressReport()
    {
        var duration = Time.unscaledTime;
        var report = $@"
══════════════════════════════════════
💥 Stress 测试报告
══════════════════════════════════════
持续时间: {duration:F2}s
技能施法次数: {_stressCastCount}
靶子峰值数量: {_targets.Count}
FPS 最低值: {_peakFPS:F1}
GC 峰值: {_peakGC / 1_048_576:F1} MB
泄露报警次数: {_leakAlertCount}
总伤害输出: {_totalDamageDealt:N0}
平均 DPS: {(_totalDamageDealt / duration):F0}
══════════════════════════════════════";

        Log(report);
        Debug.Log($"<color=yellow><b>[QA Stress Report]</b></color>{report}");
    }

    // ============================================================
    //  GUI 面板（运行时版本）
    // ============================================================

    private void OnGUI()
    {
        if (!_showPanel) return;

        GUILayout.BeginArea(new Rect(Screen.width - 320, 10, 300, Screen.height - 20));
        var boxStyle = new GUIStyle(GUI.skin.box) { fontSize = 11 };
        GUILayout.BeginVertical(boxStyle, GUILayout.Width(290));

        GUILayout.Label("═══ QA Gallery & Stress ═══", new GUIStyle(GUI.skin.label)
        {
            fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
        });

        GUILayout.Space(5);

        // 模式选择
        GUILayout.BeginHorizontal();
        GUI.enabled = _currentMode == TestMode.Idle;
        if (GUILayout.Button(_currentMode == TestMode.Gallery ? "🎬 运行中..." : "🎬 Gallery"))
            StartGallery();
        GUI.enabled = true;

        GUI.enabled = _currentMode == TestMode.Idle;
        if (GUILayout.Button(_currentMode == TestMode.Stress ? "💥 运行中..." : "💥 Stress"))
            StartStress();
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        if (_currentMode != TestMode.Idle)
        {
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("⏹ 停止测试", GUILayout.Height(25)))
                StopAllTests();
            GUI.backgroundColor = Color.white;
        }

        GUILayout.Space(5);

        // Gallery 速度控制（使用普通 Popup/Slider）
        GUILayout.Label("Gallery 速度:", new GUIStyle(GUI.skin.label) { fontSize = 10 });
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Normal")) _gallerySpeed = GallerySpeed.Normal;
        if (GUILayout.Button("0.1x")) _gallerySpeed = GallerySpeed.SlowMotion01;
        if (GUILayout.Button("0.05x")) _gallerySpeed = GallerySpeed.SlowMotion02;
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        // 压力测试配置
        GUILayout.Label($"Stress Skill ID: {_stressSkillId}", new GUIStyle(GUI.skin.label) { fontSize = 10 });
        GUILayout.Label($"靶子数量: {_stressTargetCount}", new GUIStyle(GUI.skin.label) { fontSize = 10 });

        // 打断测试
        if (GUILayout.Button("⚡ 随机打断测试"))
            StartRandomInterruptTest();

        GUILayout.Space(5);

        // 实时统计
        DrawLiveStats();

        GUILayout.Space(5);

        // 日志
        GUILayout.Label("═══ 实时日志 ═══", new GUIStyle(GUI.skin.box) { fontSize = 11 });
        _logScroll = GUILayout.BeginScrollView(_logScroll, GUILayout.Height(180));
        var logStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 10 };
        foreach (var line in _logLines)
            GUILayout.Label(line, logStyle);
        GUILayout.EndScrollView();

        if (GUILayout.Button("🗑 清空日志"))
            _logLines.Clear();

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private void DrawLiveStats()
    {
        var fps = QAPerformanceMonitor.Instance?.CurrentFPS ?? 0;
        var fpsColor = fps >= 55 ? Color.green : (fps >= 30 ? Color.yellow : Color.red);
        var style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };

        GUI.color = fpsColor;
        GUILayout.Label($"FPS: {fps:F0}", style);
        GUI.color = Color.white;

        GUILayout.Label($"Active Executions: {SkillTickManager.Instance?.ActiveCount ?? 0}");
        GUILayout.Label($"Targets: {_targets.Count}");
        GUILayout.Label($"Total Damage: {_totalDamageDealt:N0}");

        if (_currentMode == TestMode.Gallery)
            GUILayout.Label($"Gallery: [{_gallerySkillIndex + 1}/{ConfigLoader.GetAllSkillConfigs().Count}]");
        else if (_currentMode == TestMode.Stress)
            GUILayout.Label($"Stress Casts: {_stressCastCount}");

        if (QAPerformanceMonitor.Instance?.IsLeakAlert == true)
        {
            GUI.backgroundColor = Color.red;
            GUILayout.Label("⚠️ 疑似对象池泄露！", style);
            GUI.backgroundColor = Color.white;
        }
    }

    private void Log(string msg)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.ff");
        _logLines.Add($"[{timestamp}] {msg}");
        while (_logLines.Count > 200) _logLines.RemoveAt(0);
    }
}
