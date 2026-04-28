using System;
using System.Collections;
using UnityEngine;

/// <summary>
///     释放阶段枚举 —— 贯穿整个施法生命周期的状态标识。
/// </summary>
public enum CastStage
{
    /// <summary>空闲，可接受施法指令</summary>
    Idle,

    /// <summary>施法前腰（前摇 / 蓄力 / 读条）</summary>
    PreCasting,

    /// <summary>执行阶段（技能图正在运行）</summary>
    Executing,

    /// <summary>吟唱中（持续性引导技能）</summary>
    Channeling,

    /// <summary>后摇 / 收招阶段</summary>
    PostCasting,

    /// <summary>被打断</summary>
    Interrupted
}

/// <summary>
///     中断原因枚举
/// </summary>
public enum InterruptReason
{
    /// <summary>主动取消</summary>
    Manual,

    /// <summary>受击硬直 / 眩晕</summary>
    Stunned,

    /// <summary>沉默</summary>
    Silenced,

    /// <summary>位移 / 强制移动</summary>
    ForcedMove,

    /// <summary>死亡</summary>
    Death,

    /// <summary>资源不足</summary>
    ResourceDepleted,

    /// <summary>其他系统强制终止</summary>
    System
}

/// <summary>
///     施法者组件 —— 管理技能释放全生命周期。
///     职责：释放前检查 → 状态机 → 前腰等待 → 启动 SkillRunner → 后摇等待 → 打断监听 → 冷却。
///     支持两种模式：Tick 驱动（默认）和协程模式（向后兼容）。
/// </summary>
[RequireComponent(typeof(SkillRunner))]
public class SkillCaster : MonoBehaviour, IInterruptible
{
    [Header("Cast Settings")]
    [Tooltip("默认最大施法距离")]
    [SerializeField] private float defaultCastRange = 8f;

    [Tooltip("全局前腰时间倍率（1=原始值）")]
    [SerializeField] private float castTimeMultiplier = 1f;

    [Tooltip("全局后摇时间倍率（1=原始值）")]
    [SerializeField] private float postCastMultiplier = 1f;

    [Header("Resources")]
    [Tooltip("当前法力/能量")]
    [SerializeField] private float currentResource = 100f;

    [Tooltip("最大法力/能量")]
    [SerializeField] private float maxResource = 100f;

    // -- internal state --
    private SkillRunner _runner;
    private SkillTickManager _tickManager;
    private SkillExecution _currentExecution;
    private Coroutine _castCoroutine;
    private float _lastCastTime = -999f;
    private int _activeSkillId;

    // -- Tick pipeline state --
    private CastPipelineState _pipelineState;
    private float _pipelineTimer;
    private SkillConfig _pipelineConfig;
    private SkillGraph _pipelineGraph;

    public CastStage CurrentStage { get; private set; } = CastStage.Idle;
    public InterruptReason LastInterruptReason { get; private set; }
    public SkillContext ActiveContext { get; private set; }
    public int ActiveSkillId => _activeSkillId;

    /// <summary>当前资源 / 最大资源</summary>
    public float ResourceRatio => maxResource > 0f ? currentResource / maxResource : 0f;

    /// <summary>当前是否忙碌（不可接受新施法）</summary>
    public bool IsBusy => CurrentStage != CastStage.Idle && CurrentStage != CastStage.Interrupted;

    /// <summary>是否处于可被中断的阶段</summary>
    public bool IsInterruptibleStage =>
        CurrentStage == CastStage.PreCasting ||
        CurrentStage == CastStage.Executing ||
        CurrentStage == CastStage.Channeling;

    /// <summary>被打断回调</summary>
    public event Action<InterruptReason> OnInterrupted;

    /// <summary>阶段变更回调</summary>
    public event Action<CastStage, CastStage> OnStageChanged;

    /// <summary>是否使用 Tick 驱动模式</summary>
    [Header("Execution Mode")]
    [SerializeField] private bool _useTickMode = true;

    private enum CastPipelineState
    {
        Idle,
        PreCast,
        Executing,
        PostCast,
        Complete
    }

    private void Awake()
    {
        _runner = GetComponent<SkillRunner>();
        _tickManager = SkillTickManager.Instance;
        if (_tickManager == null)
            _tickManager = FindObjectOfType<SkillTickManager>();
    }

    private void Update()
    {
        if (!_useTickMode) return;
        TickPipeline(Time.deltaTime);
    }

    /// <summary>
    ///     Tick 驱动的施法管线 —— 替代协程，每帧推进。
    /// </summary>
    private void TickPipeline(float deltaTime)
    {
        if (_pipelineState == CastPipelineState.Idle) return;

        switch (_pipelineState)
        {
            case CastPipelineState.PreCast:
            {
                var castTime = _pipelineConfig.CastTime * castTimeMultiplier;
                if (castTime > 0f)
                {
                    _pipelineTimer += deltaTime;
                    ActiveContext.Blackboard.SetValue(BBKey.PreCastProgress,
                        Mathf.Clamp01(_pipelineTimer / Mathf.Max(castTime, 0.01f)));

                    if (_pipelineTimer < castTime) return; // 继续等待
                }

                // 前腰完成，进入执行阶段
                ActiveContext.Blackboard.SetValue(BBKey.IsPreCasting, false);
                ActiveContext.Blackboard.SetValue(BBKey.PreCastProgress, 1f);

                ChangeStage(CastStage.Executing);
                _currentExecution = _runner.RunSkillTick(_pipelineGraph, ActiveContext);
                _pipelineState = CastPipelineState.Executing;
                break;
            }

            case CastPipelineState.Executing:
            {
                if (_currentExecution != null && _currentExecution.IsRunning)
                    return; // 图仍在运行

                // 图执行完成或被中断
                if (ActiveContext != null && ActiveContext.IsInterrupted)
                {
                    CleanupAndSetInterrupted();
                    break;
                }

                // 进入后摇
                ChangeStage(CastStage.PostCasting);
                _pipelineTimer = 0f;
                ActiveContext.Blackboard.SetValue(BBKey.IsPostCasting, true);
                ActiveContext.Blackboard.SetValue(BBKey.PostCastTime,
                    _pipelineConfig.PostCastTime * postCastMultiplier);
                _pipelineState = CastPipelineState.PostCast;
                break;
            }

            case CastPipelineState.PostCast:
            {
                var postCastTime = _pipelineConfig.PostCastTime * postCastMultiplier;
                if (postCastTime > 0f)
                {
                    _pipelineTimer += deltaTime;
                    ActiveContext.Blackboard.SetValue(BBKey.PostCastProgress,
                        Mathf.Clamp01(_pipelineTimer / Mathf.Max(postCastTime, 0.01f)));

                    if (_pipelineTimer < postCastTime) return;
                }

                // 完成
                ActiveContext.Blackboard.SetValue(BBKey.IsPostCasting, false);
                ActiveContext.Blackboard.SetValue(BBKey.PostCastProgress, 1f);

                _lastCastTime = Time.time;
                ChangeStage(CastStage.Idle);
                ActiveContext = null;
                _activeSkillId = 0;
                _currentExecution = null;
                _pipelineState = CastPipelineState.Idle;
                break;
            }
        }
    }

    private void ChangeStage(CastStage newStage)
    {
        var prev = CurrentStage;
        CurrentStage = newStage;
        OnStageChanged?.Invoke(prev, newStage);
    }

    /// <summary>
    ///     尝试释放技能。失败返回 false，调用方可播放错误提示。
    /// </summary>
    public bool TryCast(int skillId, Transform target)
    {
        var config = ConfigLoader.GetSkillConfig(skillId);
        if (config == null)
        {
            Debug.LogError($"[SkillCaster] Skill config not found: {skillId}");
            return false;
        }

        if (IsBusy)
        {
            Debug.LogWarning($"[SkillCaster] Already casting, stage={CurrentStage}");
            return false;
        }

        if (Time.time - _lastCastTime < config.Cooldown)
        {
            Debug.Log($"[SkillCaster] {config.SkillName} on cooldown ({config.Cooldown:F1}s)");
            return false;
        }

        if (currentResource < config.ResourceCost)
        {
            Debug.Log($"[SkillCaster] Insufficient resource: {currentResource:F0}/{config.ResourceCost:F0}");
            return false;
        }

        var effectiveRange = config.CastRange > 0f ? config.CastRange : defaultCastRange;
        if (target != null && Vector3.Distance(transform.position, target.position) > effectiveRange)
        {
            Debug.Log($"[SkillCaster] Target out of range ({effectiveRange:F1})");
            return false;
        }

        var graph = ResolveGraph(config);
        if (graph == null)
        {
            Debug.LogError($"[SkillCaster] No graph for skill {config.SkillName}");
            return false;
        }

        currentResource = Mathf.Max(0f, currentResource - config.ResourceCost);
        _activeSkillId = skillId;
        ActiveContext = new SkillContext(skillId, transform, target)
        {
            DebugEnabled = _runner.IsDebugMode,
            CasterComponent = this
        };

        if (_useTickMode) {
            StartTickPipeline(config, graph);
        } else {
            _castCoroutine = StartCoroutine(CastPipeline(config, graph));
        }

        return true;
    }

    private void StartTickPipeline(SkillConfig config, SkillGraph graph)
    {
        _pipelineConfig = config;
        _pipelineGraph = graph;
        _pipelineTimer = 0f;

        ChangeStage(CastStage.PreCasting);
        ActiveContext.Blackboard.SetValue(BBKey.IsPreCasting, true);
        ActiveContext.Blackboard.SetValue(BBKey.PreCastTime, config.CastTime * castTimeMultiplier);
        _pipelineState = CastPipelineState.PreCast;
    }

    /// <summary>
    ///     打断当前技能。
    /// </summary>
    public void Interrupt(InterruptReason reason = InterruptReason.Manual)
    {
        if (!IsBusy) return;

        if (reason != InterruptReason.Death)
        {
            if (!IsInterruptibleStage) return;
            if (ActiveContext?.Config != null && !ActiveContext.Config.IsInterruptible)
            {
                Debug.Log($"[SkillCaster] Skill {_activeSkillId} is not interruptible (config).");
                return;
            }
        }

        LastInterruptReason = reason;

        if (ActiveContext != null)
        {
            ActiveContext.IsInterrupted = true;
            ActiveContext.Blackboard.SetValue(BBKey.IsInterrupted, true);
            ActiveContext.Blackboard.SetValue(BBKey.InterruptReason, reason.ToString());
        }

        if (_currentExecution != null)
        {
            _runner.InterruptTick(_currentExecution);
        }

        if (_castCoroutine != null)
        {
            StopCoroutine(_castCoroutine);
            _castCoroutine = null;
        }

        ChangeStage(CastStage.Interrupted);
        OnInterrupted?.Invoke(reason);

        Debug.Log($"[SkillCaster] Interrupted skill {_activeSkillId} due to {reason}");

        ActiveContext = null;
        _activeSkillId = 0;
        _currentExecution = null;
        _pipelineState = CastPipelineState.Idle;
    }

    void IInterruptible.InterruptCast() => Interrupt(InterruptReason.System);

    bool IInterruptible.IsCasting => IsBusy;

    public void AddResource(float amount)
    {
        currentResource = Mathf.Clamp(currentResource + amount, 0f, maxResource);
    }

    public void ResetCooldown()
    {
        _lastCastTime = -999f;
    }

    public void ForceIdle()
    {
        if (_castCoroutine != null)
        {
            StopCoroutine(_castCoroutine);
            _castCoroutine = null;
        }

        if (_currentExecution != null)
        {
            _runner.InterruptTick(_currentExecution);
        }

        CurrentStage = CastStage.Idle;
        ActiveContext = null;
        _activeSkillId = 0;
        _currentExecution = null;
        _pipelineState = CastPipelineState.Idle;
    }

    // ---- internal ----

    /// <summary>
    ///     施法管线（协程模式，向后兼容）。
    /// </summary>
    private IEnumerator CastPipeline(SkillConfig config, SkillGraph graph)
    {
        var prev = CurrentStage;

        // ===== Stage: PreCast =====
        CurrentStage = CastStage.PreCasting;
        OnStageChanged?.Invoke(prev, CurrentStage);

        var castTime = config.CastTime * castTimeMultiplier;
        if (castTime > 0f)
        {
            ActiveContext.Blackboard.SetValue(BBKey.IsPreCasting, true);
            ActiveContext.Blackboard.SetValue(BBKey.PreCastTime, castTime);

            var elapsed = 0f;
            while (elapsed < castTime)
            {
                if (ActiveContext.IsInterrupted)
                {
                    ActiveContext.Blackboard.SetValue(BBKey.IsPreCasting, false);
                    ActiveContext.Blackboard.SetValue(BBKey.PreCastProgress, 0f);
                    CleanupAndSetInterrupted();
                    yield break;
                }

                elapsed += Time.deltaTime;
                ActiveContext.Blackboard.SetValue(BBKey.PreCastProgress,
                    Mathf.Clamp01(elapsed / Mathf.Max(castTime, 0.01f)));
                yield return null;
            }

            ActiveContext.Blackboard.SetValue(BBKey.IsPreCasting, false);
            ActiveContext.Blackboard.SetValue(BBKey.PreCastProgress, 1f);
        }

        // ===== Stage: Executing =====
        prev = CurrentStage;
        CurrentStage = CastStage.Executing;
        OnStageChanged?.Invoke(prev, CurrentStage);

        yield return _runner.RunSkill(graph, ActiveContext);

        if (ActiveContext.IsInterrupted)
        {
            CleanupAndSetInterrupted();
            yield break;
        }

        // ===== Stage: PostCast =====
        prev = CurrentStage;
        CurrentStage = CastStage.PostCasting;
        OnStageChanged?.Invoke(prev, CurrentStage);

        var postCastTime = config.PostCastTime * postCastMultiplier;
        if (postCastTime > 0f)
        {
            ActiveContext.Blackboard.SetValue(BBKey.IsPostCasting, true);
            ActiveContext.Blackboard.SetValue(BBKey.PostCastTime, postCastTime);

            var elapsed = 0f;
            while (elapsed < postCastTime)
            {
                elapsed += Time.deltaTime;
                ActiveContext.Blackboard.SetValue(BBKey.PostCastProgress,
                    Mathf.Clamp01(elapsed / Mathf.Max(postCastTime, 0.01f)));
                yield return null;
            }

            ActiveContext.Blackboard.SetValue(BBKey.IsPostCasting, false);
            ActiveContext.Blackboard.SetValue(BBKey.PostCastProgress, 1f);
        }

        // ===== Finalize =====
        _lastCastTime = Time.time;
        prev = CurrentStage;
        CurrentStage = CastStage.Idle;
        OnStageChanged?.Invoke(prev, CurrentStage);

        _castCoroutine = null;
        ActiveContext = null;
        _activeSkillId = 0;
    }

    private void CleanupAndSetInterrupted()
    {
        ChangeStage(CastStage.Interrupted);
        _castCoroutine = null;
        ActiveContext = null;
        _activeSkillId = 0;
    }

    private SkillGraph ResolveGraph(SkillConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.GraphPath))
        {
            var resourceGraph = Resources.Load<SkillGraph>(config.GraphPath);
            if (resourceGraph != null) return resourceGraph;
        }

        return null;
    }
}

/// <summary>
///     可被打断的接口 —— 战斗系统可通过此接口通知技能被打断。
/// </summary>
public interface IInterruptible
{
    bool IsCasting { get; }
    void InterruptCast();
}
