using UnityEngine;

/// <summary>
///     奖励类型枚举。
/// </summary>
public enum RewardType
{
    Item = 0,
    GameplayEffect = 1,
    Skill = 2
}

/// <summary>
///     奖励派发节点。向玩家的 InventoryComponent 发放指定配置的物品。
/// </summary>
public class RewardNode : QuestNodeBase
{
    [Header("=== 奖励配置 ===")]
    [SerializeField] private RewardType _rewardType;
    [SerializeField] private int _targetId;
    [SerializeField] private int _amount = 1;
    [SerializeField] private string _description = string.Empty;

    public override void OnEnter() { GrantReward(); }

    public override QuestNodeResult Tick(float deltaTime) => QuestNodeResult.Success;

    private void GrantReward()
    {
        var owner = QuestRunner.CurrentOwner;
        if (owner == null) { Debug.LogWarning("[RewardNode] No owner."); return; }

        switch (_rewardType)
        {
            case RewardType.Item:
                GrantItemReward(owner);
                break;
            case RewardType.GameplayEffect:
                GrantGEReward(owner);
                break;
            case RewardType.Skill:
                Debug.Log($"[RewardNode] Skill reward: SkillID={_targetId}");
                break;
        }
    }

    private void GrantItemReward(Transform owner)
    {
        var inventory = owner.GetComponent<InventoryComponent>();
        if (inventory == null) { Debug.LogWarning("[RewardNode] No InventoryComponent."); return; }

        var result = inventory.AddItem(_targetId, _amount);
        Debug.Log(result == InventoryResult.Success || result == InventoryResult.InventoryFull
            ? $"[RewardNode] Granted item: ID={_targetId}, Amount={_amount}"
            : $"[RewardNode] Failed to add item {_targetId}: {result}");
    }

    private void GrantGEReward(Transform owner)
    {
        var geHost = owner.GetComponent<GEHost>();
        if (geHost == null) { Debug.LogWarning("[RewardNode] No GEHost."); return; }

        var geData = ConfigLoader.GetGameplayEffectData(_targetId);
        if (geData == null) { Debug.LogWarning($"[RewardNode] GE not found: {_targetId}"); return; }

        var geConfig = new GEConfig
        {
            GEId = geData.EffectId,
            Name = geData.EffectName,
            DurationPolicy = geData.DurationPolicy,
            Duration = geData.Duration,
            StackPolicy = geData.StackPolicy,
            MaxStacks = geData.MaxStacks
        };
        if (geData.GrantedTags != null) geConfig.GrantedTags.AddRange(geData.GrantedTags);

        geHost.ApplyEffect(geConfig, owner);
        Debug.Log($"[RewardNode] Granted GE: {geData.EffectName}");
    }
}