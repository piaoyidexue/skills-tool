using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     技能沙盒控制器 —— 在独立场景中快速测试技能效果。
///     支持：选择技能 → 生成靶子 → 观察 GE 状态 → 单步调试。
///     挂载到场景中的主摄像机或空 GameObject 上即可工作。
/// </summary>
public class SkillSandboxController : MonoBehaviour
{
    [Header("沙盒设置")]
    [Tooltip("测试目标预制体（需要挂载 GEHost、HealthComponent 等）")]
    public GameObject TargetPrefab;

    [Tooltip("目标生成间距")]
    public float TargetSpacing = 3f;

    [Tooltip("按 R 键重置所有靶子")]
    public KeyCode ResetKey = KeyCode.R;

    [Tooltip("按 F 键执行选中的技能")]
    public KeyCode ExecuteKey = KeyCode.F;

    [Header("调试")]
    [Tooltip("是否显示 GE 状态面板")]
    public bool ShowGEPanel = true;

    [Tooltip("是否启用步进模式（按空格键单步）")]
    public bool StepMode;

    /// <summary>所有可用的技能 ID 列表</summary>
    private IReadOnlyList<SkillConfig> _skillConfigs;

    /// <summary>当前选中的技能索引</summary>
    private int _selectedSkillIndex;

    /// <summary>已生成的靶子 GameObjects</summary>
    private readonly List<GameObject> _targets = new(8);

    /// <summary>施法者</summary>
    private GameObject _caster;

    /// <summary>当前活跃的技能执行</summary>
    private SkillExecution _activeExecution;

    /// <summary>沙盒内置的 GE 宿主（挂载在施法者身上用于测试自身 Buff）</summary>
    private GEHost _casterGEHost;

    private void Awake()
    {
        // 确保 SkillTickManager 存在
        if (SkillTickManager.Instance == null)
        {
            var go = new GameObject("SkillTickManager");
            go.AddComponent<SkillTickManager>();
            DontDestroyOnLoad(go);
        }

        // 确保 SpatialHashGrid 存在
        if (FindObjectOfType<SpatialHashGrid>() == null)
        {
            var go = new GameObject("SpatialHashGrid");
            go.AddComponent<SpatialHashGrid>();
            DontDestroyOnLoad(go);
        }
    }

    private void Start()
    {
        ConfigLoader.Initialize();
        _skillConfigs = ConfigLoader.GetAllSkillConfigs();

        if (_skillConfigs == null || _skillConfigs.Count == 0)
        {
            Debug.LogError("[SkillSandbox] No skill configs found. Ensure Skill.csv exists in Resources/Config/.");
            enabled = false;
            return;
        }

        Debug.Log($"[SkillSandbox] Loaded {_skillConfigs.Count} skill configs.");

        CreateCaster();
        SpawnTargets();
    }

    private void Update()
    {
        HandleInput();
    }

    // ---- 输入处理 ----

    private void HandleInput()
    {
        // 技能选择
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.mouseScrollDelta.y > 0)
        {
            _selectedSkillIndex = (_selectedSkillIndex + 1) % _skillConfigs.Count;
            Debug.Log($"[SkillSandbox] Selected skill: {GetSelectedSkillName()} (index={_selectedSkillIndex})");
        }

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.mouseScrollDelta.y < 0)
        {
            _selectedSkillIndex = (_selectedSkillIndex - 1 + _skillConfigs.Count) % _skillConfigs.Count;
            Debug.Log($"[SkillSandbox] Selected skill: {GetSelectedSkillName()} (index={_selectedSkillIndex})");
        }

        // 执行技能
        if (Input.GetKeyDown(ExecuteKey))
        {
            ExecuteSelectedSkill();
        }

        // 重置靶子
        if (Input.GetKeyDown(ResetKey))
        {
            ResetTargets();
        }

        // 步进模式
        if (StepMode && Input.GetKeyDown(KeyCode.Space))
        {
            _activeExecution?.Step();
        }
    }

    // ---- 技能执行 ----

    private void ExecuteSelectedSkill()
    {
        if (_skillConfigs == null || _selectedSkillIndex >= _skillConfigs.Count) return;

        var config = _skillConfigs[_selectedSkillIndex];
        if (string.IsNullOrWhiteSpace(config.GraphPath))
        {
            Debug.LogWarning($"[SkillSandbox] Skill '{config.SkillName}' has no GraphPath.");
            return;
        }

        var graph = Resources.Load<SkillGraph>(config.GraphPath);
        if (graph == null)
        {
            Debug.LogError($"[SkillSandbox] Skill graph not found: {config.GraphPath}");
            return;
        }

        // 选择最近的靶子作为目标
        var target = FindNearestTarget();
        if (target == null)
        {
            Debug.LogWarning("[SkillSandbox] No target available. Spawn targets first or press R to reset.");
            return;
        }

        // 构建上下文（使用带参构造函数，确保 SkillID / Config 正确初始化）
        var context = new SkillContext(config.SkillID, _caster.transform, target.transform);

        // 注入技能配置数据到黑板
        context.Blackboard.SetValue(BBKey.DamagePercent, config.DamageRate);
        context.Blackboard.SetValue(BBKey.CritChance, config.CritChance);

        Debug.Log($"[SkillSandbox] Executing: {config.SkillName} (ID={config.SkillID}) → Target={target.name}");

        _activeExecution = SkillTickManager.Instance.Register(graph, context);
        if (_activeExecution != null)
            _activeExecution.OnCompleted += OnSkillExecutionCompleted;

        if (StepMode)
        {
            _activeExecution?.Pause();
            Debug.Log("[SkillSandbox] Step mode ON. Press SPACE to advance each node.");
        }
    }

    // ---- 靶子管理 ----

    private void SpawnTargets()
    {
        if (TargetPrefab == null)
        {
            // 创建默认靶子
            TargetPrefab = CreateDefaultTargetPrefab();
        }

        for (var i = 0; i < 4; i++)
        {
            var pos = new Vector3(
                (i % 2) * TargetSpacing,
                0f,
                (i / 2) * TargetSpacing + 3f
            );

            var target = Instantiate(TargetPrefab, pos, Quaternion.identity);
            target.name = $"SandboxTarget_{i}";

            // 确保靶子有 GEHost
            if (target.GetComponent<GEHost>() == null)
                target.AddComponent<GEHost>();

            // 注册到空间网格
            var spatial = target.GetComponent<ISpatialEntity>();
            if (spatial == null)
            {
                var entity = target.AddComponent<SandboxSpatialEntity>();
                SpatialHashGrid.Instance?.Register(entity);
            }
            else
            {
                SpatialHashGrid.Instance?.Register(spatial);
            }

            _targets.Add(target);
        }
    }

    /// <summary>
    ///     OnCompleted 回调 —— 技能图正常执行完毕时由 SkillTickManager 触发。
    /// </summary>
    private void OnSkillExecutionCompleted()
    {
        Debug.Log("[SkillSandbox] Skill execution completed.");
        _activeExecution = null;
    }

    private void ResetTargets()
    {
        foreach (var t in _targets)
        {
            if (t == null) continue;
            var geHost = t.GetComponent<GEHost>();
            geHost?.ClearAll();

            var health = t.GetComponent<HealthComponent>();
            if (health != null)
                health.ResetToMax();
        }

        Debug.Log("[SkillSandbox] All targets reset.");
    }

    private Transform FindNearestTarget()
    {
        if (_caster == null) return null;

        var casterPos = _caster.transform.position;
        Transform nearest = null;
        var minDist = float.MaxValue;

        foreach (var t in _targets)
        {
            if (t == null) continue;
            var dist = Vector3.Distance(casterPos, t.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = t.transform;
            }
        }

        return nearest;
    }

    // ---- 施法者 ----

    private void CreateCaster()
    {
        _caster = new GameObject("SandboxCaster");
        _caster.transform.position = Vector3.zero;

        _casterGEHost = _caster.AddComponent<GEHost>();

        var spatial = _caster.AddComponent<SandboxSpatialEntity>();
        SpatialHashGrid.Instance?.Register(spatial);

        Debug.Log("[SkillSandbox] Caster created at origin.");
    }

    // ---- GUI 面板 ----

    private void OnGUI()
    {
        if (!ShowGEPanel) return;

        var style = new GUIStyle(GUI.skin.box)
        {
            fontSize = 14,
            alignment = TextAnchor.UpperLeft
        };

        GUILayout.BeginArea(new Rect(10, 10, 400, Screen.height - 20));

        // 技能选择
        GUILayout.BeginVertical(style);
        GUILayout.Label("═══ Skill Sandbox ═══", GUI.skin.label);

        var skillName = GetSelectedSkillName();
        GUILayout.Label($"Selected: [{_selectedSkillIndex}] {skillName}");
        GUILayout.Label($"Scroll / ↑↓ : Select skill");
        GUILayout.Label($"[{ExecuteKey}] : Execute  |  [{ResetKey}] : Reset");
        GUILayout.Label(StepMode ? "Step Mode: ON (SPACE to step)" : "");

        GUILayout.Space(10);

        // 技能配置信息
        if (_selectedSkillIndex < (_skillConfigs?.Count ?? 0))
        {
            var cfg = _skillConfigs[_selectedSkillIndex];
            GUILayout.Label($"Damage: {cfg.Damage}  Cooldown: {cfg.Cooldown}s  Range: {cfg.CastRange}");
            GUILayout.Label($"Crit: {cfg.CritChance:P0}  Radius: {cfg.Radius}  Graph: {cfg.GraphPath}");
        }

        GUILayout.EndVertical();

        GUILayout.Space(10);

        // 靶子 GE 状态
        GUILayout.BeginVertical(style);
        GUILayout.Label("═══ Target GE Status ═══", GUI.skin.label);

        foreach (var target in _targets)
        {
            if (target == null)
            {
                GUILayout.Label("  [DESTROYED]");
                continue;
            }

            var geHost = target.GetComponent<GEHost>();
            var effects = geHost?.ActiveEffects;
            if (effects == null || effects.Count == 0)
            {
                GUILayout.Label($"  {target.name}: No effects");
                continue;
            }

            GUILayout.Label($"  {target.name}:");
            foreach (var ge in effects)
            {
                var remaining = ge.DurationPolicy == GEDurationPolicy.Infinite
                    ? "∞"
                    : $"{ge.RemainingDuration:F1}s";
                var tags = ge.GrantedTags.Count > 0 ? $" [{string.Join(", ", ge.GrantedTags)}]" : "";
                GUILayout.Label($"    • {ge.Name} ({remaining}){tags}");
            }
        }

        GUILayout.EndVertical();

        GUILayout.EndArea();
    }

    private string GetSelectedSkillName()
    {
        if (_skillConfigs == null || _selectedSkillIndex >= _skillConfigs.Count)
            return "<none>";
        return _skillConfigs[_selectedSkillIndex].SkillName ?? "<unnamed>";
    }

    private static GameObject CreateDefaultTargetPrefab()
    {
        var go = new GameObject("DefaultTarget");
        go.AddComponent<GEHost>();

        // 添加简易空间实体
        if (go.GetComponent<SandboxSpatialEntity>() == null)
            go.AddComponent<SandboxSpatialEntity>();

        return go;
    }
}

/// <summary>
///     沙盒专用轻量 ISpatialEntity —— 无需依赖完整的 Entity 组件。
/// </summary>
public class SandboxSpatialEntity : MonoBehaviour, ISpatialEntity
{
    public Vector3 SpatialPosition => transform.position;
    public float SpatialRadius => 1f;
    public int SpatialEntityId => GetInstanceID();
    public bool SpatialIsActive => gameObject.activeInHierarchy;
    public int EntityId { get; }
    public Vector3 Position { get; }
    public int TeamId { get; }
    public bool IsActive { get; }
    public int EntityType { get; }
}
