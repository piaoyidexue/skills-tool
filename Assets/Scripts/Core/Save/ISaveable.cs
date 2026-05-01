using System.Collections.Generic;

// ============================================================
//  ISaveable —— 序列化契约接口
//  任何需要参与存档的模块只需实现此接口，
//  SaveManager 在保存时自动搜集所有实现者。
//
//  设计准则：
//  - 接口搜集 + 全局字典序列化：新增模块零成本接入
//  - Key 唯一性：使用 "{模块类型}.{实例标识}" 格式
//    如 "Inventory.Player"、 "AttributeSet.Boss_01"
//  - 数据隔离：每个模块只关心自己的键值对
//  - 零 GC：快照数据使用 Dictionary<string, object>，
//    仅在保存/加载时临时分配，运行时无额外开销
// ============================================================

/// <summary>
///     存档契约接口 —— 所有需要持久化的模块必须实现。
///     SaveManager 在保存时遍历场景中所有 ISaveable，
///     调用 CaptureSnapshot 收集数据；加载时通过 RestoreSnapshot 还原。
/// </summary>
public interface ISaveable
{
    /// <summary>
    ///     获取存档唯一标识符。
    ///     格式建议："{模块类型}.{实例标识}"，如 "Inventory.Player"。
    ///     必须在同一存档文件内全局唯一。
    /// </summary>
    string SaveKey { get; }

    /// <summary>
    ///     生成当前状态快照数据。
    ///     返回的字典将被序列化到存档文件中。
    ///     纯数据操作，不允许有副作用（如播放音效、刷新 UI）。
    /// </summary>
    /// <returns>当前状态的数据字典，key=字段名，value=字段值</returns>
    Dictionary<string, object> CaptureSnapshot();

    /// <summary>
    ///     根据传入的快照数据恢复状态。
    ///     应使用 SetValueWithoutNotify 避免触发 UI 回调。
    ///     恢复完成后由 SaveManager 统一触发一次 UI 刷新。
    /// </summary>
    /// <param name="snapshot">快照数据字典</param>
    void RestoreSnapshot(Dictionary<string, object> snapshot);
}
