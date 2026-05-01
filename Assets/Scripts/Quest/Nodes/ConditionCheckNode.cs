using UnityEngine;

// ============================================================
//  条件检测节点 (ConditionCheckNode)
//  检查玩家背包中某物品数量是否达标，或某个属性是否达到阈值。
//  根据 true/false 端口分支出不同的后续路径。
//
//  设计准则：
//  - 使用两个输出端口："true" 和 "false"
//  - 条件类型通过枚举配置
//  - 直接读取 InventoryComponent 和 GEHost 数据
// ============================================================

/// <summary>
///     条件类型枚举。
/// </summary>
public enum QuestConditionType
{
    /// <summary>拥有指定物品且数量达标</summary>
    HasItem = 0,

    /// <summary>属性值达到阈值</summary>
    AttributeThreshold = 1,

    /// <summary>拥有指定 Tag</summary>
    HasTag = 2,

    /// <summary>任务已完成</summary>
    QuestCompleted = 3
}

/// <summary>
///     条件检测节点。
///     检查指定条件是否满足，根据结果走 "true" 或 "false" 分支。
/// </summary>
public class ConditionCheckNode : QuestNodeBase
{
    // ──────────── 配置 ────────────

    [Header("=== 条件配置 ===")]
    /// <summary>条件类型</summary>
    [SerializeField] private QuestConditionType _conditionType;

    /// <summary>目标参数（物品ID/属性名/Tag/任务ID）</summary>
    [SerializeField] private string _targetParameter = string.Empty;

    /// <summary>目标数值（物品数量/属性阈值）</summary>
    [SerializeField] private float _targetValue = 1f;

    /// <summary>GE 属性类型（当条件类型为 AttributeThreshold 时使用）</summary>
    [SerializeField] private GEAttribute _geAttribute;

    // ──────────── 构造 ────────────

    public ConditionCheckNode()
    {
        // 条件节点有两个输出端口："true" 和 "false"
        SetPortNames(
            new[] { "input" },
            new[] { "true", "false" }
        );
    }

    // ──────────── Tick ────────────

    public override QuestNodeResult Tick(float deltaTime)
    {
        // 条件检测是即时的，不需要 Running 状态
        return QuestNodeResult.Success;
    }

    /// <summary>
    ///     根据条件结果导航到下一个节点。
    ///     覆写基类的 ResolveNextNode，走 "true" 或 "false" 分支。
    /// </summary>
    public override QuestNodeBase ResolveNextNode()
    {
        var conditionMet = EvaluateCondition();
        var portName = conditionMet ? "true" : "false";
        return GetConnectedNode(portName) as QuestNodeBase;
    }

    // ──────────── 条件评估 ────────────

    private bool EvaluateCondition()
    {
        var owner = QuestRunner.CurrentOwner;
        if (owner == null) return false;

        switch (_conditionType)
        {
            case QuestConditionType.HasItem:
                return EvaluateHasItem(owner);

            case QuestConditionType.AttributeThreshold:
                return EvaluateAttributeThreshold(owner);

            case QuestConditionType.HasTag:
                return EvaluateHasTag(owner);

            case QuestConditionType.QuestCompleted:
                return EvaluateQuestCompleted();

            default:
                return false;
        }
    }

    private bool EvaluateHasItem(Transform owner)
    {
        var inventory = owner.GetComponent<InventoryComponent>();
        if (inventory == null) return false;

        if (!int.TryParse(_targetParameter, out var itemId)) return false;
        return inventory.GetItemCount(itemId) >= (int)_targetValue;
    }

    private bool EvaluateAttributeThreshold(Transform owner)
    {
        var geHost = owner.GetComponent<GEHost>();
        if (geHost == null) return false;

        var value = geHost.EvaluateAttribute(_geAttribute, 0f);
        return value >= _targetValue;
    }

    private bool EvaluateHasTag(Transform owner)
    {
        var geHost = owner.GetComponent<GEHost>();
        if (geHost == null) return false;

        return geHost.HasTag(_targetParameter);
    }

    private bool EvaluateQuestCompleted()
    {
        if (!int.TryParse(_targetParameter, out var questId)) return false;
        return QuestRunner.IsQuestCompleted(questId);
    }
}
