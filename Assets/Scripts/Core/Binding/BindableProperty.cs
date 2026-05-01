using System;
using System.Collections.Generic;

// ============================================================
//  BindableProperty<T> —— 响应式数据绑定包装
//  核心思路：包装一个值 + 变更回调链，赋值时自动判断新旧差异，
//  不同则触发所有注册的回调。UI 组件仅需注册回调即可感知数据变化，
//  绝不在 Update 中轮询。
//
//  设计准则：
//  - 零 GC 热路径：回调链使用 Delegate.Combine/Remove（委托链），
//    不创建 List<> 或闭包
//  - 类型安全：泛型 T，编译期检查
//  - 值相等判断：使用 EqualityComparer<T>.Default 避免对值类型的装箱
//  - 模块解耦：数据持有者与 UI 观察者仅通过回调耦合
// ============================================================

/// <summary>
///     泛型响应式属性包装。
///     内部维护一个值和一条委托链；当 Value 被赋值且新旧值不同时，
///     自动触发已注册的所有回调。
///     典型用法：
///     <code>
///     public readonly BindableProperty&lt;float&gt; Health = new(100f);
///     // UI 侧注册
///     Health.OnChanged += UpdateHealthBar;
///     // 业务侧赋值
///     Health.Value = 80f; // 自动触发 UpdateHealthBar(80f)
///     </code>
/// </summary>
/// <typeparam name="T">被包装的值类型</typeparam>
[Serializable]
public class BindableProperty<T>
{
    // ──────────── 内部数据 ────────────

    private T _value;

    // ──────────── 公开属性 ────────────

    /// <summary>
    ///     当前值。setter 中自动进行新旧值比对，
    ///     不同则触发 OnChanged 委托链。
    /// </summary>
    public T Value
    {
        get => _value;
        set
        {
            if (EqualityComparer<T>.Default.Equals(_value, value))
                return;

            var oldValue = _value;
            _value = value;
            OnChanged?.Invoke(value, oldValue);
        }
    }

    /// <summary>
    ///     值变更回调。
    ///     签名：(T newValue, T oldValue) → void。
    ///     提供新旧值以支持过渡动画等需求。
    /// </summary>
    public event Action<T, T> OnChanged;

    // ──────────── 构造 ────────────

    public BindableProperty() { _value = default; }

    public BindableProperty(T initialValue) { _value = initialValue; }

    // ──────────── 便捷 API ────────────

    /// <summary>
    ///     强制触发回调（不改变值）。
    ///     适用于 UI 初始化时需要刷新一次的场景。
    /// </summary>
    public void Notify()
    {
        OnChanged?.Invoke(_value, _value);
    }

    /// <summary>
    ///     静默赋值（不触发回调）。
    ///     适用于反序列化恢复状态等不应产生 UI 刷新的场景。
    /// </summary>
    public void SetValueWithoutNotify(T value)
    {
        _value = value;
    }

    /// <summary>
    ///     获取当前值但不触发任何回调。
    ///     等效于读取 Value 属性，语义更明确。
    /// </summary>
    public T GetValue() => _value;

    /// <summary>
    ///     是否有注册的回调。
    /// </summary>
    public bool HasListeners => OnChanged != null;

    // ──────────── 隐式转换 ────────────

    /// <summary>
    ///     隐式转换为 T，简化读取。
    ///     <code>float hp = healthBindable; // 等效于 healthBindable.Value</code>
    /// </summary>
    public static implicit operator T(BindableProperty<T> prop) => prop._value;

    // ──────────── 清理 ────────────

    /// <summary>
    ///     清除所有回调引用。
    ///     在 MonoBehaviour.OnDestroy 中调用以防止泄漏。
    /// </summary>
    public void ClearListeners()
    {
        OnChanged = null;
    }

    public override string ToString() => _value?.ToString() ?? "null";
}
