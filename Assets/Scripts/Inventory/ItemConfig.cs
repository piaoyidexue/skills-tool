using System.Collections.Generic;

// ============================================================
//  物品配置数据 (ItemConfig)
//  CSV 驱动，与 ConfigLoader 配合使用。
//  物品系统的核心是"数据的载体"，装备的核心是"状态（GAS）的附着物"。
//  完全复用已有的 CSV 数据流和 GameplayEffect（GE）系统。
// ============================================================

/// <summary>
///     物品类型枚举。
/// </summary>
public enum ItemType
{
    /// <summary>普通物品（材料、钥匙等）</summary>
    Consumable = 0,

    /// <summary>可装备物品（武器、防具等）</summary>
    Equipment = 1,

    /// <summary>素材/材料</summary>
    Material = 2,

    /// <summary>任务物品</summary>
    QuestItem = 3
}

/// <summary>
///     装备槽位枚举。
/// </summary>
public enum EquipmentSlot
{
    /// <summary>无槽位（非装备）</summary>
    None = 0,

    /// <summary>头部</summary>
    Head = 1,

    /// <summary>身体</summary>
    Body = 2,

    /// <summary>武器</summary>
    Weapon = 3,

    /// <summary>饰品</summary>
    Accessory = 4
}

/// <summary>
///     物品配置数据。
///     从 Item.csv 加载，作为物品系统的唯一定义源。
/// </summary>
public class ItemConfig
{
    /// <summary>物品唯一 ID</summary>
    public int ItemID;

    /// <summary>物品名称</summary>
    public string ItemName;

    /// <summary>物品描述</summary>
    public string Description;

    /// <summary>物品类型</summary>
    public ItemType Type;

    /// <summary>最大堆叠数</summary>
    public int MaxStack;

    /// <summary>关联的 GE ID（装备类物品穿戴时赋予的永久 Buff）</summary>
    public int GameplayEffectID;

    /// <summary>关联的技能图 ID（消耗品类物品使用时触发的技能效果）</summary>
    public int SkillGraphID;

    /// <summary>装备槽位（仅装备类物品有效）</summary>
    public EquipmentSlot EquipSlot;

    /// <summary>物品品质（0=普通, 1=精良, 2=稀有, 3=史诗, 4=传说）</summary>
    public int Quality;

    /// <summary>出售价格</summary>
    public int SellPrice;

    /// <summary>图标 Key（对应 EffectConfig 或 UI 图集）</summary>
    public string IconKey;

    /// <summary>是否可丢弃</summary>
    public bool CanDiscard;

    /// <summary>额外标签（管道符分隔，如 fire|boost）</summary>
    public List<string> Tags = new();

    /// <summary>
    ///     是否为装备类型。
    /// </summary>
    public bool IsEquipment => Type == ItemType.Equipment;

    /// <summary>
    ///     是否为消耗品类型。
    /// </summary>
    public bool IsConsumable => Type == ItemType.Consumable;

    /// <summary>
    ///     是否有关联的 GameplayEffect（装备类）。
    /// </summary>
    public bool HasGameplayEffect => GameplayEffectID > 0;

    /// <summary>
    ///     是否有关联的技能图（消耗品类）。
    /// </summary>
    public bool HasSkillGraph => SkillGraphID > 0;
}
