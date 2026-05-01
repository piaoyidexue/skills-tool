using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  SaveableInventory —— InventoryComponent 的存档适配器
//  将背包槽数组序列化为标准格式。
//  挂载到与 InventoryComponent 同一 GameObject 上即可接入存档系统。
// ============================================================

/// <summary>
///     背包容档适配器 —— 实现 ISaveable 接口。
///     序列化格式：
///     - "slot_count": int（槽位总数）
///     - "slot_0": "itemId,count" 格式的字符串（每个非空槽位一条）
///     - ...
///     - "slot_N": "itemId,count"
/// </summary>
[RequireComponent(typeof(InventoryComponent))]
public class SaveableInventory : MonoBehaviour, ISaveable
{
    // ──────────── ISaveable 实现 ────────────

    /// <summary>
    ///     存档唯一标识。格式："Inventory.{实例名}"。
    /// </summary>
    public string SaveKey => $"Inventory.{gameObject.name}";

    /// <summary>
    ///     生成背包快照：遍历所有槽位，将非空槽位序列化为 "itemId,count" 字符串。
    /// </summary>
    public Dictionary<string, object> CaptureSnapshot()
    {
        var inventory = GetComponent<InventoryComponent>();
        if (inventory == null) return null;

        var snapshot = new Dictionary<string, object>
        {
            ["slot_count"] = inventory.Capacity
        };

        var slots = inventory.Slots;
        for (var i = 0; i < slots.Count; i++)
        {
            if (slots[i].IsEmpty) continue;
            snapshot[$"slot_{i}"] = $"{slots[i].ItemId},{slots[i].Count}";
        }

        return snapshot;
    }

    /// <summary>
    ///     从快照恢复背包：清空所有槽位，然后按序恢复。
    ///     使用直接数组写入（不触发 AddItem 逻辑），避免产生事件副作用。
    /// </summary>
    public void RestoreSnapshot(Dictionary<string, object> snapshot)
    {
        var inventory = GetComponent<InventoryComponent>();
        if (inventory == null || snapshot == null) return;

        inventory.InitializeSlots(); // 清空所有槽位

        if (!snapshot.TryGetValue("slot_count", out var countObj)) return;
        var slotCount = System.Convert.ToInt32(countObj);

        // 恢复每个槽位
        for (var i = 0; i < slotCount; i++)
        {
            var key = $"slot_{i}";
            if (!snapshot.TryGetValue(key, out var slotData)) continue;

            var parts = slotData.ToString().Split(',');
            if (parts.Length != 2) continue;

            if (!int.TryParse(parts[0], out var itemId) || itemId <= 0) continue;
            if (!int.TryParse(parts[1], out var count) || count <= 0) continue;

            // 直接写入槽位（不触发事件，不触发 GlobalEventBus）
            var slots = inventory.Slots;
            if (i < slots.Count)
            {
                inventory.SetSlotDirect(i, itemId, count);
            }
        }
    }
}
