using UnityEngine;

// ============================================================
//  全局事件结构体定义 (GameEvents)
//  所有事件数据使用 struct 定义，确保 0 GC 传递。
//  事件按业务域分组：实体域、战斗域、技能域、物品域、任务域。
//
//  命名规范：{业务域}{动作}Event
//  例：EntityDeathEvent, ReactionTriggeredEvent
// ============================================================

// ──────────── 实体域事件 ────────────

/// <summary>
///     实体死亡事件。
///     当任何实体（玩家、怪物、靶子等）生命值归零时抛出。
/// </summary>
public struct EntityDeathEvent
{
    /// <summary>死亡实体的 Transform</summary>
    public Transform Entity;

    /// <summary>击杀者（可为 null）</summary>
    public Transform Killer;

    /// <summary>实体配置 ID（如果有）</summary>
    public int EntityId;

    /// <summary>死亡时剩余的过量伤害</summary>
    public float OverkillDamage;
}

/// <summary>
///     实体受伤事件。
///     在伤害结算完成后抛出，包含原始值和最终值。
/// </summary>
public struct EntityDamagedEvent
{
    /// <summary>受伤实体</summary>
    public Transform Target;

    /// <summary>伤害来源</summary>
    public Transform Instigator;

    /// <summary>原始伤害（GE 修正前）</summary>
    public float RawDamage;

    /// <summary>最终伤害（GE 修正后）</summary>
    public float FinalDamage;
}

// ──────────── 战斗域事件 ────────────

/// <summary>
///     元素反应触发事件。
///     当 ReactionEngine 结算反应成功时抛出。
/// </summary>
public struct ReactionTriggeredEvent
{
    /// <summary>反应类型</summary>
    public ReactionType ReactionType;

    /// <summary>反应显示名称（如"融化"、"超载"）</summary>
    public string ReactionName;

    /// <summary>反应目标</summary>
    public Transform Target;

    /// <summary>触发者</summary>
    public Transform Instigator;

    /// <summary>额外伤害</summary>
    public float BonusDamage;

    /// <summary>总伤害（含基础+反应加成）</summary>
    public float TotalDamage;
}

/// <summary>
///     暴击触发事件。
/// </summary>
public struct CritTriggeredEvent
{
    /// <summary>攻击者</summary>
    public Transform Attacker;

    /// <summary>目标</summary>
    public Transform Target;

    /// <summary>暴击伤害值</summary>
    public float CritDamage;

    /// <summary>暴击倍率</summary>
    public float CritMultiplier;
}

// ──────────── 技能域事件 ────────────

/// <summary>
///     技能释放完成事件。
///     当一个技能的完整流程（前摇→施法→后摇）执行完毕后抛出。
/// </summary>
public struct SkillCastCompleteEvent
{
    /// <summary>施法者</summary>
    public Transform Caster;

    /// <summary>技能 ID</summary>
    public int SkillId;

    /// <summary>技能名称</summary>
    public string SkillName;

    /// <summary>是否被打断</summary>
    public bool WasInterrupted;
}

/// <summary>
///     技能释放开始事件。
/// </summary>
public struct SkillCastStartEvent
{
    /// <summary>施法者</summary>
    public Transform Caster;

    /// <summary>技能 ID</summary>
    public int SkillId;

    /// <summary>技能名称</summary>
    public string SkillName;
}

// ──────────── 物品域事件 ────────────

/// <summary>
///     物品获取事件。
///     当物品添加到背包时抛出。
/// </summary>
public struct ItemAcquiredEvent
{
    /// <summary>获得物品的实体</summary>
    public Transform Owner;

    /// <summary>物品 ID</summary>
    public int ItemId;

    /// <summary>获取数量</summary>
    public int Amount;
}

/// <summary>
///     物品使用事件。
///     当消耗品被使用时抛出。
/// </summary>
public struct ItemUsedEvent
{
    /// <summary>使用物品的实体</summary>
    public Transform Owner;

    /// <summary>物品 ID</summary>
    public int ItemId;

    /// <summary>使用数量</summary>
    public int Amount;
}

/// <summary>
///     装备穿戴事件。
///     当装备被穿戴上时抛出。
/// </summary>
public struct EquipmentEquippedEvent
{
    /// <summary>穿戴装备的实体</summary>
    public Transform Owner;

    /// <summary>物品 ID</summary>
    public int ItemId;

    /// <summary>装备槽位</summary>
    public EquipmentSlot Slot;
}

/// <summary>
///     装备卸下事件。
/// </summary>
public struct EquipmentUnequippedEvent
{
    /// <summary>卸下装备的实体</summary>
    public Transform Owner;

    /// <summary>物品 ID</summary>
    public int ItemId;

    /// <summary>装备槽位</summary>
    public EquipmentSlot Slot;
}

// ──────────── 任务域事件 ────────────

/// <summary>
///     任务状态变更事件。
/// </summary>
public struct QuestStateChangedEvent
{
    /// <summary>任务 ID</summary>
    public int QuestId;

    /// <summary>任务名称</summary>
    public string QuestName;

    /// <summary>旧状态</summary>
    public QuestState OldState;

    /// <summary>新状态</summary>
    public QuestState NewState;
}

/// <summary>
///     任务进度更新事件。
/// </summary>
public struct QuestProgressEvent
{
    /// <summary>任务 ID</summary>
    public int QuestId;

    /// <summary>进度描述</summary>
    public string Description;

    /// <summary>当前进度</summary>
    public int Current;

    /// <summary>目标进度</summary>
    public int Target;
}

/// <summary>
///     对话触发事件。
///     由对话节点抛出，UI 层订阅此事件打开对话框。
/// </summary>
public struct DialogueTriggeredEvent
{
    /// <summary>对话 ID</summary>
    public int DialogueId;

    /// <summary>对话内容</summary>
    public string Content;

    /// <summary>说话者名称</summary>
    public string SpeakerName;

    /// <summary>回调：对话结束后由 UI 层调用以通知任务引擎继续 Tick</summary>
    public System.Action OnDialogueComplete;
}
