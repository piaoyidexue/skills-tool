using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  技能测试场景引导器 (SkillTestSceneSetup)
//  一键搭建完整的技能测试场景，验证全链路：
//  输入触发 → SkillCaster 管线 → 技能图执行 → GAS 结算 → 表现层响应
//
//  使用方式：
//  1. 创建空场景
//  2. 创建空 GameObject，挂载此脚本
//  3. 运行场景
//  4. 按 1-5 数字键释放对应技能，按 R 重置靶子
// ============================================================

/// <summary>
///     技能测试场景引导器。
///     一键搭建施法者 + 靶子 + 全链路管线，提供运行时测试 GUI。
///     挂载到任意空 GameObject 上即可运行。
/// </summary>
public class SkillTestSceneSetup : MonoBehaviour
{
    [Header("测试配置")]
    [Tooltip("要测试的技能 ID 列表（对应 Skill.csv 中的 skill_id）")]
    public int[] testSkillIds = { 10001, 20001, 30001, 40001, 12001 };

    [Tooltip("靶子数量")]
    public int targetCount = 3;

    [Tooltip("靶子间距")]
    public float targetSpacing = 3f;

    [Tooltip("靶子距施法者距离")]
    public float targetDistance = 6f;

    [Tooltip("按 R 键重置靶子")]
    public KeyCode resetKey = KeyCode.R;

    [Header("可视化")]
    [Tooltip("显示 GE 状态面板")]
    public bool showPanel = true;

    [Tooltip("显示施法者范围圆")]
    public bool showRangeCircle = true;

    // ---- 运行时状态 ----
    private GameObject _casterObj;
    private SkillOwner _casterOwner;
    private SkillCaster _casterCaster;
    private readonly List<GameObject> _targets = new(8);
    private int _selectedTargetIndex;
    private string _lastLog = string.Empty;

    private void Start()
    {
        // 1. 确保全局单例存在
        EnsureSingletons();

        // 2. 确保配置已加载
        ConfigLoader.Initialize();

        // 3. 创建施法者实体（完整管线）
        CreateCaster();

        // 4. 创建靶子实体（GAS 完整组件）
        CreateTargets();

        // 5. 创建示例技能图（如果不存在）
        EnsureSkillGraphs();

        Debug.Log("[SkillTestScene] 场景搭建完成。按 1-5 释放技能，↑↓ 切换目标，R 重置靶子。");
    }

    private void Update()
    {
        HandleInput();
    }

    // ============================================================
    //  全局单例保障
    // ============================================================

    private static void EnsureSingletons()
    {
        // SkillTickManager
        if (SkillTickManager.Instance == null)
        {
            var go = new GameObject("[SkillTickManager]");
            go.AddComponent<SkillTickManager>();
            DontDestroyOnLoad(go);
        }

        // SpatialHashGrid
        if (FindObjectOfType<SpatialHashGrid>() == null)
        {
            var go = new GameObject("[SpatialHashGrid]");
            go.AddComponent<SpatialHashGrid>();
            DontDestroyOnLoad(go);
        }

        // VFXManager
        VFXManager.EnsureInstance();

        // GameSystemBootstrapper 的 TagDamageRule 由 RuntimeInitializeOnLoadMethod 自动注册
        // ReactionEngineGlobal 同样自动注册
    }

    // ============================================================
    //  施法者实体创建
    // ============================================================

    private void CreateCaster()
    {
        _casterObj = new GameObject("TestCaster");
        _casterObj.transform.position = Vector3.zero;

        // 必要组件链：SkillOwner → SkillCaster → SkillRunner
        _casterOwner = _casterObj.AddComponent<SkillOwner>();
        _casterCaster = _casterObj.AddComponent<SkillCaster>();
        _casterObj.AddComponent<SkillRunner>();

        // GAS：GEHost（Tag容器 + Effect管理）
        _casterObj.AddComponent<GEHost>();

        // 空间注册
        var spatial = _casterObj.AddComponent<SandboxSpatialEntity>();
        SpatialHashGrid.Instance?.Register(spatial);

        // 默认测试第一个技能
        if (testSkillIds != null && testSkillIds.Length > 0)
            _casterOwner.skillID = testSkillIds[0];

        Debug.Log("[SkillTestScene] 施法者创建完成（SkillOwner + SkillCaster + SkillRunner + GEHost）");
    }

    // ============================================================
    //  靶子实体创建
    // ============================================================

    private void CreateTargets()
    {
        for (var i = 0; i < targetCount; i++)
        {
            var angle = (i - (targetCount - 1) / 2f) * 30f;
            var rad = angle * Mathf.Deg2Rad;
            var pos = new Vector3(Mathf.Sin(rad) * targetDistance, 0, Mathf.Cos(rad) * targetDistance);

            var target = new GameObject($"TestTarget_{i}");
            target.transform.position = pos;

            // GAS 完整组件
            target.AddComponent<GEHost>();

            // Health + IDamageable
            var health = target.AddComponent<HealthComponent>();
            health.MaxHealth = 1000f;

            // 空间注册
            var spatial = target.AddComponent<SandboxSpatialEntity>();
            SpatialHashGrid.Instance?.Register(spatial);

            // 表现层：Tag 变更 → VFX 响应
            var responder = target.AddComponent<StatusVFXResponder>();
            responder.mappings = CreateDefaultTagVFXMappings();

            _targets.Add(target);
        }

        Debug.Log($"[SkillTestScene] 已创建 {targetCount} 个靶子（GEHost + HealthComponent + StatusVFXResponder）");
    }

    /// <summary>
    ///     默认 Tag → VFX 映射，覆盖常见状态效果。
    /// </summary>
    private static StatusVFXResponder.TagVFXMapping[] CreateDefaultTagVFXMappings()
    {
        return new[]
        {
            new StatusVFXResponder.TagVFXMapping
            {
                tag = "burn",
                vfxKey = "HitSpark",
                duration = 0.5f,
                scaleMultiplier = 1.2f
            },
            new StatusVFXResponder.TagVFXMapping
            {
                tag = "chill",
                vfxKey = "FrostBurst",
                duration = 0.6f,
                scaleMultiplier = 1.1f
            },
            new StatusVFXResponder.TagVFXMapping
            {
                tag = "conductive",
                vfxKey = "LightningBeam",
                duration = 0.3f,
                scaleMultiplier = 0.8f
            },
            new StatusVFXResponder.TagVFXMapping
            {
                tag = "mark",
                vfxKey = "HitSpark",
                duration = 0.4f,
                scaleMultiplier = 0.6f
            }
        };
    }

    // ============================================================
    //  示例技能图生成
    // ============================================================

    private void EnsureSkillGraphs()
    {
        // 使用运行时技能图工厂为每个测试技能确保图资产可用
        foreach (var skillId in testSkillIds)
        {
            var graph = RuntimeSkillGraphFactory.GetOrCreate(skillId);
            if (graph != null)
            {
                var config = ConfigLoader.GetSkillConfig(skillId);
                Debug.Log($"[SkillTestScene] 技能 {config?.SkillName ?? skillId.ToString()} 图就绪: {graph.name}");
            }
        }
    }

    // ============================================================
    //  输入处理
    // ============================================================

    private void HandleInput()
    {
        if (_casterOwner == null) return;

        // 数字键 1-9 释放对应技能
        for (var i = 0; i < testSkillIds.Length && i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                var target = GetSelectedTarget();
                var success = _casterOwner.CastSkill(testSkillIds[i], target);
                _lastLog = success
                    ? $"释放技能 ID={testSkillIds[i]}"
                    : $"技能 ID={testSkillIds[i]} 释放失败（冷却/忙碌/无图）";
            }
        }

        // ↑↓ 切换靶子
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            _selectedTargetIndex = (_selectedTargetIndex + 1) % _targets.Count;
        }
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            _selectedTargetIndex = (_selectedTargetIndex - 1 + _targets.Count) % _targets.Count;
        }

        // R 重置靶子
        if (Input.GetKeyDown(resetKey))
        {
            ResetTargets();
        }
    }

    private Transform GetSelectedTarget()
    {
        if (_targets.Count == 0) return null;
        _selectedTargetIndex = Mathf.Clamp(_selectedTargetIndex, 0, _targets.Count - 1);
        return _targets[_selectedTargetIndex].transform;
    }

    private void ResetTargets()
    {
        foreach (var t in _targets)
        {
            if (t == null) continue;
            var geHost = t.GetComponent<GEHost>();
            geHost?.ClearAll();

            var health = t.GetComponent<HealthComponent>();
            if (health != null) health.ResetToMax();
        }

        Debug.Log("[SkillTestScene] 所有靶子已重置");
    }

    // ============================================================
    //  场景可视化
    // ============================================================

    private void OnDrawGizmos()
    {
        if (!showRangeCircle || _casterObj == null) return;

        // 施法者范围圆
        Gizmos.color = Color.cyan;
        DrawCircle(_casterObj.transform.position, 8f);

        // 当前选中靶子高亮
        if (_targets.Count > 0 && _selectedTargetIndex < _targets.Count)
        {
            var selected = _targets[_selectedTargetIndex];
            if (selected != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(selected.transform.position, 1f);
            }
        }
    }

    private static void DrawCircle(Vector3 center, float radius, int segments = 32)
    {
        for (var i = 0; i < segments; i++)
        {
            var a1 = (float)i / segments * Mathf.PI * 2f;
            var a2 = (float)(i + 1) / segments * Mathf.PI * 2f;
            Gizmos.DrawLine(
                center + new Vector3(Mathf.Cos(a1) * radius, 0, Mathf.Sin(a1) * radius),
                center + new Vector3(Mathf.Cos(a2) * radius, 0, Mathf.Sin(a2) * radius));
        }
    }

    // ============================================================
    //  GUI 面板
    // ============================================================

    private void OnGUI()
    {
        if (!showPanel) return;

        var style = new GUIStyle(GUI.skin.box) { fontSize = 13, alignment = TextAnchor.UpperLeft };
        var labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };

        GUILayout.BeginArea(new Rect(10, 10, 420, Screen.height - 20));

        // ── 操作提示 ──
        GUILayout.BeginVertical(style);
        GUILayout.Label("═══ Skill Test Scene ═══", labelStyle);
        GUILayout.Label($"施法者: {_casterObj?.name ?? "未创建"}", labelStyle);
        GUILayout.Label($"施法阶段: {_casterCaster?.CurrentStage ?? CastStage.Idle}", labelStyle);
        GUILayout.Label(string.Empty, labelStyle);

        GUILayout.Label("操作:", labelStyle);
        GUILayout.Label("  [1-5] 释放技能  |  [↑↓] 切换靶子  |  [R] 重置", labelStyle);
        GUILayout.Label($"  当前靶子: [{_selectedTargetIndex}]", labelStyle);
        GUILayout.Label(string.Empty, labelStyle);

        if (!string.IsNullOrEmpty(_lastLog))
        {
            GUILayout.Label($"最近操作: {_lastLog}", labelStyle);
        }

        GUILayout.EndVertical();

        GUILayout.Space(8);

        // ── 技能列表 ──
        GUILayout.BeginVertical(style);
        GUILayout.Label("═══ 技能列表 ═══", labelStyle);
        for (var i = 0; i < testSkillIds.Length; i++)
        {
            var cfg = ConfigLoader.GetSkillConfig(testSkillIds[i]);
            var name = cfg != null ? cfg.SkillName : $"Unknown({testSkillIds[i]})";
            var dmg = cfg?.Damage ?? 0;
            var cd = cfg?.Cooldown ?? 0;
            var range = cfg?.CastRange ?? 0;
            GUILayout.Label($"  [{i + 1}] {name}  DMG={dmg}  CD={cd}s  Range={range}", labelStyle);
        }
        GUILayout.EndVertical();

        GUILayout.Space(8);

        // ── 靶子 GE 状态 ──
        GUILayout.BeginVertical(style);
        GUILayout.Label("═══ 靶子状态 ═══", labelStyle);

        foreach (var target in _targets)
        {
            if (target == null) continue;

            var geHost = target.GetComponent<GEHost>();
            var health = target.GetComponent<HealthComponent>();

            var hp = health != null ? $"{health.CurrentHealth:F0}/{health.MaxHealth:F0}" : "N/A";
            var selected = target == GetSelectedTarget()?.gameObject;
            var prefix = selected ? "►" : " ";

            var effects = geHost?.ActiveEffects;
            if (effects == null || effects.Count == 0)
            {
                GUILayout.Label($"{prefix} {target.name}: HP={hp}  无效果", labelStyle);
            }
            else
            {
                GUILayout.Label($"{prefix} {target.name}: HP={hp}", labelStyle);
                foreach (var ge in effects)
                {
                    var remaining = ge.DurationPolicy == GEDurationPolicy.Infinite
                        ? "∞"
                        : $"{ge.RemainingDuration:F1}s";
                    var tags = ge.GrantedTags.Count > 0 ? $" [{string.Join(", ", ge.GrantedTags)}]" : "";
                    var stacks = ge.StackCount > 1 ? $" x{ge.StackCount}" : "";
                    GUILayout.Label($"    • {ge.Name} ({remaining}){stacks}{tags}", labelStyle);
                }
            }
        }

        GUILayout.EndVertical();

        GUILayout.Space(8);

        // ── 全链路状态 ──
        GUILayout.BeginVertical(style);
        GUILayout.Label("═══ 管线状态 ═══", labelStyle);
        GUILayout.Label($"  SkillTickManager: 活跃={SkillTickManager.Instance?.ActiveCount ?? 0}", labelStyle);
        GUILayout.Label($"  VFXManager: {(VFXManager.Instance != null ? "就绪" : "未初始化")}", labelStyle);
        GUILayout.Label($"  ConfigLoader: 已加载技能={ConfigLoader.GetAllSkillConfigs()?.Count ?? 0}", labelStyle);

        var geCount = ConfigLoader.GetAllGameplayEffectDatas()?.Count ?? 0;
        GUILayout.Label($"  GameplayEffect: 已加载={geCount}", labelStyle);
        GUILayout.EndVertical();

        GUILayout.EndArea();
    }
}
