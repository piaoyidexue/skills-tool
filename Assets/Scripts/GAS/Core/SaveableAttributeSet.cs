using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  SaveableAttributeSet —— AttributeSet 的存档适配器
//  记录角色核心游玩数据（当前生命值、等级等）。
//  使用 BindableProperty.SetValueWithoutNotify 恢复状态，
//  避免触发 UI 回调副作用。
// ============================================================

/// <summary>
///     AttributeSet 存档适配器 —— 实现 ISaveable 接口。
///     序列化格式：
///     - "current_health": float
///     - "max_health": float
///     - "attack": float
///     - "defense": float
///     - "move_speed": float
///     - "attack_speed": float
///     - "crit_chance": float
///     - "crit_damage": float
/// </summary>
[RequireComponent(typeof(AttributeSet))]
public class SaveableAttributeSet : MonoBehaviour, ISaveable
{
    // ──────────── ISaveable 实现 ────────────

    /// <summary>
    ///     存档唯一标识。格式："AttributeSet.{实例名}"。
    /// </summary>
    public string SaveKey => $"AttributeSet.{gameObject.name}";

    /// <summary>
    ///     生成属性快照。
    /// </summary>
    public Dictionary<string, object> CaptureSnapshot()
    {
        var attrSet = GetComponent<AttributeSet>();
        if (attrSet == null) return null;

        return new Dictionary<string, object>
        {
            ["current_health"] = attrSet.CurrentHealth,
            ["max_health"] = attrSet.MaxHealth,
            ["attack"] = attrSet.BaseAttack,
            ["defense"] = attrSet.BaseDefense,
            ["move_speed"] = attrSet.BaseMoveSpeed,
            ["attack_speed"] = attrSet.BaseAttackSpeed,
            ["crit_chance"] = attrSet.BaseCritChance,
            ["crit_damage"] = attrSet.BaseCritDamage
        };
    }

    /// <summary>
    ///     从快照恢复属性。
    ///     使用 BindableProperty.SetValueWithoutNotify 静默恢复，
    ///     不触发 UI 回调，避免加载存档时产生 UI 动画副作用。
    /// </summary>
    public void RestoreSnapshot(Dictionary<string, object> snapshot)
    {
        var attrSet = GetComponent<AttributeSet>();
        if (attrSet == null || snapshot == null) return;

        // 恢复绑定属性（静默）
        if (TryGetFloat(snapshot, "max_health", out var maxHealth))
            attrSet.BindableMaxHealth.SetValueWithoutNotify(maxHealth);

        if (TryGetFloat(snapshot, "current_health", out var currentHealth))
            attrSet.BindableHealth.SetValueWithoutNotify(
                Mathf.Clamp(currentHealth, 0f, maxHealth));

        // 恢复后统一通知一次 UI（而非每次赋值都触发）
        attrSet.BindableMaxHealth.Notify();
        attrSet.BindableHealth.Notify();
    }

    // ──────────── 辅助 ────────────

    private static bool TryGetFloat(Dictionary<string, object> dict, string key, out float value)
    {
        value = 0f;
        if (!dict.TryGetValue(key, out var obj)) return false;

        try
        {
            value = System.Convert.ToSingle(obj);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
