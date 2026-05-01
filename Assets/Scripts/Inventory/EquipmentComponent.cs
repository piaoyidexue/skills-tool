using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  装备组件 (EquipmentComponent)
//  装备的核心是"状态（GAS）的附着物"。
//  装备被穿戴时，本质上是向角色的 GEHost 注入一个
//  持续时间为"永久（Infinite）"的 GE。
//  卸下时，根据记录的 GE 实例句柄，调用 GEHost.RemoveEffect 剥离属性加成。
//
//  设计准则：
//  - 完全复用 GEHost + GameplayEffect 系统
//  - 装备槽与 GE 实例一一映射，通过 GE ID 管理
//  - 穿戴/卸下通过 GlobalEventBus 发布事件
// ============================================================

/// <summary>
///     装备槽运行时数据。
///     记录槽位中的物品 ID 和对应的 GE 实例 ID。
/// </summary>
[Serializable]
public struct EquippedItem
{
    /// <summary>装备的物品 ID（0 表示空槽）</summary>
    public int ItemId;

    /// <summary>穿戴时注入的 GE 实例 ID（用于卸下时移除）</summary>
    public int AppliedGEId;

    /// <summary>槽位是否为空</summary>
    public bool IsEmpty => ItemId <= 0;

    /// <summary>创建空装备槽</summary>
    public static EquippedItem Empty => new() { ItemId = 0, AppliedGEId = 0 };
}

/// <summary>
///     装备操作结果。
/// </summary>
public enum EquipResult
{
    /// <summary>操作成功</summary>
    Success,

    /// <summary>槽位不匹配</summary>
    SlotMismatch,

    /// <summary>槽位已被占用（需先卸下）</summary>
    SlotOccupied,

    /// <summary>物品不是装备</summary>
    NotEquipment,

    /// <summary>物品配置缺失</summary>
    ConfigMissing,

    /// <summary>GE 配置缺失</summary>
    GEConfigMissing,

    /// <summary>GEHost 组件缺失</summary>
    NoGEHost,

    /// <summary>槽位已为空</summary>
    SlotEmpty,

    /// <summary>背包空间不足</summary>
    InventoryFull
}

/// <summary>
///     装备组件。
///     定义不同的装备槽（头部、身体、武器、饰品），
///     通过 GEHost 桥接 GameplayEffect 系统。
///     需要 GEHost 和 InventoryComponent 组件配合使用。
/// </summary>
[RequireComponent(typeof(GEHost))]
public class EquipmentComponent : MonoBehaviour
{
    // ──────────── 配置 ────────────

    [Header("=== 装备配置 ===")]
    [Tooltip("初始装备物品 ID 列表（按槽位顺序：Head, Body, Weapon, Accessory），0=无")]
    [SerializeField] private int[] _initialEquipment = { 0, 0, 0, 0 };

    // ──────────── 运行时数据 ────────────

    /// <summary>
    ///     装备槽数组。索引对应 EquipmentSlot 枚举值。
    ///     [1]=Head, [2]=Body, [3]=Weapon, [4]=Accessory
    /// </summary>
    private readonly EquippedItem[] _equipSlots = new EquippedItem[5];

    /// <summary>GE 实例 ID 计数器（为每个装备分配唯一 ID）</summary>
    private int _geIdCounter = 10000;

    // ──────────── 引用 ────────────

    private GEHost _geHost;
    private InventoryComponent _inventory;

    // ──────────── 公开属性 ────────────

    /// <summary>获取指定装备槽的内容</summary>
    public EquippedItem this[EquipmentSlot slot] => _equipSlots[(int)slot];

    /// <summary>所有装备槽（只读）</summary>
    public IReadOnlyList<EquippedItem> AllSlots => _equipSlots;

    // ──────────── 事件 ────────────

    /// <summary>装备变更回调</summary>
    public event Action<EquipmentSlot> OnEquipmentChanged;

    // ──────────── 生命周期 ────────────

    private void Awake()
    {
        _geHost = GetComponent<GEHost>();
        _inventory = GetComponent<InventoryComponent>();
    }

    private void Start()
    {
        // 应用初始装备
        if (_initialEquipment != null)
        {
            for (var i = 1; i < _initialEquipment.Length && i < _equipSlots.Length; i++)
            {
                if (_initialEquipment[i] > 0)
                {
                    EquipInternal(_initialEquipment[i], (EquipmentSlot)i);
                }
            }
        }
    }

    // ============================================================
    //  核心 API
    // ============================================================

    /// <summary>
    ///     穿戴装备。
    ///     流程：检查槽位匹配 → 读取物品 GE ID → 构造 GE 实例 → ApplyEffect 赋予角色永久 Buff。
    ///     如果槽位已有装备，先自动卸下旧装备。
    /// </summary>
    /// <param name="itemId">装备物品 ID</param>
    /// <returns>操作结果</returns>
    public EquipResult Equip(int itemId)
    {
        if (itemId <= 0) return EquipResult.NotEquipment;

        var config = ConfigLoader.GetItemConfig(itemId);
        if (config == null) return EquipResult.ConfigMissing;
        if (!config.IsEquipment) return EquipResult.NotEquipment;
        if (config.EquipSlot == EquipmentSlot.None) return EquipResult.SlotMismatch;

        if (_geHost == null) return EquipResult.NoGEHost;

        var slot = config.EquipSlot;

        // 如果槽位已有装备，先卸下
        if (!_equipSlots[(int)slot].IsEmpty)
        {
            var unequipResult = Unequip(slot);
            if (unequipResult != EquipResult.Success) return unequipResult;
        }

        return EquipInternal(itemId, slot);
    }

    /// <summary>
    ///     卸下装备。
    ///     流程：根据记录的 GE 实例句柄 → 调用 GEHost.RemoveEffect 剥离属性加成。
    ///     卸下的装备回到背包（如果背包有空间）。
    /// </summary>
    public EquipResult Unequip(EquipmentSlot slot)
    {
        if (slot == EquipmentSlot.None) return EquipResult.SlotMismatch;

        var equippedItem = _equipSlots[(int)slot];
        if (equippedItem.IsEmpty) return EquipResult.SlotEmpty;

        if (_geHost == null) return EquipResult.NoGEHost;

        // 移除 GE 效果
        _geHost.RemoveEffect(equippedItem.AppliedGEId);

        var itemId = equippedItem.ItemId;

        // 清空槽位
        _equipSlots[(int)slot] = EquippedItem.Empty;

        // 尝试放回背包
        if (_inventory != null)
        {
            var result = _inventory.AddItem(itemId);
            if (result == InventoryResult.InventoryFull)
            {
                Debug.LogWarning($"[EquipmentComponent] Inventory full, item {itemId} dropped.");
            }
        }

        // 发布全局事件
        GlobalEventBus.Publish(new EquipmentUnequippedEvent
        {
            Owner = transform,
            ItemId = itemId,
            Slot = slot
        });

        OnEquipmentChanged?.Invoke(slot);
        return EquipResult.Success;
    }

    /// <summary>
    ///     检查指定槽位是否有装备。
    /// </summary>
    public bool IsSlotOccupied(EquipmentSlot slot)
    {
        return !_equipSlots[(int)slot].IsEmpty;
    }

    /// <summary>
    ///     获取指定槽位装备的物品 ID。
    /// </summary>
    public int GetEquippedItemId(EquipmentSlot slot)
    {
        return _equipSlots[(int)slot].ItemId;
    }

    /// <summary>
    ///     卸下所有装备。
    /// </summary>
    public void UnequipAll()
    {
        for (var i = 1; i < _equipSlots.Length; i++)
        {
            if (!_equipSlots[i].IsEmpty)
            {
                Unequip((EquipmentSlot)i);
            }
        }
    }

    // ──────────── 内部辅助 ────────────

    /// <summary>
    ///     内部穿戴逻辑：构造 GE 实例 → ApplyEffect。
    ///     装备的 GE 数据从 GameplayEffect.csv 获取，
    ///     同时将 DurationPolicy 强制设为 Infinite（永久）。
    /// </summary>
    private EquipResult EquipInternal(int itemId, EquipmentSlot slot)
    {
        var config = ConfigLoader.GetItemConfig(itemId);
        if (config == null) return EquipResult.ConfigMissing;

        // 读取关联的 GE 配置
        if (!config.HasGameplayEffect) return EquipResult.GEConfigMissing;

        var geData = ConfigLoader.GetGameplayEffectData(config.GameplayEffectID);
        if (geData == null)
        {
            Debug.LogWarning($"[EquipmentComponent] GE config not found: {config.GameplayEffectID}");
            return EquipResult.GEConfigMissing;
        }

        // 分配唯一 GE 实例 ID（避免与其他 GE 冲突）
        var uniqueGEId = ++_geIdCounter;

        // 构造 GE 配置：持续时间为永久（Infinite）
        var geConfig = new GEConfig
        {
            GEId = uniqueGEId,
            Name = $"Equip_{config.ItemName}",
            DurationPolicy = GEDurationPolicy.Infinite,  // 装备永远是永久效果
            StackPolicy = GEStackPolicy.Ignore,
            MaxStacks = 1
        };

        // 复制 GE 的标签
        if (geData.GrantedTags != null)
        {
            geConfig.GrantedTags.AddRange(geData.GrantedTags);
        }

        // 添加装备特有标签
        geConfig.GrantedTags.Add($"equip.{config.ItemName.ToLowerInvariant().Replace(" ", "_")}");

        // 装备属性加成通过 GE Modifier 实现
        // 根据装备配置构造对应的 Modifier（CSV 中的 GameplayEffect 条目定义了属性修改）
        BuildEquipmentModifiers(geData, geConfig, config);

        // 向 GEHost 注入永久 GE
        var applied = _geHost.ApplyEffect(geConfig, transform);
        if (!applied)
        {
            Debug.LogWarning($"[EquipmentComponent] Failed to apply GE for item {itemId}");
            return EquipResult.GEConfigMissing;
        }

        // 记录槽位
        _equipSlots[(int)slot] = new EquippedItem
        {
            ItemId = itemId,
            AppliedGEId = uniqueGEId
        };

        // 从背包移除（如果存在背包组件且物品在背包中）
        if (_inventory != null && _inventory.HasItem(itemId))
        {
            _inventory.RemoveItem(itemId);
        }

        // 发布全局事件
        GlobalEventBus.Publish(new EquipmentEquippedEvent
        {
            Owner = transform,
            ItemId = itemId,
            Slot = slot
        });

        OnEquipmentChanged?.Invoke(slot);
        return EquipResult.Success;
    }

    /// <summary>
    ///     根据装备配置和 GameplayEffect 数据构建 Modifier 列表。
    ///     装备的属性加成本质上就是 GE Modifier 的应用。
    ///     此方法将 CSV 中的 GameplayEffect 数值映射为运行时 Modifier。
    /// </summary>
    private void BuildEquipmentModifiers(GameplayEffectData geData, GEConfig geConfig, ItemConfig itemConfig)
    {
        // 基础伤害/治疗作为 Custom 属性的 Modifier
        if (geData.BaseDamage > 0)
        {
            geConfig.Modifiers.Add(new GEModifier
            {
                Attribute = GEAttribute.Custom,
                Operation = GEModOp.Add,
                Magnitude = geData.BaseDamage
            });
        }

        if (geData.BaseHealing > 0)
        {
            geConfig.Modifiers.Add(new GEModifier
            {
                Attribute = GEAttribute.HealingReceived,
                Operation = GEModOp.Add,
                Magnitude = geData.BaseHealing
            });
        }

        // 根据装备槽位自动推导默认 Modifier
        // 武器：攻击力加成  头部/身体：防御/生命  饰品：速度/暴击
        switch (itemConfig.EquipSlot)
        {
            case EquipmentSlot.Weapon:
                if (geData.BaseDamage > 0)
                {
                    geConfig.Modifiers.Add(new GEModifier
                    {
                        Attribute = GEAttribute.DamageDealtMultiplier,
                        Operation = GEModOp.Add,
                        Magnitude = geData.BaseDamage * 0.01f
                    });
                }
                break;

            case EquipmentSlot.Body:
                // 身体装备提供受伤减免
                geConfig.Modifiers.Add(new GEModifier
                {
                    Attribute = GEAttribute.DamageTakenMultiplier,
                    Operation = GEModOp.Multiply,
                    Magnitude = 1f - geData.BaseDamage * 0.005f
                });
                break;

            case EquipmentSlot.Head:
                // 头部装备提供攻击速度
                geConfig.Modifiers.Add(new GEModifier
                {
                    Attribute = GEAttribute.AttackSpeed,
                    Operation = GEModOp.Add,
                    Magnitude = geData.BaseDamage * 0.02f
                });
                break;

            case EquipmentSlot.Accessory:
                // 饰品提供移动速度
                geConfig.Modifiers.Add(new GEModifier
                {
                    Attribute = GEAttribute.MoveSpeed,
                    Operation = GEModOp.Add,
                    Magnitude = geData.BaseDamage * 0.05f
                });
                break;
        }
    }
}
