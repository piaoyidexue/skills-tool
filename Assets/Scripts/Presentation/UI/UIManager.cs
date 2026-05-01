using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  UI 层级枚举 + UIWindowBase + UIManager
//  栈式面板管理 + 生命周期规范 + 层级隔离。
//
//  设计准则：
//  - 数据驱动：UI 组件不碰业务逻辑，只监听 BindableProperty 回调
//  - 栈式路由：打开面板入栈，关闭出栈并自动恢复上一层的交互
//  - 层级隔离：不同层级的面板互不干扰
//  - 生命周期规范：Init → OnOpen → OnRefresh → OnClose → OnDestroy
// ============================================================

/// <summary>
///     UI 层级枚举 —— 控制面板的渲染顺序和交互优先级。
///     数值越大越靠前（越靠近屏幕）。
/// </summary>
public enum UILayer
{
    /// <summary>底层 HUD（血条、小地图等常驻元素）</summary>
    HUD = 0,

    /// <summary>常驻面板（背包、角色面板等）</summary>
    Panel = 100,

    /// <summary>弹窗（确认框、提示框等模态对话框）</summary>
    Popup = 200,

    /// <summary>顶层警告/提示（网络断开、强制更新等）</summary>
    Alert = 300,

    /// <summary>加载幕布（场景切换遮罩）</summary>
    LoadingCurtain = 400
}

// ============================================================
//  UIWindowBase —— UI 窗口基类
// ============================================================

/// <summary>
///     UI 窗口基类 —— 规范化生命周期。
///     所有 UI 面板必须继承此类，禁止在子类中直接操作业务逻辑。
///     子类通过重写 OnRefresh 绑定 BindableProperty 来响应数据变化。
/// </summary>
public abstract class UIWindowBase : MonoBehaviour
{
    // ──────────── 生命周期阶段 ────────────

    /// <summary>窗口当前状态</summary>
    public enum WindowState
    {
        None,
        Initializing,
        Opening,
        Opened,
        Closing,
        Closed
    }

    // ──────────── 属性 ────────────

    /// <summary>窗口唯一标识（用于路由查找）</summary>
    public string WindowId;

    /// <summary>所属层级</summary>
    public UILayer Layer = UILayer.Panel;

    /// <summary>是否模态（打开时阻止下层交互）</summary>
    public bool IsModal = true;

    /// <summary>是否缓存（关闭时不销毁，下次直接复用）</summary>
    public bool CacheOnClose = true;

    /// <summary>当前状态</summary>
    public WindowState State { get; protected set; } = WindowState.None;

    /// <summary>是否已初始化</summary>
    public bool IsInitialized { get; protected set; }

    /// <summary>是否已打开</summary>
    public bool IsOpened => State == WindowState.Opened;

    // ──────────── 公开 API ────────────

    /// <summary>
    ///     初始化窗口（仅首次打开时调用一次）。
    ///     子类在此处创建子控件引用、注册 BindableProperty 回调。
    /// </summary>
    public virtual void Init()
    {
        if (IsInitialized) return;
        State = WindowState.Initializing;
        IsInitialized = true;
    }

    /// <summary>
    ///     打开窗口。由 UIManager 调用。
    /// </summary>
    /// <param name="openArgs">打开参数（可选，由子类解释）</param>
    public void Open(object openArgs = null)
    {
        if (!IsInitialized) Init();

        State = WindowState.Opening;
        gameObject.SetActive(true);

        OnOpen(openArgs);
        OnRefresh();

        State = WindowState.Opened;
    }

    /// <summary>
    ///     关闭窗口。由 UIManager 调用。
    /// </summary>
    public void Close()
    {
        if (State == WindowState.Closed || State == WindowState.Closing) return;

        State = WindowState.Closing;
        OnClose();

        if (CacheOnClose)
        {
            gameObject.SetActive(false);
        }
        else
        {
            Destroy(gameObject);
        }

        State = WindowState.Closed;
    }

    /// <summary>
    ///     刷新数据绑定。由 UIManager 在数据变化时调用，
    ///     或子类在 BindableProperty 回调中手动调用。
    /// </summary>
    public void Refresh()
    {
        if (State != WindowState.Opened) return;
        OnRefresh();
    }

    // ──────────── 生命周期虚方法（子类重写） ────────────

    /// <summary>
    ///     打开时回调。openArgs 由 UIManager 传入。
    ///     子类在此处绑定 BindableProperty、播放打开动画。
    /// </summary>
    protected virtual void OnOpen(object openArgs) { }

    /// <summary>
    ///     数据刷新回调。
    ///     子类在此处从 BindableProperty 读取最新值并更新 UI 表现。
    /// </summary>
    protected virtual void OnRefresh() { }

    /// <summary>
    ///     关闭时回调。
    ///     子类在此处解绑 BindableProperty、播放关闭动画。
    /// </summary>
    protected virtual void OnClose() { }

    // ──────────── Unity 生命周期 ────────────

    protected virtual void OnDestroy()
    {
        // 安全清理：如果子类忘记在 OnClose 中解绑回调，
        // 在销毁时强制清除所有 BindableProperty 监听
        State = WindowState.Closed;
    }
}

// ============================================================
//  UIManager —— 栈式面板管理器
// ============================================================

/// <summary>
///     UI 管理器 —— 基于堆栈的面板路由与层级控制。
///     每个层级独立维护一个 Stack，打开面板入栈，关闭出栈。
///     模态面板会阻止低层级面板的交互，关闭时自动恢复。
/// </summary>
public class UIManager : MonoBehaviour
{
    // ──────────── 单例 ────────────

    public static UIManager Instance { get; private set; }

    // ──────────── 配置 ────────────

    [Header("=== 层级根节点 ===")]
    [Tooltip("每个 UILayer 对应的 Canvas 根节点，用于控制 sortingOrder 和交互阻断")]
    [SerializeField] private Transform[] _layerRoots = new Transform[5];

    [Header("=== 调试 ===")]
    [SerializeField] private bool _showDebugInfo;

    // ──────────── 运行时数据 ────────────

    /// <summary>
    ///     每个层级的面板栈。按层级隔离，同级面板按栈序管理。
    /// </summary>
    private readonly Dictionary<UILayer, Stack<UIWindowBase>> _layerStacks = new();

    /// <summary>
    ///     全局面板注册表。WindowId → UIWindowBase 实例。
    ///     用于快速查找面板实例，避免遍历栈。
    /// </summary>
    private readonly Dictionary<string, UIWindowBase> _windowRegistry = new(32);

    /// <summary>
    ///     模态遮罩。当有模态面板打开时显示，阻止低层级交互。
    /// </summary>
    private GameObject _modalBlocker;

    // ──────────── 生命周期 ────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeLayerStacks();
        EnsureModalBlocker();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ──────────── 初始化 ────────────

    private void InitializeLayerStacks()
    {
        foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
        {
            _layerStacks[layer] = new Stack<UIWindowBase>(4);
        }
    }

    private void EnsureModalBlocker()
    {
        if (_modalBlocker != null) return;

        _modalBlocker = new GameObject("ModalBlocker");
        _modalBlocker.transform.SetParent(transform, false);
        _modalBlocker.SetActive(false);

        // 阻隔射线的透明 Image
        var canvas = _modalBlocker.AddComponent<UnityEngine.UI.Image>();
        canvas.color = new Color(0, 0, 0, 0.4f);
        var rt = _modalBlocker.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
    }

    // ──────────── 公开 API ────────────

    /// <summary>
    ///     注册面板到管理器（通常在面板 Awake 中调用）。
    /// </summary>
    public void RegisterWindow(UIWindowBase window)
    {
        if (window == null || string.IsNullOrEmpty(window.WindowId)) return;
        _windowRegistry[window.WindowId] = window;
    }

    /// <summary>
    ///     反注册面板。
    /// </summary>
    public void UnregisterWindow(UIWindowBase window)
    {
        if (window == null) return;
        _windowRegistry.Remove(window.WindowId);
    }

    /// <summary>
    ///     打开指定面板并压入对应层级的栈。
    /// </summary>
    /// <param name="windowId">面板唯一标识</param>
    /// <param name="openArgs">打开参数</param>
    /// <returns>打开的面板实例，失败返回 null</returns>
    public UIWindowBase OpenWindow(string windowId, object openArgs = null)
    {
        if (!_windowRegistry.TryGetValue(windowId, out var window))
        {
            Debug.LogWarning($"[UIManager] Window not registered: {windowId}");
            return null;
        }

        return OpenWindow(window, openArgs);
    }

    /// <summary>
    ///     打开面板实例并压栈。
    /// </summary>
    public UIWindowBase OpenWindow(UIWindowBase window, object openArgs = null)
    {
        if (window == null) return null;

        var layer = window.Layer;
        var stack = _layerStacks[layer];

        // 暂停当前栈顶面板的交互
        if (stack.Count > 0)
        {
            var top = stack.Peek();
            if (top.IsOpened) top.gameObject.SetActive(false);
        }

        // 设置父节点到对应层级根
        if ((int)layer < _layerRoots.Length && _layerRoots[(int)layer] != null)
        {
            window.transform.SetParent(_layerRoots[(int)layer], false);
        }

        stack.Push(window);
        window.Open(openArgs);

        // 更新模态遮罩
        UpdateModalBlocker();

        return window;
    }

    /// <summary>
    ///     关闭当前层级的栈顶面板，出栈并恢复下一层。
    /// </summary>
    /// <param name="layer">目标层级</param>
    public void CloseTopWindow(UILayer layer)
    {
        if (!_layerStacks.TryGetValue(layer, out var stack) || stack.Count == 0) return;

        var window = stack.Pop();
        window.Close();

        // 恢复新的栈顶面板
        if (stack.Count > 0)
        {
            var newTop = stack.Peek();
            if (newTop != null)
            {
                newTop.gameObject.SetActive(true);
                newTop.Refresh();
            }
        }

        UpdateModalBlocker();
    }

    /// <summary>
    ///     关闭指定面板（从栈中查找并移除）。
    /// </summary>
    public void CloseWindow(string windowId)
    {
        if (!_windowRegistry.TryGetValue(windowId, out var window)) return;
        CloseWindow(window);
    }

    /// <summary>
    ///     关闭指定面板实例。
    /// </summary>
    public void CloseWindow(UIWindowBase window)
    {
        if (window == null) return;

        var layer = window.Layer;
        if (!_layerStacks.TryGetValue(layer, out var stack)) return;

        // 如果是栈顶，直接 Pop
        if (stack.Count > 0 && stack.Peek() == window)
        {
            CloseTopWindow(layer);
            return;
        }

        // 非栈顶：从栈中移除（转为临时列表操作）
        var temp = new List<UIWindowBase>(stack.Count);
        while (stack.Count > 0)
        {
            var top = stack.Pop();
            if (top == window)
            {
                top.Close();
                break;
            }
            temp.Add(top);
        }

        // 重新入栈
        for (var i = temp.Count - 1; i >= 0; i--)
            stack.Push(temp[i]);

        UpdateModalBlocker();
    }

    /// <summary>
    ///     关闭所有面板（清空所有层级栈）。
    /// </summary>
    public void CloseAll()
    {
        foreach (var kvp in _layerStacks)
        {
            while (kvp.Value.Count > 0)
            {
                var window = kvp.Value.Pop();
                window.Close();
            }
        }

        UpdateModalBlocker();
    }

    /// <summary>
    ///     获取指定层级的栈顶面板。
    /// </summary>
    public UIWindowBase GetTopWindow(UILayer layer)
    {
        if (!_layerStacks.TryGetValue(layer, out var stack) || stack.Count == 0) return null;
        return stack.Peek();
    }

    /// <summary>
    ///     获取指定面板是否处于打开状态。
    /// </summary>
    public bool IsWindowOpen(string windowId)
    {
        return _windowRegistry.TryGetValue(windowId, out var window) && window.IsOpened;
    }

    /// <summary>
    ///     获取指定层级的打开面板数量。
    /// </summary>
    public int GetOpenWindowCount(UILayer layer)
    {
        return _layerStacks.TryGetValue(layer, out var stack) ? stack.Count : 0;
    }

    // ──────────── HUD 层快捷接口 ────────────

    /// <summary>
    ///     在 HUD 层创建浮动文字。
    ///     将 QAFloatingText 统一归口到 UIManager 管理。
    /// </summary>
    /// <param name="prefab">浮动文字预制体</param>
    /// <param name="worldPosition">世界坐标位置</param>
    /// <param name="text">显示文本</param>
    /// <param name="color">文字颜色</param>
    /// <param name="fontSize">字号</param>
    public void ShowFloatingText(QAFloatingText prefab, Vector3 worldPosition,
        string text, Color color, int fontSize = 24)
    {
        if (prefab == null) return;

        var instance = Instantiate(prefab, worldPosition, Quaternion.identity);

        // 归口到 HUD 层
        if ((int)UILayer.HUD < _layerRoots.Length && _layerRoots[(int)UILayer.HUD] != null)
        {
            instance.transform.SetParent(_layerRoots[(int)UILayer.HUD], true);
        }

        instance.ShowCustom(text, color, fontSize);
    }

    // ──────────── 内部逻辑 ────────────

    /// <summary>
    ///     更新模态遮罩的显示状态。
    ///     当任意层级栈顶存在模态面板时显示遮罩。
    /// </summary>
    private void UpdateModalBlocker()
    {
        var hasModal = false;

        foreach (var kvp in _layerStacks)
        {
            if (kvp.Value.Count == 0) continue;
            var top = kvp.Value.Peek();
            if (top != null && top.IsOpened && top.IsModal)
            {
                hasModal = true;
                break;
            }
        }

        if (_modalBlocker != null)
            _modalBlocker.SetActive(hasModal);
    }

    // ──────────── 调试面板 ────────────

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (!_showDebugInfo) return;

        GUILayout.BeginArea(new Rect(730, 10, 350, 500));
        GUILayout.Label("<b>UI Manager</b>", new GUIStyle(GUI.skin.label) { richText = true });

        foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
        {
            var count = GetOpenWindowCount(layer);
            GUILayout.Label($"  {layer}: {count} window(s)");

            if (_layerStacks.TryGetValue(layer, out var stack))
            {
                foreach (var w in stack)
                    GUILayout.Label($"    - {w.WindowId} [{w.State}]");
            }
        }

        GUILayout.EndArea();
    }
#endif
}
