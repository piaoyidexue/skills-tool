using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ============================================================
//  SkillData —— 编译后的技能数据资产
//  由 SkillBuilder 从 SkillGraphAsset 编译生成，是运行时唯一执行依据。
//  可被序列化为 ScriptableObject 独立资产，支持版本控制与离线校验。
// ============================================================

/// <summary>
///     编译后的技能数据资产 —— 纯数据结构。
///     技能运行时的"唯一真相来源"，禁止在运行时修改。
/// </summary>
[CreateAssetMenu(fileName = "NewSkillData", menuName = "Skill System/Skill Data")]
public class SkillData : ScriptableObject
{
    // ---- 基础标识 ----
    [Tooltip("技能唯一ID（对应 SkillConfig.skill_id）")]
    public int SkillId;

    [Tooltip("技能显示名称")]
    public string SkillName;

    // ---- 源引用 ----
    [Tooltip("编译来源的 SkillGraphAsset（用于编辑器回退和重新编译）")]
    public SkillGraphAsset SourceGraph;

    [Tooltip("源图的版本哈希（用于检测源图是否变更）")]
    public string SourceGraphHash;

    // ---- 编译元数据 ----
    [Tooltip("编译时间戳")]
    public string CompileTimestamp;

    [Tooltip("编译模式")]
    public SkillCompileMode CompileMode;

    [Tooltip("是否包含需要运行时解释的动态节点")]
    public bool HasDynamicNodes;

    // ---- 时间轴数据 ----
    [Tooltip("时间轴步骤列表（已按 TriggerTime 升序排序）")]
    public List<SkillStep> Steps = new();

    // ---- 总时长 ----
    [Tooltip("技能总时长（秒，最后一个步骤的触发时间）")]
    public float TotalDuration;

    // ---- 前摇/后摇（与 SkillCaster 管线对齐）----
    [Tooltip("前摇时间（秒）")]
    public float PreCastTime;

    [Tooltip("后摇时间（秒）")]
    public float PostCastTime;

    [Tooltip("是否可打断")]
    public bool IsInterruptible = true;

    // ============================================================
    //  运行时查询 API
    // ============================================================

    /// <summary>获取指定时间之前的所有步骤（用于 TimelineSkillRunner 推进）</summary>
    public List<SkillStep> GetStepsBeforeTime(float time)
    {
        var result = new List<SkillStep>();
        foreach (var step in Steps)
        {
            if (step.TriggerTime <= time) result.Add(step);
        }
        return result;
    }

    /// <summary>获取下一个未执行的步骤索引</summary>
    public int GetNextStepIndex(float currentTime, int lastExecutedIndex)
    {
        for (int i = lastExecutedIndex + 1; i < Steps.Count; i++)
        {
            if (Steps[i].TriggerTime <= currentTime) return i;
        }
        return -1;
    }

    /// <summary>检查本技能是否可完全时间轴化（无动态节点）</summary>
    public bool IsFullyTimelineDriven => !HasDynamicNodes && CompileMode == SkillCompileMode.FullTimeline;

    /// <summary>验证数据完整性</summary>
    public bool Validate(out string error)
    {
        if (SkillId <= 0)
        {
            error = "SkillId 无效";
            return false;
        }

        if (Steps == null || Steps.Count == 0)
        {
            error = "Steps 为空";
            return false;
        }

        // 检查时间轴是否有序
        for (int i = 1; i < Steps.Count; i++)
        {
            if (Steps[i].TriggerTime < Steps[i - 1].TriggerTime)
            {
                error = $"Steps 时间戳无序: step[{i - 1}].TriggerTime={Steps[i - 1].TriggerTime} > step[{i}].TriggerTime={Steps[i].TriggerTime}";
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary>生成调试摘要</summary>
    public string GetDebugSummary()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== SkillData: {SkillName} (ID={SkillId}) ===");
        sb.AppendLine($"CompileMode: {CompileMode}, HasDynamicNodes: {HasDynamicNodes}");
        sb.AppendLine($"TotalDuration: {TotalDuration:F2}s, Steps: {Steps.Count}");
        sb.AppendLine($"PreCast: {PreCastTime:F2}s, PostCast: {PostCastTime:F2}s");
        sb.AppendLine("Timeline:");
        foreach (var step in Steps)
        {
            var typeList = string.Join(", ", step.Effects.Select(e => e.EffectType.ToString()));
            sb.AppendLine($"  [{step.TriggerTime:F2}s] {(step.IsDynamic ? "[DYNAMIC] " : "")}{typeList}");
        }
        return sb.ToString();
    }

    private void OnValidate()
    {
        // 自动排序
        if (Steps != null && Steps.Count > 1)
        {
            Steps.Sort((a, b) => a.TriggerTime.CompareTo(b.TriggerTime));
        }

        // 自动计算总时长
        if (Steps != null && Steps.Count > 0)
        {
            TotalDuration = Steps[Steps.Count - 1].TriggerTime;
        }
    }
}

/// <summary>
///     技能编译模式。
/// </summary>
public enum SkillCompileMode
{
    /// <summary>完整时间轴编译 —— 所有节点已转换为时间轴步骤</summary>
    FullTimeline,

    /// <summary>混合模式 —— 大部分为时间轴，部分动态节点需要运行时回退</summary>
    Hybrid,

    /// <summary>回退模式 —— 无法编译为时间轴，完全依赖 Tick 解释器</summary>
    FallbackOnly
}
