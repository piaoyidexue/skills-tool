using UnityEngine;

/// <summary>
///     技能执行器 —— 统一使用 Tick 驱动系统（0 GC）。
///     通过 SkillTickManager.Register() 注册执行实例，每帧自动推进。
/// </summary>
public class SkillRunner : MonoBehaviour
{
    [HideInInspector] public bool IsDebugMode;

    /// <summary>当前执行上下文（用于调试窗口读取）</summary>
    public SkillContext CurrentContext { get; private set; }
    public SkillExecution CurrentExecution { get; private set; }

    public static SkillRunner Instance { get; private set; }

    private SkillTickManager _tickManager;

    private void Awake()
    {
        Instance = this;
        _tickManager = SkillTickManager.Instance;
        if (_tickManager == null)
            _tickManager = FindObjectOfType<SkillTickManager>();
    }

    // ============================================================
    //  Tick 驱动接口（0 GC）
    // ============================================================

    /// <summary>
    ///     启动技能图执行（Tick 驱动模式）。
    ///     注册到全局 SkillTickManager，每帧自动推进。
    /// </summary>
    public SkillExecution RunSkillTick(SkillGraphAsset graph, SkillContext ctx)
    {
        if (_tickManager == null)
        {
            Debug.LogError("[SkillRunner] SkillTickManager not found.");
            return null;
        }

        var execution = _tickManager.Register(graph, ctx);
        CurrentExecution = execution;
        CurrentContext = ctx;
        return execution;
    }

    /// <summary>
    ///     中断 Tick 驱动的技能执行。
    /// </summary>
    public void InterruptTick(SkillExecution execution)
    {
        _tickManager?.Interrupt(execution);
    }

    // ---- 调试 ----
    public void Pause() => CurrentExecution?.Pause();
    public void Step() => CurrentExecution?.Step();
    public void Continue() => CurrentExecution?.Continue();
}
