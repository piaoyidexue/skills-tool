using System.Linq;
using System;

/// <summary>
///     并行节点 —— 同时执行所有分支，全部完成后从 "output" 端口继续。
///     使用命名端口系统：以 "branches" 开头的端口为并行分支，"output" 为主输出。
/// </summary>
public class ParallelNode : SkillNode
{
    /// <summary>分支数量提示字段（仅供编辑器参考，运行时通过连接数确定）</summary>
    public int branchHint = 2;

    // ---- 多端口配置 ----
    public override int maxOutConnections => -1;

    public ParallelNode()
    {
        SetPortNames(new[] { "input" }, new[] { "output" });
    }

    [System.NonSerialized] private System.Collections.Generic.List<BranchState> _branches = new();

    public override void OnEnter(SkillContext ctx)
    {
        _branches.Clear();
        var branchConns = SkillOutConnections
            .Where(c => c.portName != null && c.portName.StartsWith("branches", StringComparison.Ordinal))
            .ToList();

        ctx.Blackboard.SetValue(BBKey.BranchCount, (float)branchConns.Count);

        foreach (var conn in branchConns)
        {
            var next = conn.targetNode as SkillNode;
            if (next == null) continue;

            var state = new BranchState { Node = next, Status = NodeTickResult.Running };
            next.OnEnter(ctx);
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
                branch.Status = branch.Node.Tick(ctx, deltaTime);
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
                branch.Node.OnExit(ctx);
        }
        _branches.Clear();
    }

    private class BranchState
    {
        public SkillNode Node;
        public NodeTickResult Status;
    }
}
