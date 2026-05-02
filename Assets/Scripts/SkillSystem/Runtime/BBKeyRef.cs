using System;
using UnityEngine;

// ============================================================
//  BBKeyRef —— 黑板键声明式引用
//  节点通过 BBKeyRef 引用黑板键，替代硬编码字符串。
//  在 Inspector 中提供下拉选择常用键 + 自定义输入。
//  与 FloatBinding/StringBinding/BoolBinding 配合使用，
//  实现节点间完全解耦：节点不知道谁写入，只声明读取哪个键。
// ============================================================

/// <summary>
///     黑板键引用类型 —— 描述引用的是预定义键还是自定义键。
/// </summary>
public enum BBKeyRefMode
{
    /// <summary>预定义键（从 BBKey 常量中选择）</summary>
    Predefined,

    /// <summary>自定义键（手动输入字符串）</summary>
    Custom
}

/// <summary>
///     黑板键声明式引用 —— 替代硬编码字符串。
///     在 Inspector 中提供下拉选择预定义键 + 自定义输入模式。
///     解析时返回最终的字符串键名。
///     典型用法：
///     <code>
///     public BBKeyRef targetKey = new(BBKeyRefMode.Predefined, BBKey.TargetList);
///     // Tick 中: var key = targetKey.Resolve(); ctx.Blackboard.GetValue&lt;object&gt;(key);
///     </code>
/// </summary>
[Serializable]
public class BBKeyRef
{
    /// <summary>引用模式</summary>
    public BBKeyRefMode Mode = BBKeyRefMode.Predefined;

    /// <summary>预定义键名（当 Mode = Predefined 时使用）</summary>
    public string PredefinedKey = BBKey.TargetList;

    /// <summary>自定义键名（当 Mode = Custom 时使用）</summary>
    public string CustomKey = string.Empty;

    public BBKeyRef() { }

    public BBKeyRef(BBKeyRefMode mode, string key)
    {
        Mode = mode;
        if (mode == BBKeyRefMode.Predefined)
            PredefinedKey = key;
        else
            CustomKey = key;
    }

    /// <summary>
    ///     解析为最终的字符串键名。
    /// </summary>
    public string Resolve()
    {
        return Mode == BBKeyRefMode.Custom ? CustomKey : PredefinedKey;
    }

    /// <summary>
    ///     隐式转换为字符串，简化读取。
    /// </summary>
    public static implicit operator string(BBKeyRef keyRef) => keyRef?.Resolve() ?? string.Empty;

    public override string ToString() => Resolve();
}
