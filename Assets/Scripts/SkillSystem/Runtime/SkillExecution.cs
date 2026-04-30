using System;
using UnityEngine;

/// <summary>
///     单个技能执行实例 —— 替代协程驱动的状态机。
///     由 SkillTickManager 每帧调用 Tick()，内部管理节点切换和子图栈。
///     完全 0 GC：无 IEnumerator 装箱，无 yield 分配。
///
///     架构分离（组合模式）：
///     - CurrentNode (SkillNodeBase)：图导航 + 调试显示（自建框架）
///     - CurrentLogic (ISkillNodeLogic)：逻辑执行（0 框架依赖，通过组合获取）
///     - 导航走 CurrentNode.ResolveNextNode()，执行走 CurrentLogic.Tick/OnEnter/OnExit
/// </summary>
public class SkillExecution
{
    /// <summary>内部子图执行栈（用于 SubGraph 递归）</summary>
    private readonly System.Collections.Generic.Stack<SkillExecutionFrame> _frames = new(8);

    /// <summary>当前正在运行的图节点（用于图导航 + 调试显示）</summary>
    public SkillNodeBase CurrentNode { get; private set; }

    /// <summary>
    ///     当前正在运行的逻辑接口（0 框架依赖，组合获取）。
    ///     通过 CurrentNode.Logic 获取（组合模式），不再强转 ISkillNodeLogic。
    /// </summary>
    public ISkillNodeLogic CurrentLogic { get; private set; }

    /// <summary>当前技能上下文</summary>
    public SkillContext Context { get; private set; }

    /// <summary>当前执行帧（栈顶）</summary>
    public SkillExecutionFrame CurrentFrame => _frames.Count > 0 ? _frames.Peek() : null;

    /// <summary>是否已被标记中断</summary>
    public bool IsInterrupted { get; private set; }

    /// <summary>是否仍在运行中</summary>
    public bool IsRunning { get; private set; }

    /// <summary>完成回调（技能正常结束或中断后触发）</summary>
    public event Action OnCompleted;

    // ---- 调试支持 ----
    private bool _pauseRequested;
    private bool _stepRequested;
    private bool _debugEnabled;

    /// <summary>
    ///     【新增】：触发完成事件（专门提供给外部的 SkillTickManager 调用）
    /// </summary>
    public void NotifyCompleted()
    {
        // 在类的内部调用自身的事件是合法的
        OnCompleted?.Invoke();
    }

    // ReSharper disable Unity.PerformanceAnalysis
    /// <summary>
    ///     初始化执行实例。每次从对象池取出时调用。
    /// </summary>
    public void Initialize(SkillGraphAsset graph, SkillContext context)
    {
        Context = context;
        _debugEnabled = context?.DebugEnabled ?? false;

        if (graph == null || context == null)
        {
            MarkInterrupted();
            return;
        }

        if (!context.TryEnterGraph(graph.GraphId))
        {
            Debug.LogError(
                $"[SkillExecution] Blocked subgraph execution for {graph.GraphId}. Check recursion or depth limit.");
            MarkInterrupted();
            return;
        }

        var frame = new SkillExecutionFrame(graph, context);
        _frames.Push(frame);

        CurrentNode = graph.GetStartNode();
        if (CurrentNode == null)
        {
            Debug.LogError($"[SkillExecution] Graph {graph.name} has no StartNode.");
            _frames.Pop();
            context.ExitGraph();
            MarkInterrupted();
            return;
        }

        CurrentLogic = CurrentNode.Logic;
        IsRunning = true;
        IsInterrupted = false;
        CurrentLogic.OnEnter(context);
    }

    /// <summary>
    ///     核心 Tick 方法 —— 每帧由 SkillTickManager 调用。
    ///     驱动当前节点的状态机，处理节点切换和子图进出。
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (!IsRunning || CurrentNode == null || CurrentLogic == null || IsInterrupted) return;

        // 断点暂停
        if (_debugEnabled && _pauseRequested && !_stepRequested) return;

        _stepRequested = false;

        // 检查上下文级中断标志
        if (Context != null && Context.IsInterrupted)
        {
            MarkInterrupted();
            return;
        }

        // 驱动当前节点逻辑（通过 ISkillNodeLogic 接口，0 框架依赖）
        var result = CurrentLogic.Tick(Context, deltaTime);

        switch (result)
        {
            case NodeTickResult.Success:
                OnNodeComplete();
                break;

            case NodeTickResult.Failure:
                OnNodeFailed();
                break;

            case NodeTickResult.Running:
                // 节点仍在执行中，下一帧继续
                break;
        }
    }

    /// <summary>
    ///     当前节点成功完成，推进到下一个节点。
    /// </summary>
    private void OnNodeComplete()
    {
        if (CurrentNode == null || CurrentLogic == null) return;

        CurrentLogic.OnExit(Context);
        // 图导航仍走 ResolveNextNode()（图结构职责）
        var nextNode = CurrentNode.ResolveNextNode(Context);

        if (nextNode != null)
        {
            // 推进到下一节点（组合模式：Logic 通过节点获取）
            CurrentNode = nextNode;
            CurrentLogic = nextNode.Logic;
            CurrentLogic.OnEnter(Context);
        }
        else
        {
            // 当前子图执行完毕
            OnGraphComplete();
        }
    }

    /// <summary>
    ///     当前节点执行失败，终止执行。
    /// </summary>
    private void OnNodeFailed()
    {
        if (CurrentLogic != null)
        {
            CurrentLogic.OnExit(Context);
        }

        MarkInterrupted();
    }

    /// <summary>
    ///     当前图执行完毕，弹出子图栈。
    /// </summary>
    private void OnGraphComplete()
    {
        if (_frames.Count == 0) return;

        _frames.Pop();
        Context.ExitGraph();

        if (_frames.Count > 0)
        {
            // 回到上级图的调用点（子图节点），继续执行
            var callerFrame = _frames.Peek();
            CurrentNode = callerFrame.CurrentNode?.ResolveNextNode(Context) ?? null;
            if (CurrentNode != null)
            {
                CurrentLogic = CurrentNode.Logic;
                CurrentLogic.OnEnter(Context);
            }
            else
            {
                // 上级图也结束了
                OnGraphComplete();
            }
        }
        else
        {
            // 顶层图执行完毕
            CurrentNode = null;
            CurrentLogic = null;
            IsRunning = false;
        }
    }

    /// <summary>
    ///     标记中断（安全，可被 Tick 循环内调用，会在帧末清理）。
    /// </summary>
    public void MarkInterrupted()
    {
        if (!IsRunning) return;

        IsInterrupted = true;
        IsRunning = false;

        if (Context != null)
        {
            Context.IsInterrupted = true;
        }

        // 清理当前节点
        if (CurrentLogic != null)
        {
            CurrentLogic.OnExit(Context);
            CurrentNode = null;
            CurrentLogic = null;
        }
    }

    /// <summary>
    ///     强制跳转到指定节点（用于条件分支等场景）。
    /// </summary>
    public void SkipTo(SkillNodeBase targetNode)
    {
        if (CurrentLogic != null)
        {
            CurrentLogic.OnExit(Context);
        }

        CurrentNode = targetNode;
        CurrentLogic = targetNode?.Logic;
        if (CurrentLogic != null)
        {
            CurrentLogic.OnEnter(Context);
        }
    }

    // ---- 调试控制 ----
    public void Pause()
    {
        _pauseRequested = true;
    }

    public void Step()
    {
        _pauseRequested = false;
        _stepRequested = true;
    }

    public void Continue()
    {
        _pauseRequested = false;
        _stepRequested = false;
    }

    /// <summary>
    ///     重置实例以供对象池复用。
    /// </summary>
    public void Reset()
    {
        _frames.Clear();
        CurrentNode = null;
        CurrentLogic = null;
        Context = null;
        IsInterrupted = false;
        IsRunning = false;
        _pauseRequested = false;
        _stepRequested = false;
        _debugEnabled = false;
        OnCompleted = null;
    }
}