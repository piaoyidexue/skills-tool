using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     技能全局 Tick 驱动器 —— 替代协程系统，统一遍历所有活跃技能执行实例。
///     0 GC 运行时：无 IEnumerator 装箱，无 WaitForSeconds 分配。
///     每个技能实例通过 Tick(deltaTime) 驱动内部状态机。
/// </summary>
[DefaultExecutionOrder(-100)]
public class SkillTickManager : MonoBehaviour
{
    /// <summary>活跃执行实例池（预分配容量，避免运行时扩容）</summary>
    private readonly List<SkillExecution> _activeExecutions = new(128);

    /// <summary>待移除队列（帧末统一清理，避免遍历中修改集合）</summary>
    private readonly List<SkillExecution> _pendingRemovals = new(32);

    /// <summary>可复用的执行实例对象池（可选，大规模场景收益显著）</summary>
    private readonly Stack<SkillExecution> _pool = new(64);

    /// <summary>是否正在 Tick 遍历中（防止重入）</summary>
    private bool _isTicking;

    [Header("Debug")]
    [SerializeField] private bool _showDebugInfo;

    public static SkillTickManager Instance { get; private set; }

    /// <summary>当前活跃执行数</summary>
    public int ActiveCount => _activeExecutions.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        var dt = Time.deltaTime;
        TickAll(dt);
    }

    /// <summary>
    ///     核心 Tick 循环 —— 遍历所有活跃执行，驱动节点状态机。
    /// </summary>
    private void TickAll(float deltaTime)
    {
        if (_activeExecutions.Count == 0) return;

        _isTicking = true;

        for (var i = _activeExecutions.Count - 1; i >= 0; i--)
        {
            var execution = _activeExecutions[i];

            // 已标记中断 → 延迟移除
            if (execution.IsInterrupted)
            {
                _pendingRemovals.Add(execution);
                continue;
            }

            // 驱动一帧
            execution.Tick(deltaTime);

            // 执行完成（无当前节点且不在运行中）→ 延迟移除
            if (execution.CurrentNode == null && !execution.IsRunning)
            {
                execution.NotifyCompleted();       // ✅ 改成调用刚才新增的这个方法
                _pendingRemovals.Add(execution);
            }
        }

        _isTicking = false;

        // 帧末清理已完成/中断的执行
        if (_pendingRemovals.Count > 0)
        {
            foreach (var exec in _pendingRemovals)
            {
                _activeExecutions.Remove(exec);
                RecycleExecution(exec);
            }

            _pendingRemovals.Clear();
        }
    }

    /// <summary>
    ///     注册一个新的技能执行实例到全局 Tick 循环。
    /// </summary>
    public SkillExecution Register(SkillGraph graph, SkillContext context)
    {
        var execution = GetOrCreateExecution();
        execution.Initialize(graph, context);
        _activeExecutions.Add(execution);
        return execution;
    }

    /// <summary>
    ///     立即中断指定技能执行（安全，可在 Tick 遍历中调用）。
    /// </summary>
    public void Interrupt(SkillExecution execution)
    {
        if (execution == null) return;
        execution.MarkInterrupted();
    }

    /// <summary>
    ///     中断指定 Caster 的所有活跃技能执行。
    /// </summary>
    public void InterruptAll(SkillCaster caster)
    {
        if (caster == null) return;

        for (var i = _activeExecutions.Count - 1; i >= 0; i--)
        {
            var exec = _activeExecutions[i];
            if (exec.Context?.CasterComponent == caster)
            {
                exec.MarkInterrupted();
            }
        }
    }

    /// <summary>
    ///     强制清空所有活跃执行（场景切换时调用）。
    /// </summary>
    public void ClearAll()
    {
        foreach (var exec in _activeExecutions)
        {
            exec.MarkInterrupted();
        }

        _activeExecutions.Clear();
        _pendingRemovals.Clear();
    }

    private SkillExecution GetOrCreateExecution()
    {
        return _pool.Count > 0 ? _pool.Pop() : new SkillExecution();
    }

    private void RecycleExecution(SkillExecution execution)
    {
        execution.Reset();
        if (_pool.Count < 128)
        {
            _pool.Push(execution);
        }
    }

    private void OnDestroy()
    {
        ClearAll();
        _pool.Clear();

        if (Instance == this)
        {
            Instance = null;
        }
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (!_showDebugInfo) return;

        GUILayout.BeginArea(new Rect(10, 10, 350, 500));
        GUILayout.Label($"<b>Skill Tick Manager</b>", new GUIStyle(GUI.skin.label) { richText = true });
        GUILayout.Label($"Active Executions: {_activeExecutions.Count}");
        GUILayout.Label($"Pool Size: {_pool.Count}");

        GUILayout.Space(10);
        GUILayout.Label("<b>Active Skills:</b>", new GUIStyle(GUI.skin.label) { richText = true });

        foreach (var exec in _activeExecutions)
        {
            var name = exec.Context?.Config?.SkillName ?? "Unknown";
            var node = exec.CurrentNode != null ? exec.CurrentNode.name : "End";
            GUILayout.Label($"  {name} @ {node}");
        }

        GUILayout.EndArea();
    }
#endif
}
