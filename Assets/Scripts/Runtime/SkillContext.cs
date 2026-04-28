using System.Collections.Generic;
using UnityEngine;

public class SkillContext
{
    private readonly Stack<string> _graphStack = new();

    public SkillContext()
    {
        Blackboard = new Blackboard();
        Recorder = new DebugRecorder();
    }

    public SkillContext(int skillID, Transform caster, Transform initialTarget)
    {
        SkillID = skillID;
        Config = ConfigLoader.GetSkillConfig(skillID);
        Caster = caster;
        Target = initialTarget;
        Blackboard = new Blackboard();
        Recorder = new DebugRecorder();
    }

    public int SkillID { get; private set; }
    public SkillConfig Config { get; private set; }
    public Transform Caster { get; set; }
    public Transform Target { get; set; }

    /// <summary>施法者组件引用（由 SkillCaster 注入）</summary>
    public SkillCaster CasterComponent { get; set; }

    /// <summary>是否已被中断，节点可在 Execute 内检查此标志提前退出</summary>
    public bool IsInterrupted { get; set; }

    /// <summary>当前所处释放阶段</summary>
    public CastStage CurrentCastStage
    {
        get
        {
            if (CasterComponent != null) return CasterComponent.CurrentStage;
            return IsInterrupted ? CastStage.Interrupted : CastStage.Executing;
        }
    }

    public Blackboard Blackboard { get; set; }
    public DebugRecorder Recorder { get; private set; }
    public bool DebugEnabled { get; set; }
    public int MaxSubgraphDepth { get; set; } = 3;

    public int ActiveGraphDepth => _graphStack.Count;

    public bool TryEnterGraph(string graphName)
    {
        if (_graphStack.Count >= MaxSubgraphDepth) return false;

        if (_graphStack.Contains(graphName)) return false;

        _graphStack.Push(graphName);
        Blackboard.SetValue(BBKey.CurrentGraph, graphName);
        return true;
    }

    public void ExitGraph()
    {
        if (_graphStack.Count == 0)
        {
            Blackboard.SetValue(BBKey.CurrentGraph, string.Empty);
            return;
        }

        _graphStack.Pop();
        Blackboard.SetValue(BBKey.CurrentGraph, _graphStack.Count > 0 ? _graphStack.Peek() : string.Empty);
    }
}
