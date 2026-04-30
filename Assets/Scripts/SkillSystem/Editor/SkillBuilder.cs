using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// ============================================================
//  SkillBuilder —— Graph → SkillData 编译器
//  职责：遍历 SkillGraphAsset，将可编译节点转换为时间轴数据。
//  不可编译节点（Condition/RollChance/AnimEventWait）标记为动态点。
// ============================================================

public static class SkillBuilder
{
    // ---- 编译结果 ----
    public class BuildResult
    {
        public bool Success;
        public SkillData Data;
        public string Error;
        public List<string> Warnings = new();
        public int CompiledNodeCount;
        public int DynamicNodeCount;
    }

    // ============================================================
    //  主入口
    // ============================================================

    /// <summary>
    ///     编译 SkillGraphAsset 为 SkillData。
    /// </summary>
    public static BuildResult Build(SkillGraphAsset graph, int skillId = 0, string skillName = "")
    {
        if (graph == null)
            return new BuildResult { Success = false, Error = "Graph is null" };

        if (!graph.HasSingleStartNode())
            return new BuildResult { Success = false, Error = "Graph must have exactly one StartNode" };

        var result = new BuildResult();
        var data = ScriptableObject.CreateInstance<SkillData>();
        data.SkillId = skillId;
        data.SkillName = string.IsNullOrEmpty(skillName) ? graph.name : skillName;
        data.SourceGraph = graph;
        data.SourceGraphHash = ComputeGraphHash(graph);
        data.CompileTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // 遍历图并编译
        var compileState = new CompileState();
        var startNode = graph.GetStartNode();
        var compileCtx = skillId > 0 ? new SkillContext(skillId, null, null) : null;
        TraverseAndCompile(startNode, compileState, result, compileCtx);

        // 填充数据
        data.Steps = compileState.Steps;
        data.HasDynamicNodes = compileState.HasDynamicNodes;
        data.CompileMode = DetermineCompileMode(compileState, result);
        data.PreCastTime = compileState.PreCastTime;
        data.PostCastTime = compileState.PostCastTime;
        data.TotalDuration = compileState.CurrentTime;
        data.IsInterruptible = true; // 默认值，可从配置读取

        // 最终验证
        if (!data.Validate(out var validateError))
        {
            result.Success = false;
            result.Error = validateError;
            return result;
        }

        result.Success = true;
        result.Data = data;
        result.CompiledNodeCount = compileState.CompiledNodeCount;
        result.DynamicNodeCount = compileState.DynamicNodeCount;
        return result;
    }

    /// <summary>
    ///     编译并保存为独立资产文件。
    /// </summary>
    public static BuildResult BuildAndSave(SkillGraphAsset graph, string outputPath, int skillId = 0, string skillName = "")
    {
        var result = Build(graph, skillId, skillName);
        if (!result.Success) return result;

#if UNITY_EDITOR
        // 确保目录存在
        var dir = System.IO.Path.GetDirectoryName(outputPath)?.Replace("\\", "/");
        if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
        {
            var parent = System.IO.Path.GetDirectoryName(dir)?.Replace("\\", "/") ?? "Assets";
            var name = System.IO.Path.GetFileName(dir);
            AssetDatabase.CreateFolder(parent, name);
        }

        // 检查是否已有同名资产，更新或创建
        var existing = AssetDatabase.LoadAssetAtPath<SkillData>(outputPath);
        if (existing != null)
        {
            // 更新现有资产
            EditorUtility.CopySerialized(result.Data, existing);
            EditorUtility.SetDirty(existing);
            result.Warnings.Add($"Updated existing asset: {outputPath}");
        }
        else
        {
            AssetDatabase.CreateAsset(result.Data, outputPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        result.Warnings.Add($"Saved to: {outputPath}");
#endif
        return result;
    }

    // ============================================================
    //  遍历与编译逻辑
    // ============================================================

    private class CompileState
    {
        public float CurrentTime = 0f;
        public List<SkillStep> Steps = new();
        public bool HasDynamicNodes = false;
        public int CompiledNodeCount = 0;
        public int DynamicNodeCount = 0;
        public float PreCastTime = 0f;
        public float PostCastTime = 0f;

        // 当前正在构建的步骤（同一时间点的多个效果合并）
        public SkillStep PendingStep = null;

        /// <summary>确保 PendingStep 存在</summary>
        public SkillStep EnsurePendingStep(float time, string stepId)
        {
            if (PendingStep == null || Mathf.Abs(PendingStep.TriggerTime - time) > 0.001f)
            {
                FlushPendingStep();
                PendingStep = new SkillStep(time, stepId);
            }
            return PendingStep;
        }

        /// <summary>将 PendingStep 写入 Steps</summary>
        public void FlushPendingStep()
        {
            if (PendingStep != null && PendingStep.Effects.Count > 0)
            {
                Steps.Add(PendingStep);
            }
            PendingStep = null;
        }
    }

    private static void TraverseAndCompile(SkillNodeBase node, CompileState state, BuildResult result, SkillContext compileCtx)
    {
        if (node == null) return;

        // 处理节点本身
        ProcessNode(node, state, result, compileCtx);

        // 获取下一个节点（简单路径，不考虑分支）
        // 对于线性图（无分支），ResolveNextNode 返回单一节点
        // 对于分支图，这里只走默认路径，分支逻辑留给运行时
        var nextNodes = GetNextNodes(node);

        if (nextNodes.Count == 0)
        {
            // 路径结束
            state.FlushPendingStep();
            return;
        }

        if (nextNodes.Count == 1)
        {
            // 单一路径，继续遍历
            TraverseAndCompile(nextNodes[0], state, result, compileCtx);
            return;
        }

        // 多分支（ConditionNode 等）—— 标记为动态
        state.HasDynamicNodes = true;
        state.DynamicNodeCount++;
        result.Warnings.Add($"Node '{node.name}' has multiple outputs —— marking as dynamic branch point");

        // 创建动态步骤标记
        state.FlushPendingStep();
        var dynamicStep = new SkillStep(state.CurrentTime, $"dynamic_{node.NodeGuid}")
        {
            IsDynamic = true,
            SourceNodeGuid = node.NodeGuid,
            Description = $"Dynamic branch from {node.name}"
        };
        state.Steps.Add(dynamicStep);

        // 尝试编译每条分支（用于离线分析，运行时只走一条）
        foreach (var branchNode in nextNodes)
        {
            // 创建分支快照用于分析
            var branchState = new CompileState
            {
                CurrentTime = state.CurrentTime,
                HasDynamicNodes = state.HasDynamicNodes,
                PreCastTime = state.PreCastTime,
                PostCastTime = state.PostCastTime
            };
            TraverseAndCompile(branchNode, branchState, result, compileCtx);
        }
    }

    private static void ProcessNode(SkillNodeBase node, CompileState state, BuildResult result, SkillContext compileCtx)
    {
        // 根据节点类型处理
        switch (node)
        {
            case StartNode _:
                // StartNode 不产生效果，只标记开始
                break;

            case EndNode _:
                // EndNode 不产生效果，结束当前路径
                state.FlushPendingStep();
                break;

            case DelayNode delayNode:
                // DelayNode 推进时间游标
                state.FlushPendingStep();
                var delay = delayNode.GetTimelineDuration();
                state.CurrentTime += delay;
                break;

            case PreCastNode preCast:
                state.PreCastTime = preCast.GetTimelineDuration();
                break;

            case PostCastNode postCast:
                state.PostCastTime = postCast.GetTimelineDuration();
                break;

            default:
                // 通用处理
                if (node.CanCompile)
                {
                    var effects = node.Compile(compileCtx);
                    if (effects != null && effects.Count > 0)
                    {
                        var step = state.EnsurePendingStep(state.CurrentTime, node.NodeGuid);
                        foreach (var effect in effects)
                        {
                            step.AddEffect(effect);
                        }
                        state.CompiledNodeCount++;
                    }
                }
                else
                {
                    // 不可编译节点 → 动态标记
                    state.HasDynamicNodes = true;
                    state.DynamicNodeCount++;
                    state.FlushPendingStep();

                    var dynamicStep = new SkillStep(state.CurrentTime, $"dynamic_{node.NodeGuid}")
                    {
                        IsDynamic = true,
                        SourceNodeGuid = node.NodeGuid,
                        Description = $"Dynamic node: {node.name}"
                    };
                    state.Steps.Add(dynamicStep);

                    result.Warnings.Add($"Node '{node.name}' is not compilable —— dynamic fallback");
                }
                break;
        }

        // 推进时间（如果节点声明了时间贡献）
        var duration = node.GetTimelineDuration();
        if (duration > 0f && !(node is DelayNode))
        {
            state.CurrentTime += duration;
        }
    }

    // ============================================================
    //  图导航辅助
    // ============================================================

    private static List<SkillNodeBase> GetNextNodes(SkillNodeBase node)
    {
        var result = new List<SkillNodeBase>();
        if (node?.OwningGraph == null) return result;

        // 获取所有输出边对应的目标节点
        var edges = node.GetOutputEdges();
        foreach (var edge in edges)
        {
            var target = node.OwningGraph.FindNodeByGuid(edge.TargetNodeGuid);
            if (target != null) result.Add(target);
        }

        return result;
    }

    // ============================================================
    //  编译模式判定
    // ============================================================

    private static SkillCompileMode DetermineCompileMode(CompileState state, BuildResult result)
    {
        if (state.DynamicNodeCount == 0 && state.CompiledNodeCount > 0)
            return SkillCompileMode.FullTimeline;

        if (state.DynamicNodeCount > 0 && state.CompiledNodeCount > 0)
            return SkillCompileMode.Hybrid;

        if (state.CompiledNodeCount == 0)
            return SkillCompileMode.FallbackOnly;

        return SkillCompileMode.Hybrid;
    }

    // ============================================================
    //  图哈希（用于检测变更）
    // ============================================================

    private static string ComputeGraphHash(SkillGraphAsset graph)
    {
        if (graph == null) return string.Empty;

        var hash = new System.Text.StringBuilder();
        hash.Append(graph.GraphId);
        hash.Append("|");
        hash.Append(graph.Nodes.Count);
        hash.Append("|");
        hash.Append(graph.Edges.Count);

        foreach (var node in graph.Nodes)
        {
            hash.Append(node.NodeGuid);
            hash.Append(node.name);
        }

        foreach (var edge in graph.Edges)
        {
            hash.Append(edge.SourceNodeGuid);
            hash.Append(edge.TargetNodeGuid);
        }

        // 简化哈希
        return hash.ToString().GetHashCode().ToString("X8");
    }
}

// ============================================================
//  编辑器菜单
// ============================================================

#if UNITY_EDITOR
public static class SkillBuilderMenu
{
    [MenuItem("Assets/Skill System/Compile Selected Graph", false, 100)]
    private static void CompileSelectedGraph()
    {
        var selected = Selection.activeObject as SkillGraphAsset;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Skill Builder", "Please select a SkillGraphAsset first.", "OK");
            return;
        }

        var outputPath = $"Assets/Resources/SkillData/{selected.name}_Data.asset";
        var result = SkillBuilder.BuildAndSave(selected, outputPath);

        if (result.Success)
        {
            var msg = $"Compiled successfully!\n\nMode: {result.Data.CompileMode}\n" +
                      $"Steps: {result.Data.Steps.Count}\n" +
                      $"Compiled Nodes: {result.CompiledNodeCount}\n" +
                      $"Dynamic Nodes: {result.DynamicNodeCount}\n\n" +
                      $"Saved to: {outputPath}";
            if (result.Warnings.Count > 0)
                msg += "\n\nWarnings:\n" + string.Join("\n", result.Warnings);
            EditorUtility.DisplayDialog("Skill Builder", msg, "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Skill Builder", $"Compile failed:\n{result.Error}", "OK");
        }
    }

    [MenuItem("Assets/Skill System/Compile Selected Graph", true)]
    private static bool CompileSelectedGraphValidation()
    {
        return Selection.activeObject is SkillGraphAsset;
    }
}
#endif
