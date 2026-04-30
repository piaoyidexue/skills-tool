using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  效果规格 (EffectSpec)
//  Context + Data 结合体，真正在系统中流转的"工单"。
//  包含可变字段用于在流转过程中被各种系统修改。
// ============================================================

/// <summary>
///     效果规格 —— Context + Data 结合体，是真正在系统中流转的"工单"。
///     不可变部分由 Context 提供，可变部分（数值）在管线中被修改。
/// </summary>
[Serializable]
public class EffectSpec
{
    // ---- 不可变部分（创建后不变） ----
    public EffectContext Context;
    public GameplayEffectData Data;

    // ---- 可变数值（管线中修改） ----
    public float CalculatedDamage;
    public float CalculatedHealing;

    /// <summary>是否暴击</summary>
    public bool IsCriticalHit;

    /// <summary>最终应用标签（可被 Modifier 添加）</summary>
    public List<string> AppliedTags;

    /// <summary>是否已处理（防止重复应用）</summary>
    public bool IsProcessed;

    /// <summary>处理结果描述</summary>
    public string ResultDescription;

    // ---- 对象池支持 ----
    private static readonly Queue<EffectSpec> Pool = new();

    private EffectSpec() { }

    /// <summary>
    ///     从对象池获取或创建新的 EffectSpec。
    /// </summary>
    public static EffectSpec Obtain(EffectContext context, GameplayEffectData data)
    {
        if (!Pool.TryDequeue(out var spec))
            spec = new EffectSpec();

        spec.Context = context;
        spec.Data = data;
        spec.CalculatedDamage = data.BaseDamage;
        spec.CalculatedHealing = data.BaseHealing;
        spec.IsCriticalHit = false;
        spec.AppliedTags = null;
        spec.IsProcessed = false;
        spec.ResultDescription = string.Empty;

        // 标签初始化
        if (data.GrantedTags != null && data.GrantedTags.Count > 0)
        {
            spec.AppliedTags = new List<string>(data.GrantedTags);
        }

        // 应用技能等级缩放
        if (context.AbilityLevel > 1)
        {
            var levelBonus = (context.AbilityLevel - 1) * data.ScalingFactor;
            spec.CalculatedDamage *= (1f + levelBonus);
            spec.CalculatedHealing *= (1f + levelBonus);
        }

        return spec;
    }

    /// <summary>
    ///     释放回对象池（避免 GC）。
    /// </summary>
    public void Release()
    {
        Context.SourceTags?.Clear();
        AppliedTags?.Clear();
        Pool.Enqueue(this);
    }

    /// <summary>
    ///     添加应用标签。
    /// </summary>
    public void AddAppliedTag(string tag)
    {
        AppliedTags ??= new List<string>();
        if (!AppliedTags.Contains(tag)) AppliedTags.Add(tag);
    }

    /// <summary>
    ///     添加多个应用标签。
    /// </summary>
    public void AddAppliedTags(IEnumerable<string> tags)
    {
        foreach (var tag in tags) AddAppliedTag(tag);
    }

    /// <summary>
    ///     检查是否包含指定应用标签（支持层级匹配）。
    /// </summary>
    public bool HasAppliedTag(string tag)
    {
        if (AppliedTags == null) return false;
        foreach (var t in AppliedTags)
            if (string.Equals(t, tag, StringComparison.OrdinalIgnoreCase) ||
                t.StartsWith(tag + ".", StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    /// <summary>
    ///     获取总伤害值（含暴击倍率）。
    /// </summary>
    public float GetTotalDamage()
    {
        return IsCriticalHit ? CalculatedDamage * Data.CritMultiplier : CalculatedDamage;
    }

    /// <summary>
    ///     获取总治疗值（含暴击倍率）。
    /// </summary>
    public float GetTotalHealing()
    {
        return IsCriticalHit ? CalculatedHealing * Data.CritMultiplier : CalculatedHealing;
    }

    /// <summary>
    ///     标记为已处理。
    /// </summary>
    public void MarkProcessed(string result = "Success")
    {
        IsProcessed = true;
        ResultDescription = result;
    }

    public override string ToString() =>
        $"EffectSpec[{Data.EffectName ?? Data.EffectId.ToString()}: DMG={CalculatedDamage:F0}, Crit={IsCriticalHit}]";
}