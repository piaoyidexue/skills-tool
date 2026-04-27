using System;
using System.Collections;
using System.Linq;

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

    public override IEnumerator Execute(SkillContext ctx)
    {
        var branchConns = SkillOutConnections
            .Where(c => c.portName != null && c.portName.StartsWith("branches", StringComparison.Ordinal))
            .ToList();

        var branchCount = 0;
        var completed = 0;

        foreach (var conn in branchConns)
        {
            var next = conn.targetNode as SkillNode;
            if (next == null) continue;

            branchCount++;
            SkillRunner.Instance.StartCoroutine(RunBranch(next, ctx, () => completed++));
        }

        ctx.Blackboard.SetValue(BBKey.BranchCount, (float)branchCount);

        while (completed < branchCount) yield return null;
    }

    /// <summary>并行节点使用 "output" 端口返回主后继</summary>
    public override SkillNode ResolveNextNode(SkillContext ctx)
    {
        return GetConnectedNode("output");
    }

    private IEnumerator RunBranch(SkillNode startNode, SkillContext ctx, Action onComplete)
    {
        yield return SkillRunner.Instance.RunNodeChain(startNode, ctx, OwningGraph);
        onComplete?.Invoke();
    }
}