using System.Linq;
using System;
using UnityEngine;

/// <summary>
///     并行节点 —— 同时执行所有分支，全部完成后从 "output" 端口继续。
///     使用命名端口系统：以 "branches" 开头的端口为并行分支，"output" 为主输出。
/// </summary>
[CreateAssetMenu(fileName = "ParallelNode", menuName = "Skill System/Nodes/Flow/Parallel")]
public class ParallelNode : SkillNodeBase
{
    /// <summary>分支数量提示字段（仅供编辑器参考，运行时通过边数确定）</summary>
    public int branchHint = 2;

    protected override void OnEnable()
    {
        base.OnEnable();
        SetPortNames(new[] { "input" }, new[] { "output" });
    }

    [System.NonSerialized] private System.Collections.Generic.List<BranchState> _branches = new();

    public override void OnEnter(SkillContext ctx)
    {
        _branches.Clear();
        var branchEdges = GetOutputEdges()
            .Where(e => e.SourcePort != null && e.SourcePort.StartsWith("branches", StringComparison.Ordinal))
            .ToList();

        ctx.Blackboard.SetValue(BBKey.BranchCount, (float)branchEdges.Count);

        foreach (var edge in branchEdges)
        {
            var next = OwningGraph.FindNodeByGuid(edge.TargetNodeGuid) as SkillNodeBase;
            if (next == null) continue;

            next.OnEnter(ctx);
            var state = new BranchState { Logic = next.Logic, Status = NodeTickResult.Running };
            _branches.Add(state);
        }
    }

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        var allDone = true;

        for (var i = 0; i < _branches.Count; i++)
        {
            var branch = _branches[i];
            if (branch.Status == NodeTickResult.Running)
            {
                branch.Status = branch.Logic.Tick(ctx, deltaTime);
                if (branch.Status == NodeTickResult.Running)
                    allDone = false;
            }
        }

        return allDone ? NodeTickResult.Success : NodeTickResult.Running;
    }

    public override void OnExit(SkillContext ctx)
    {
        foreach (var branch in _branches)
        {
            if (branch.Status == NodeTickResult.Running)
                branch.Logic.OnExit(ctx);
        }
        _branches.Clear();
    }

    private class BranchState
    {
        /// <summary>分支节点的逻辑接口（0 框架依赖，执行引擎使用）</summary>
        public ISkillNodeLogic Logic;
        public NodeTickResult Status;
    }
}
