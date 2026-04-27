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
///     【设计要点】CastPipeline 是前腰/后摇的权威计时源，PreCastNode/PostCastNode 仅负责 VFX。
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
    private Coroutine _castCoroutine;
    private float _lastCastTime = -999f;
    private int _activeSkillId;

    public CastStage CurrentStage { get; private set; } = CastStage.Idle;
    public InterruptReason LastInterruptReason { get; private set; }
    public SkillContext ActiveContext { get; private set; }
    public int ActiveSkillId => _activeSkillId;

    /// <summary>当前资源 / 最大资源</summary>
    public float ResourceRatio => maxResource > 0f ? currentResource / maxResource : 0f;

    /// <summary>当前是否忙碌（不可接受新施法）</summary>
    public bool IsBusy => CurrentStage != CastStage.Idle && CurrentStage != CastStage.Interrupted;

    /// <summary>是否处于可被中断的阶段</summary>
    /// <remarks>
    ///     PreCasting: 前腰等待期间可中断
    ///     Executing: 图运行时 PreCastNode/ChannelNode 等节点检测 ctx.IsInterrupted 退出
    ///     Channeling: 吟唱期间可中断
    ///     注意：PostCasting（后摇）阶段不可中断（收招已完成）
    /// </remarks>
    public bool IsInterruptibleStage =>
        CurrentStage == CastStage.PreCasting ||
        CurrentStage == CastStage.Executing ||
        CurrentStage == CastStage.Channeling;

    /// <summary>被打断回调</summary>
    public event Action<InterruptReason> OnInterrupted;

    /// <summary>阶段变更回调</summary>
    public event Action<CastStage, CastStage> OnStageChanged;

    private void Awake()
    {
        _runner = GetComponent<SkillRunner>();
    }

    /// <summary>
    ///     尝试释放技能。失败返回 false，调用方可播放错误提示。
    /// </summary>
    public bool TryCast(int skillId, Transform target)
    {
        // ---- release logic check ----
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

        // ---- consume resource ----
        currentResource = Mathf.Max(0f, currentResource - config.ResourceCost);

        // ---- start cast ----
        _activeSkillId = skillId;
        ActiveContext = new SkillContext(skillId, transform, target)
        {
            DebugEnabled = _runner.IsDebugMode,
            CasterComponent = this
        };

        _castCoroutine = StartCoroutine(CastPipeline(config, graph));
        return true;
    }

    /// <summary>
    ///     打断当前技能。
    ///     检查 IsInterruptibleStage 和 SkillConfig.IsInterruptible 双重条件。
    ///     Death 原因始终生效（死亡强制中断）。
    /// </summary>
    public void Interrupt(InterruptReason reason = InterruptReason.Manual)
    {
        if (!IsBusy) return;

        // 死亡强制中断，不受任何限制
        if (reason != InterruptReason.Death)
        {
            // 检查阶段是否可中断
            if (!IsInterruptibleStage) return;

            // 检查技能配置是否允许中断
            if (ActiveContext?.Config != null && !ActiveContext.Config.IsInterruptible)
            {
                Debug.Log($"[SkillCaster] Skill {_activeSkillId} is not interruptible (config).");
                return;
            }
        }

        LastInterruptReason = reason;

        // 传播中断标志到上下文和 Blackboard（图内节点检测用）
        if (ActiveContext != null)
        {
            ActiveContext.IsInterrupted = true;
            ActiveContext.Blackboard.SetValue(BBKey.IsInterrupted, true);
            ActiveContext.Blackboard.SetValue(BBKey.InterruptReason, reason.ToString());
        }

        // 终止协程（打断正在等待的前腰/后摇/图执行）
        if (_castCoroutine != null)
        {
            StopCoroutine(_castCoroutine);
            _castCoroutine = null;
        }

        var prev = CurrentStage;
        CurrentStage = CastStage.Interrupted;
        OnStageChanged?.Invoke(prev, CurrentStage);
        OnInterrupted?.Invoke(reason);

        Debug.Log($"[SkillCaster] Interrupted skill {_activeSkillId} due to {reason}");

        // 清理上下文
        ActiveContext = null;
        _activeSkillId = 0;
    }

    /// <summary>
    ///     IInterruptible 接口实现。
    /// </summary>
    void IInterruptible.InterruptCast() => Interrupt(InterruptReason.System);

    bool IInterruptible.IsCasting => IsBusy;

    /// <summary>
    ///     添加资源（法力回复等）。
    /// </summary>
    public void AddResource(float amount)
    {
        currentResource = Mathf.Clamp(currentResource + amount, 0f, maxResource);
    }

    /// <summary>
    ///     重置冷却（调试用）。
    /// </summary>
    public void ResetCooldown()
    {
        _lastCastTime = -999f;
    }

    /// <summary>
    ///     强制回到 Idle（调试用）。
    /// </summary>
    public void ForceIdle()
    {
        if (_castCoroutine != null)
        {
            StopCoroutine(_castCoroutine);
            _castCoroutine = null;
        }

        CurrentStage = CastStage.Idle;
        ActiveContext = null;
        _activeSkillId = 0;
    }

    // ---- internal ----

    /// <summary>
    ///     施法管线 —— 权威计时源。
    ///     前腰和后摇等待在此处完成，图内 PreCastNode/PostCastNode 仅负责 VFX。
    ///     吟唱由 ChannelNode 自主管理（不在管线内计时）。
    /// </summary>
    private IEnumerator CastPipeline(SkillConfig config, SkillGraph graph)
    {
        var prev = CurrentStage;

        // ===== Stage: PreCast (前腰) =====
        CurrentStage = CastStage.PreCasting;
        OnStageChanged?.Invoke(prev, CurrentStage);

        var castTime = config.CastTime * castTimeMultiplier;
        if (castTime > 0f)
        {
            ActiveContext.Blackboard.SetValue(BBKey.IsPreCasting, true);
            ActiveContext.Blackboard.SetValue(BBKey.PreCastTime, castTime);

            // 逐帧等待，持续更新进度（供 UI 读条），检查中断
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

        // ===== Stage: Executing (运行技能图) =====
        // PreCastNode（VFX 版）在图开头，播放前腰 VFX
        // ChannelNode 自行管理吟唱 timing
        prev = CurrentStage;
        CurrentStage = CastStage.Executing;
        OnStageChanged?.Invoke(prev, CurrentStage);

        yield return _runner.RunSkill(graph, ActiveContext);

        // 图执行期间被中断（PreCastNode / ChannelNode 内部设了 ctx.IsInterrupted）
        if (ActiveContext.IsInterrupted)
        {
            CleanupAndSetInterrupted();
            yield break;
        }

        // ===== Stage: PostCast (后摇) =====
        prev = CurrentStage;
        CurrentStage = CastStage.PostCasting;
        OnStageChanged?.Invoke(prev, CurrentStage);

        var postCastTime = config.PostCastTime * postCastMultiplier;
        if (postCastTime > 0f)
        {
            ActiveContext.Blackboard.SetValue(BBKey.IsPostCasting, true);
            ActiveContext.Blackboard.SetValue(BBKey.PostCastTime, postCastTime);

            // 后摇不检查中断（已进入收招），但更新进度
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
        var prev = CurrentStage;
        CurrentStage = CastStage.Interrupted;
        OnStageChanged?.Invoke(prev, CurrentStage);
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
