using System;
using UnityEngine;

// ============================================================
//  BindableProperty 常用特化快捷类型
//  避免在 AttributeSet 等高频场景重复书写 BindableProperty<float> 等长名
// ============================================================

/// <summary>float 响应式属性快捷别名</summary>
[Serializable]
public class BindableFloat : BindableProperty<float>
{
    public BindableFloat() : base() { }
    public BindableFloat(float initialValue) : base(initialValue) { }
}

/// <summary>int 响应式属性快捷别名</summary>
[Serializable]
public class BindableInt : BindableProperty<int>
{
    public BindableInt() : base() { }
    public BindableInt(int initialValue) : base(initialValue) { }
}

/// <summary>bool 响应式属性快捷别名</summary>
[Serializable]
public class BindableBool : BindableProperty<bool>
{
    public BindableBool() : base() { }
    public BindableBool(bool initialValue) : base(initialValue) { }
}

/// <summary>
///     BindableProperty 扩展方法 —— 为 float 提供便捷的增减操作。
///     针对血条、护盾等"增减式"属性，减少 Value = Value + delta 的冗余写法。
/// </summary>
public static class BindablePropertyExtensions
{
    /// <summary>
    ///     对 float 绑定属性增减一个 delta，自动触发回调。
    /// </summary>
    public static void AddDelta(this BindableProperty<float> prop, float delta)
    {
        prop.Value = prop.Value + delta;
    }

    /// <summary>
    ///     对 int 绑定属性增减一个 delta，自动触发回调。
    /// </summary>
    public static void AddDelta(this BindableProperty<int> prop, int delta)
    {
        prop.Value = prop.Value + delta;
    }

    /// <summary>
    ///     对 float 绑定属性进行 Clamp 后赋值。
    /// </summary>
    public static void SetClamped(this BindableProperty<float> prop, float value, float min, float max)
    {
        prop.Value = Mathf.Clamp(value, min, max);
    }
}
