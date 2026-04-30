using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  SkillStep —— 时间轴上的单个执行点
//  在 triggerTime 时刻触发其包含的所有 SkillEffectData
// ============================================================

/// <summary>
///     技能时间轴步骤 —— 纯数据结构。
///     由 SkillBuilder 从 Graph 编译生成，TimelineSkillRunner 按 triggerTime 驱动。
/// </summary>
[Serializable]
public class SkillStep
{
    /// <summary>触发时间点（秒，相对于技能开始）</summary>
    [Tooltip("触发时间点（秒，相对于技能开始）")]
    public float TriggerTime;

    /// <summary>本步骤包含的效果列表（同一时间点可并行多个效果）</summary>
    [Tooltip("本步骤包含的效果列表")]
    public List<SkillEffectData> Effects = new();

    /// <summary>步骤标识（用于调试和断点）</summary>
    [Tooltip("步骤标识（用于调试）")]
    public string StepId;

    /// <summary>步骤描述（调试用途）</summary>
    public string Description;

    /// <summary>
    ///     是否为动态步骤（包含运行时才能确定的分支/概率/事件等待）。
    ///     true 时 TimelineSkillRunner 会在此暂停并回退到 Tick 解释器。
    /// </summary>
    [Tooltip("是否为动态步骤（需要运行时解释）")]
    public bool IsDynamic;

    /// <summary>
    ///     动态步骤对应的原始节点 GUID（用于回退时定位）。
    /// </summary>
    public string SourceNodeGuid;

    // ============================================================
    //  便捷方法
    // ============================================================

    public SkillStep() { }

    public SkillStep(float triggerTime, string stepId = "")
    {
        TriggerTime = triggerTime;
        StepId = stepId;
    }

    /// <summary>添加一个效果到本步骤</summary>
    public void AddEffect(SkillEffectData effect)
    {
        Effects.Add(effect);
    }

    /// <summary>检查本步骤是否包含指定类型的效果</summary>
    public bool HasEffectOfType(SkillEffectType type)
    {
        foreach (var e in Effects)
            if (e.EffectType == type) return true;
        return false;
    }

    /// <summary>获取本步骤中指定类型的所有效果</summary>
    public List<SkillEffectData> GetEffectsOfType(SkillEffectType type)
    {
        var result = new List<SkillEffectData>();
        foreach (var e in Effects)
            if (e.EffectType == type) result.Add(e);
        return result;
    }

    public override string ToString() =>
        $"SkillStep[t={TriggerTime:F2}s, effects={Effects.Count}, id={StepId}]";
}
