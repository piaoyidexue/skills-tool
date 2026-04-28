using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     技能执行器 —— 兼容协程与 Tick 系统的桥接层。
///     新代码应使用 SkillTickManager.Register() 进行 Tick 驱动执行，
///     旧协程兼容 API 保留用于过渡期。
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
    //  新接口：Tick 驱动（0 GC）
    // ============================================================

    /// <summary>
    ///     启动技能图执行（Tick 驱动模式）。
    ///     注册到全局 SkillTickManager，每帧自动推进。
    /// </summary>
    public SkillExecution RunSkillTick(SkillGraph graph, SkillContext ctx)
    {
        if (_tickManager == null)
        {
            Debug.LogError("[SkillRunner] SkillTickManager not found. Falling back to coroutine.");
            StartCoroutine(RunSkill(graph, ctx));
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

    // ============================================================
    //  旧接口：协程驱动（向后兼容）
    // ============================================================

    /// <summary>
    ///     启动技能图执行（协程驱动模式，向后兼容）。
    /// </summary>
    public IEnumerator RunSkill(SkillGraph graph, SkillContext ctx)
    {
        if (graph == null || ctx == null) yield break;

        if (!ctx.TryEnterGraph(graph.GraphId))
        {
            Debug.LogError(
                $"[SkillRunner] Blocked subgraph execution for {graph.GraphId}. Check recursion or depth limit.");
            yield break;
        }

        var frame = new SkillExecutionFrame(graph, ctx);
        var frames = new Stack<SkillExecutionFrame>();
        frames.Push(frame);

        CurrentContext = ctx;

        SkillNode current = graph.GetStartNode();
        if (current == null)
        {
            Debug.LogError($"[SkillRunner] Graph {graph.name} has no StartNode.");
            frames.Pop();
            ctx.ExitGraph();
            yield break;
        }

        while (current != null)
        {
            frame.CurrentNode = current;
            current.IsExecuting = true;
            ctx.Recorder.Record(graph.GraphId, current.name, "Enter", ctx.Blackboard);

            if ((IsDebugMode || ctx.DebugEnabled) && current.HasBreakpoint)
            {
                Debug.Log($"[SkillRunner] Breakpoint hit: {graph.GraphId}/{current.name}");
                yield return new WaitWhile(() => !IsDebugMode && !ctx.DebugEnabled);
            }

            yield return current.Execute(ctx);
            ctx.Recorder.Record(graph.GraphId, current.name, "Exit", ctx.Blackboard);

            current.IsExecuting = false;
            current = current.ResolveNextNode(ctx);
        }

        frames.Pop();
        ctx.ExitGraph();
        CurrentContext = null;
    }

    /// <summary>
    ///     执行部分节点链（协程模式，向后兼容）。
    /// </summary>
    public IEnumerator RunNodeChain(SkillNode startNode, SkillContext ctx, SkillGraph owningGraph)
    {
        var current = startNode;
        while (current != null)
        {
            current.IsExecuting = true;
            ctx.Recorder.Record(owningGraph != null ? owningGraph.GraphId : "<Detached>", current.name, "Enter",
                ctx.Blackboard);

            yield return current.Execute(ctx);
            ctx.Recorder.Record(owningGraph != null ? owningGraph.GraphId : "<Detached>", current.name, "Exit",
                ctx.Blackboard);
            current.IsExecuting = false;
            current = current.ResolveNextNode(ctx);
        }
    }

    // ---- 调试 ----
    public void Pause() => CurrentExecution?.Pause();
    public void Step() => CurrentExecution?.Step();
    public void Continue() => CurrentExecution?.Continue();
}
