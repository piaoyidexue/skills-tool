using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkillRunner : MonoBehaviour
{
    [HideInInspector] public bool IsDebugMode;
    private readonly Stack<SkillExecutionFrame> _frames = new();

    private bool _pauseRequested;
    private bool _stepRequested;

    public static SkillRunner Instance { get; private set; }
    public SkillContext CurrentContext => _frames.Count > 0 ? _frames.Peek().Context : null;
    public SkillExecutionFrame CurrentFrame => _frames.Count > 0 ? _frames.Peek() : null;

    private void Awake()
    {
        Instance = this;
    }

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
        _frames.Push(frame);

        SkillNode current = graph.GetStartNode();
        if (current == null)
        {
            Debug.LogError($"[SkillRunner] Graph {graph.name} has no StartNode.");
            _frames.Pop();
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
                _pauseRequested = true;
                Debug.Log($"[SkillRunner] Breakpoint hit: {graph.GraphId}/{current.name}");
            }

            while (_pauseRequested && !_stepRequested) yield return null;

            _stepRequested = false;
            yield return current.Execute(ctx);
            ctx.Recorder.Record(graph.GraphId, current.name, "Exit", ctx.Blackboard);

            current.IsExecuting = false;
            current = current.ResolveNextNode(ctx);
        }

        _frames.Pop();
        ctx.ExitGraph();
    }

    public IEnumerator RunNodeChain(SkillNode startNode, SkillContext ctx, SkillGraph owningGraph)
    {
        var current = startNode;
        while (current != null)
        {
            current.IsExecuting = true;
            ctx.Recorder.Record(owningGraph != null ? owningGraph.GraphId : "<Detached>", current.name, "Enter",
                ctx.Blackboard);

            if ((IsDebugMode || ctx.DebugEnabled) && current.HasBreakpoint) _pauseRequested = true;

            while (_pauseRequested && !_stepRequested) yield return null;

            _stepRequested = false;
            yield return current.Execute(ctx);
            ctx.Recorder.Record(owningGraph != null ? owningGraph.GraphId : "<Detached>", current.name, "Exit",
                ctx.Blackboard);
            current.IsExecuting = false;
            current = current.ResolveNextNode(ctx);
        }
    }

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
}