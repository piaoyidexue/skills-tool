using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  背包核心组件 (InventoryComponent)
//  数据驱动、0 GC 的背包系统。
//  核心数据结构为固定长度的 ItemSlot 数组，
//  支持堆叠、交换、检查容量等操作。
//
//  设计准则：
//  - ItemSlot 使用 struct，零 GC 传递
//  - 所有操作通过 ItemConfig + ConfigLoader 获取定义
//  - 与 EquipmentComponent 协同，但不直接依赖
// ============================================================

/// <summary>
///     物品槽 —— 纯数据结构体，0 GC。
///     一个槽位只容纳一种物品，记录物品 ID 和当前数量。
/// </summary>
[Serializable]
public struct ItemSlot
{
    /// <summary>物品 ID（对应 ItemConfig.ItemID，0 表示空槽）</summary>
    public int ItemId;

    /// <summary>当前堆叠数量</summary>
    public int Count;

    /// <summary>槽位是否为空</summary>
    public bool IsEmpty => ItemId <= 0 || Count <= 0;

    /// <summary>
    ///     获取物品配置（需 ConfigLoader 已初始化）。
    /// </summary>
    public ItemConfig Config => ItemId > 0 ? ConfigLoader.GetItemConfig(ItemId) : null;

    /// <summary>
    ///     构造一个有内容的物品槽。
    /// </summary>
    public ItemSlot(int itemId, int count)
    {
        ItemId = itemId;
        Count = count;
    }

    /// <summary>
    ///     创建空槽。
    /// </summary>
    public static ItemSlot Empty => new(0, 0);
}

/// <summary>
///     背包操作结果枚举。
/// </summary>
public enum InventoryResult
{
    /// <summary>操作成功</summary>
    Success,

    /// <summary>背包已满</summary>
    InventoryFull,

    /// <summary>物品不存在</summary>
    ItemNotFound,

    /// <summary>数量不足</summary>
    InsufficientCount,

    /// <summary>无效操作（如交换同一槽位）</summary>
    InvalidOperation,

    /// <summary>物品不可丢弃</summary>
    CannotDiscard
}

/// <summary>
///     背包核心组件。
///     维护一个固定长度的 ItemSlot 数组，提供添加、移除、交换等基础 API。
///     通过 GlobalEventBus 发布 ItemAcquiredEvent / ItemUsedEvent。
/// </summary>
public class InventoryComponent : MonoBehaviour
{
    // ──────────── 配置 ────────────

    [Header("=== 背包配置 ===")]
    [Tooltip("背包槽位总数")]
    [SerializeField] private int _capacity = 40;

    // ──────────── 运行时数据 ────────────

    /// <summary>物品槽数组（固定长度）</summary>
    [SerializeField] private ItemSlot[] _slots;

    // ──────────── 公开属性 ────────────

    /// <summary>背包容量</summary>
    public int Capacity => _capacity;

    /// <summary>物品槽数组（只读）</summary>
    public IReadOnlyList<ItemSlot> Slots => _slots;

    /// <summary>已使用槽位数</summary>
    public int UsedSlotCount
    {
        get
        {
            var count = 0;
            foreach (var slot in _slots)
                if (!slot.IsEmpty) count++;
            return count;
        }
    }

    /// <summary>空闲槽位数</summary>
    public int FreeSlotCount => _capacity - UsedSlotCount;

    // ──────────── 事件 ────────────

    /// <summary>物品变更回调（用于 UI 刷新等）</summary>
    public event Action<int> OnSlotChanged;

    /// <summary>背包内容变更回调</summary>
    public event Action OnInventoryChanged;

    // ──────────── 生命周期 ────────────

    private void Awake()
    {
        InitializeSlots();
    }

    /// <summary>
    ///     初始化物品槽数组。
    /// </summary>
    public void InitializeSlots()
    {
        if (_slots == null || _slots.Length != _capacity)
        {
            _slots = new ItemSlot[_capacity];
            for (var i = 0; i < _capacity; i++)
                _slots[i] = ItemSlot.Empty;
        }
    }

    // ============================================================
    //  核心 API
    // ============================================================

    /// <summary>
    ///     添加物品到背包（处理堆叠逻辑）。
    ///     优先堆叠到已有同类物品的槽位，其次放入空槽。
    /// </summary>
    /// <param name="itemId">物品 ID</param>
    /// <param name="amount">添加数量</param>
    /// <param name="remaining">未能添加的剩余数量</param>
    /// <returns>操作结果</returns>
    public InventoryResult AddItem(int itemId, int amount, out int remaining)
    {
        remaining = amount;
        if (itemId <= 0 || amount <= 0) return InventoryResult.InvalidOperation;

        var config = ConfigLoader.GetItemConfig(itemId);
        if (config == null) return InventoryResult.ItemNotFound;

        // 第一步：尝试堆叠到已有同类物品的槽位
        for (var i = 0; i < _capacity && remaining > 0; i++)
        {
            if (_slots[i].ItemId != itemId) continue;

            var maxAdd = config.MaxStack - _slots[i].Count;
            var toAdd = Mathf.Min(remaining, maxAdd);
            if (toAdd <= 0) continue;

            _slots[i].Count += toAdd;
            remaining -= toAdd;
            NotifySlotChanged(i);
        }

        // 第二步：放入空槽
        for (var i = 0; i < _capacity && remaining > 0; i++)
        {
            if (!_slots[i].IsEmpty) continue;

            var toAdd = Mathf.Min(remaining, config.MaxStack);
            _slots[i] = new ItemSlot(itemId, toAdd);
            remaining -= toAdd;
            NotifySlotChanged(i);
        }

        // 发布全局事件
        var added = amount - remaining;
        if (added > 0)
        {
            GlobalEventBus.Publish(new ItemAcquiredEvent
            {
                Owner = transform,
                ItemId = itemId,
                Amount = added
            });
        }

        return remaining > 0 ? InventoryResult.InventoryFull : InventoryResult.Success;
    }

    /// <summary>
    ///     添加物品（简化版，不关心剩余数量）。
    /// </summary>
    public InventoryResult AddItem(int itemId, int amount = 1)
    {
        return AddItem(itemId, amount, out _);
    }

    /// <summary>
    ///     移除指定数量的物品。
    /// </summary>
    /// <param name="itemId">物品 ID</param>
    /// <param name="amount">移除数量</param>
    /// <returns>操作结果</returns>
    public InventoryResult RemoveItem(int itemId, int amount = 1)
    {
        if (itemId <= 0 || amount <= 0) return InventoryResult.InvalidOperation;

        // 检查总量是否足够
        var totalCount = GetItemCount(itemId);
        if (totalCount < amount) return InventoryResult.InsufficientCount;

        // 从后往前移除（保持前部槽位紧凑）
        var remaining = amount;
        for (var i = _capacity - 1; i >= 0 && remaining > 0; i--)
        {
            if (_slots[i].ItemId != itemId) continue;

            var toRemove = Mathf.Min(remaining, _slots[i].Count);
            _slots[i].Count -= toRemove;
            remaining -= toRemove;

            if (_slots[i].Count <= 0)
            {
                _slots[i] = ItemSlot.Empty;
            }

            NotifySlotChanged(i);
        }

        OnInventoryChanged?.Invoke();
        return InventoryResult.Success;
    }

    /// <summary>
    ///     交换两个槽位的内容。
    /// </summary>
    public InventoryResult SwapSlots(int indexA, int indexB)
    {
        if (indexA < 0 || indexA >= _capacity || indexB < 0 || indexB >= _capacity)
            return InventoryResult.InvalidOperation;
        if (indexA == indexB) return InventoryResult.InvalidOperation;

        var temp = _slots[indexA];
        _slots[indexA] = _slots[indexB];
        _slots[indexB] = temp;

        NotifySlotChanged(indexA);
        NotifySlotChanged(indexB);
        return InventoryResult.Success;
    }

    /// <summary>
    ///     获取指定物品的总数量。
    /// </summary>
    public int GetItemCount(int itemId)
    {
        var total = 0;
        foreach (var slot in _slots)
            if (slot.ItemId == itemId)
                total += slot.Count;
        return total;
    }

    /// <summary>
    ///     检查是否拥有指定数量的物品。
    /// </summary>
    public bool HasItem(int itemId, int amount = 1)
    {
        return GetItemCount(itemId) >= amount;
    }

    /// <summary>
    ///     查找指定物品的第一个槽位索引，不存在返回 -1。
    /// </summary>
    public int FindSlot(int itemId)
    {
        for (var i = 0; i < _capacity; i++)
            if (_slots[i].ItemId == itemId)
                return i;
        return -1;
    }

    /// <summary>
    ///     查找第一个空槽索引，不存在返回 -1。
    /// </summary>
    public int FindEmptySlot()
    {
        for (var i = 0; i < _capacity; i++)
            if (_slots[i].IsEmpty)
                return i;
        return -1;
    }

    /// <summary>
    ///     使用消耗品。
    ///     如果物品是消耗品，读取其技能图 ID 触发效果，然后在背包中扣除数量。
    /// </summary>
    /// <param name="slotIndex">槽位索引</param>
    /// <returns>操作结果</returns>
    public InventoryResult UseItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _capacity) return InventoryResult.InvalidOperation;

        var slot = _slots[slotIndex];
        if (slot.IsEmpty) return InventoryResult.ItemNotFound;

        var config = slot.Config;
        if (config == null) return InventoryResult.ItemNotFound;

        // 仅消耗品可使用
        if (!config.IsConsumable) return InventoryResult.InvalidOperation;

        // 扣除数量
        _slots[slotIndex].Count -= 1;
        if (_slots[slotIndex].Count <= 0)
        {
            _slots[slotIndex] = ItemSlot.Empty;
        }
        NotifySlotChanged(slotIndex);

        // 发布全局事件
        GlobalEventBus.Publish(new ItemUsedEvent
        {
            Owner = transform,
            ItemId = config.ItemID,
            Amount = 1
        });

        return InventoryResult.Success;
    }

    /// <summary>
    ///     丢弃指定槽位的物品。
    /// </summary>
    public InventoryResult DiscardItem(int slotIndex, int amount = 1)
    {
        if (slotIndex < 0 || slotIndex >= _capacity) return InventoryResult.InvalidOperation;

        var slot = _slots[slotIndex];
        if (slot.IsEmpty) return InventoryResult.ItemNotFound;

        var config = slot.Config;
        if (config != null && !config.CanDiscard) return InventoryResult.CannotDiscard;

        var toRemove = Mathf.Min(amount, slot.Count);
        _slots[slotIndex].Count -= toRemove;
        if (_slots[slotIndex].Count <= 0)
        {
            _slots[slotIndex] = ItemSlot.Empty;
        }
        NotifySlotChanged(slotIndex);

        return InventoryResult.Success;
    }

    /// <summary>
    ///     清空所有物品。
    /// </summary>
    public void ClearAll()
    {
        for (var i = 0; i < _capacity; i++)
        {
            if (!_slots[i].IsEmpty)
            {
                _slots[i] = ItemSlot.Empty;
                NotifySlotChanged(i);
            }
        }
    }

    // ──────────── 内部辅助 ────────────

    private void NotifySlotChanged(int index)
    {
        OnSlotChanged?.Invoke(index);
        OnInventoryChanged?.Invoke();
    }

    // ──────────── 存档接口 ────────────

    /// <summary>
    ///     直接设置指定槽位的数据（不触发事件、不触发 GlobalEventBus）。
    ///     仅供 SaveableInventory.RestoreSnapshot 使用，
    ///     避免加载存档时产生副作用。
    /// </summary>
    public void SetSlotDirect(int index, int itemId, int count)
    {
        if (index < 0 || index >= _capacity) return;
        _slots[index] = new ItemSlot(itemId, count);
    }
}
