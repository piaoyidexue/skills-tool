using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  全局事件总线 (GlobalEventBus)
//  基于结构体（Struct）和泛型接口的发布/订阅模型。
//  利用类型本身作为事件过滤的 Key（不需要字符串字典），
//  内部使用委托链管理订阅者。
//
//  设计准则：
//  - 0 GC：事件数据使用 struct 传递，按值拷贝无堆分配
//  - 类型安全：泛型匹配在编译期确保类型正确，无装箱拆箱
//  - 模块解耦：发布者与订阅者仅通过 struct 类型耦合，无接口依赖
//  - 安全注销：MonoBehaviour 必须在 OnDisable/OnDestroy 注销
// ============================================================

/// <summary>
///     全局事件总线 —— 零 GC、类型安全的发布/订阅模型。
///     使用泛型类型作为事件 Key，内部用委托链管理订阅者。
///     事件数据必须是 struct，按值传递避免堆分配。
/// </summary>
public static class GlobalEventBus
{
    // ──────────── 内部存储 ────────────

    /// <summary>
    ///     泛型事件订阅者字典。
    ///     Key: 事件类型（typeof(T)），Value: 委托链。
    ///     使用 Dictionary<Type, Delegate> 而非 Dictionary<Type, List<...>>，
    ///     因为 Delegate.Combine/Remove 本身就是高效的委托链操作。
    /// </summary>
    private static readonly Dictionary<Type, Delegate> Subscribers = new(64);

    // ──────────── 订阅 API ────────────

    /// <summary>
    ///     订阅指定类型的事件。
    ///     泛型参数 T 同时充当 Key 和数据载体，编译期类型安全。
    /// </summary>
    /// <typeparam name="T">事件数据类型（必须是 struct）</typeparam>
    /// <param name="handler">事件回调，接收 struct 按值传递</param>
    public static void Subscribe<T>(Action<T> handler) where T : struct
    {
        if (handler == null)
        {
            Debug.LogWarning("[GlobalEventBus] Cannot subscribe with null handler.");
            return;
        }

        var eventType = typeof(T);
        if (Subscribers.TryGetValue(eventType, out var existing))
        {
            Subscribers[eventType] = Delegate.Combine(existing, handler);
        }
        else
        {
            Subscribers[eventType] = handler;
        }
    }

    /// <summary>
    ///     取消订阅指定类型的事件。
    ///     任何在 OnEnable 中注册的 MonoBehaviour 必须在 OnDisable 或 OnDestroy 中调用此方法。
    /// </summary>
    public static void Unsubscribe<T>(Action<T> handler) where T : struct
    {
        if (handler == null) return;

        var eventType = typeof(T);
        if (!Subscribers.TryGetValue(eventType, out var existing)) return;

        var updated = Delegate.Remove(existing, handler);
        if (updated == null)
        {
            Subscribers.Remove(eventType);
        }
        else
        {
            Subscribers[eventType] = updated;
        }
    }

    // ──────────── 无参数事件支持 ────────────

    /// <summary>
    ///     订阅无数据事件（纯信号型事件，如场景加载完成）。
    ///     内部使用 Unit 空结构体作为载体，保持 API 一致性。
    ///     自动维护 Action → Action&lt;Unit&gt; 映射，支持正确注销。
    /// </summary>
    public static void Subscribe(Action handler)
    {
        if (handler == null) return;
        Action<Unit> wrapper = _ => handler();
        _actionWrappers[handler] = wrapper;
        Subscribe(wrapper);
    }

    /// <summary>
    ///     取消订阅无数据事件。
    /// </summary>
    public static void Unsubscribe(Action handler)
    {
        if (handler == null) return;
        if (!_actionWrappers.TryGetValue(handler, out var wrapper)) return;
        Unsubscribe(wrapper);
        _actionWrappers.Remove(handler);
    }

    // ──────────── 发布 API ────────────

    /// <summary>
    ///     派发事件。
    ///     接收泛型 struct 实例，按值传递给所有监听者。
    ///     派发过程中异常不会中断后续订阅者的调用。
    /// </summary>
    /// <typeparam name="T">事件数据类型</typeparam>
    /// <param name="eventData">事件数据（struct 按值传递，0 GC）</param>
    public static void Publish<T>(T eventData) where T : struct
    {
        var eventType = typeof(T);
        if (!Subscribers.TryGetValue(eventType, out var del)) return;

        // 委托链按序调用，异常隔离
        var handlers = del as Action<T>;
        if (handlers == null) return;

        var invocationList = handlers.GetInvocationList();
        for (var i = 0; i < invocationList.Length; i++)
        {
            try
            {
                ((Action<T>)invocationList[i])(eventData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GlobalEventBus] Exception in handler for {eventType.Name}: {ex}");
            }
        }
    }

    /// <summary>
    ///     派发无数据事件（纯信号型）。
    /// </summary>
    public static void Publish()
    {
        Publish(default(Unit));
    }

    // ──────────── 查询 API ────────────

    /// <summary>
    ///     检查指定事件类型是否有订阅者。
    /// </summary>
    public static bool HasSubscribers<T>() where T : struct
    {
        return Subscribers.ContainsKey(typeof(T));
    }

    /// <summary>
    ///     获取指定事件类型的订阅者数量。
    /// </summary>
    public static int GetSubscriberCount<T>() where T : struct
    {
        if (!Subscribers.TryGetValue(typeof(T), out var del)) return 0;
        return del.GetInvocationList().Length;
    }

    // ──────────── 清理 API ────────────

    /// <summary>
    ///     清除指定事件类型的所有订阅者。
    /// </summary>
    public static void ClearSubscribers<T>() where T : struct
    {
        Subscribers.Remove(typeof(T));
    }

    /// <summary>
    ///     清除所有订阅（场景切换时调用）。
    /// </summary>
    public static void ClearAll()
    {
        Subscribers.Clear();
        _actionWrappers.Clear();
    }

    /// <summary>
    ///     无参事件包装器映射，用于支持 Action 订阅/注销。
    ///     Key: 原始 Action，Value: 包装后的 Action&lt;Unit&gt;
    /// </summary>
    private static readonly Dictionary<Action, Action<Unit>> _actionWrappers = new(32);

    // ──────────── 内部类型 ────────────

    /// <summary>
    ///     空结构体，用于无数据事件的载体。
    /// </summary>
    private struct Unit { }
}
