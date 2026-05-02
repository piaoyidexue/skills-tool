using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  GraphPreset —— 图拓扑预设模板
//  将高频出现的图结构沉淀为可复用预设，一键生成完整 SkillGraphAsset。
//  策划只需在配方表中填入"预设 ID"，编辑器脚本自动生成图、
//  连线、绑定 CSV 字段，后续仅对极个别特殊技能手动微调。
// ============================================================

/// <summary>
///     图拓扑预设类型 —— 覆盖《元素阵线》中常见的技能拓扑结构。
/// </summary>
public enum GraphPresetType
{
    /// <summary>穿透直线：前摇 → 动作等待 → 伤害 → 特效 → 后摇</summary>
    ImpactLane,

    /// <summary>射线光束：前摇 → 光束持续 → 多段Tick → 后摇</summary>
    BeamLane,

    /// <summary>投射物：前摇 → 发射 → 命中等待 → 伤害 → 特效 → 后摇</summary>
    ProjectileLane,

    /// <summary>AoE 范围：前摇 → EQS查询 → 范围特效 → 多目标伤害 → 后摇</summary>
    AoELane,

    /// <summary>吟唱持续：前摇 → 吟唱(多Tick) → 后摇</summary>
    ChannelLane,

    /// <summary>终结技：吸能 → 爆发 → 后摇</summary>
    FinisherLane,

    /// <summary>自定义（不使用预设，手动编辑图）</summary>
    Custom
}

/// <summary>
///     图拓扑预设配置 —— 描述一个预设的参数化拓扑。
///     被 SkillRecipe 引用，由 ElementLineGraphGenerator 消费。
/// </summary>
[Serializable]
public class GraphPreset
{
    /// <summary>预设类型</summary>
    public GraphPresetType PresetType = GraphPresetType.ImpactLane;

    /// <summary>预设显示名称</summary>
    public string DisplayName = "穿透模板";

    /// <summary>是否包含条件分支（暴击/概率判定）</summary>
    public bool HasConditionBranch;

    /// <summary>条件分支类型</summary>
    public ConditionMode BranchConditionMode = ConditionMode.Random;

    /// <summary>是否包含 EQS 查询节点</summary>
    public bool HasTargetQuery;

    /// <summary>EQS 查询最大范围</summary>
    public float TargetQueryRange = 10f;

    /// <summary>EQS 最大目标数</summary>
    public int TargetQueryMaxResults = 1;

    /// <summary>是否包含元素反应标签</summary>
    public bool HasElementTags;

    /// <summary>元素标签（分号分隔，如 "element.fire;element.ice"）</summary>
    public string ElementTags = string.Empty;

    /// <summary>是否包含地形涂绘</summary>
    public bool HasTerrainPaint;

    /// <summary>地形标签（分号分隔，如 "scorch|ice"）</summary>
    public string TerrainTags = string.Empty;

    /// <summary>是否包含共鸣节点</summary>
    public bool HasResonance;

    /// <summary>共鸣标签</summary>
    public string ResonanceTags = string.Empty;

    /// <summary>是否使用子图（公共伤害逻辑块）</summary>
    public bool UseSubGraphForDamage;

    /// <summary>子图资产路径（如 "SkillGraphs/Common_ImpactDamage"）</summary>
    public string SubGraphPath = string.Empty;

    // ---- 预设工厂 ----

    /// <summary>获取预设类型的默认配置</summary>
    public static GraphPreset GetDefault(GraphPresetType type)
    {
        return type switch
        {
            GraphPresetType.ImpactLane => new GraphPreset
            {
                PresetType = GraphPresetType.ImpactLane,
                DisplayName = "穿透模板",
                HasConditionBranch = false,
                HasTargetQuery = false,
                HasElementTags = true,
                ElementTags = "element.fire"
            },
            GraphPresetType.BeamLane => new GraphPreset
            {
                PresetType = GraphPresetType.BeamLane,
                DisplayName = "射线模板",
                HasConditionBranch = false,
                HasTargetQuery = false,
                HasElementTags = true,
                ElementTags = "element.fire"
            },
            GraphPresetType.ProjectileLane => new GraphPreset
            {
                PresetType = GraphPresetType.ProjectileLane,
                DisplayName = "投射物模板",
                HasConditionBranch = false,
                HasTargetQuery = false,
                HasElementTags = true,
                ElementTags = "element.fire"
            },
            GraphPresetType.AoELane => new GraphPreset
            {
                PresetType = GraphPresetType.AoELane,
                DisplayName = "AoE范围模板",
                HasConditionBranch = false,
                HasTargetQuery = true,
                TargetQueryRange = 5f,
                TargetQueryMaxResults = 5,
                HasElementTags = true,
                ElementTags = "element.fire"
            },
            GraphPresetType.ChannelLane => new GraphPreset
            {
                PresetType = GraphPresetType.ChannelLane,
                DisplayName = "吟唱模板",
                HasConditionBranch = false,
                HasTargetQuery = false,
                HasElementTags = true,
                ElementTags = "element.fire"
            },
            GraphPresetType.FinisherLane => new GraphPreset
            {
                PresetType = GraphPresetType.FinisherLane,
                DisplayName = "终结技模板",
                HasConditionBranch = false,
                HasTargetQuery = false,
                HasElementTags = true,
                ElementTags = "element.fire",
                HasResonance = true,
                ResonanceTags = "row_focus"
            },
            _ => new GraphPreset
            {
                PresetType = GraphPresetType.Custom,
                DisplayName = "自定义"
            }
        };
    }
}

/// <summary>
///     技能配方 —— 从 CSV 读取的配方行，描述一个技能应使用哪个预设、什么参数。
///     工作流：策划填写 SkillRecipe.csv → 编辑器读取 → 一键生成 SkillGraphAsset。
/// </summary>
[Serializable]
public class SkillRecipe
{
    /// <summary>技能 ID（对应 Skill.csv 的 skill_id）</summary>
    public int SkillId;

    /// <summary>技能名称</summary>
    public string SkillName;

    /// <summary>使用的预设类型</summary>
    public GraphPresetType PresetType;

    /// <summary>预设参数覆盖（JSON 序列化，可选）</summary>
    public string PresetOverrideJson;

    /// <summary>元素标签（覆盖预设默认）</summary>
    public string ElementTags;

    /// <summary>输出图资产路径（生成的 SkillGraphAsset 存放位置）</summary>
    public string OutputPath = "Assets/Resources/SkillGraphs/";
}
