using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     QA 性能监控器 —— 运行时 UI 面板，监控 FPS、内存、对象池、VFX 对象数量。
///     支持：实时折线图、泄露报警、自定义标记面板。
///     使用 Unity UI 在 Screen 空间绘制（无需额外包）。
/// </summary>
public class QAPerformanceMonitor : MonoBehaviour
{
    public static QAPerformanceMonitor Instance { get; private set; }

    [Header("=== 显示开关 ===")]
    [SerializeField] private bool _showFps = true;
    [SerializeField] private bool _showMemory = true;
    [SerializeField] private bool _showPool = true;
    [SerializeField] private bool _showSkillExecutions = true;
    [SerializeField] private bool _showVFXPool = true;
    [SerializeField] private bool _enableChart = true;
    [SerializeField] private bool _showAlertOnLeak = true;

    [Header("=== 面板外观 ===")]
    [SerializeField] private Color _panelBg = new(0f, 0f, 0f, 0.75f);
    [SerializeField] private Color _normalColor = new(0.6f, 0.9f, 0.6f);
    [SerializeField] private Color _warnColor = new(1f, 0.8f, 0.2f);
    [SerializeField] private Color _alertColor = new(1f, 0.2f, 0.2f);
    [SerializeField] private Color _goodColor = new(0.2f, 0.9f, 0.3f);

    [Header("=== 图表参数 ===")]
    [SerializeField] private int _chartHistory = 120;  // 记录 120 帧
    [SerializeField] private float _chartHeight = 60f;
    [SerializeField] private float _chartWidth = 200f;

    // ---- 帧率计算 ----
    private float _fps;
    private float _fpsUpdateTimer;
    private int _frameCount;

    // ---- 历史记录 ----
    private readonly List<float> _fpsHistory = new();
    private readonly List<float> _memHistory = new();
    private readonly List<int> _poolHistory = new();

    // ---- 上一次采样的对象数（用于泄露检测）----
    private int _lastPoolActiveCount;
    private int _leakAlertFrames;
    private bool _isLeakAlert;

    // ---- 自定义计数 ----
    private readonly Dictionary<string, float> _customCounters = new();
    private readonly Dictionary<string, Color> _customColors = new();

    // ---- GUIStyle 缓存 ----
    private GUIStyle _labelStyle;
    private GUIStyle _boxStyle;
    private bool _stylesInit;
    private Texture2D _bgTex;
    private Texture2D _chartLineTex;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        _frameCount++;
        _fpsUpdateTimer += Time.deltaTime;

        if (_fpsUpdateTimer >= 0.25f) // 每 0.25s 刷新一次 FPS
        {
            _fps = _frameCount / _fpsUpdateTimer;
            _frameCount = 0;
            _fpsUpdateTimer = 0f;

            RecordSample();
            DetectLeak();
        }
    }

    private void RecordSample()
    {
        var memMB = GC.GetTotalMemory(false) / 1_048_576f;

        _fpsHistory.Add(_fps);
        _memHistory.Add(memMB);

        var poolCount = GetVFXPoolCount();
        _poolHistory.Add(poolCount);

        // 限制历史长度
        while (_fpsHistory.Count > _chartHistory) _fpsHistory.RemoveAt(0);
        while (_memHistory.Count > _chartHistory) _memHistory.RemoveAt(0);
        while (_poolHistory.Count > _chartHistory) _poolHistory.RemoveAt(0);
    }

    private void DetectLeak()
    {
        var current = GetVFXPoolCount();
        // 连续 5 秒对象数持续增长（每次都比上次多 10 个以上）→ 疑似泄露
        if (_poolHistory.Count >= 20)
        {
            var trend = 0;
            for (var i = 1; i < _poolHistory.Count; i++)
                trend += _poolHistory[i] - _poolHistory[i - 1];

            _leakAlertFrames = trend > 50 ? _leakAlertFrames + 1 : 0;
            _isLeakAlert = _leakAlertFrames >= 5;
        }
        _lastPoolActiveCount = current;
    }

    private void InitStyles()
    {
        if (_stylesInit) return;

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            richText = true,
            alignment = TextAnchor.UpperLeft
        };

        _boxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 12
        };

        // 创建纯色背景纹理
        _bgTex = MakeTex(1, 1, _panelBg);
        _chartLineTex = MakeTex(1, 1, _goodColor);

        _stylesInit = true;
    }

    private static Texture2D MakeTex(int w, int h, Color col)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var pixels = new Color[w * h];
        for (var i = 0; i < pixels.Length; i++) pixels[i] = col;
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private void OnGUI()
    {
        if (!_showFps && !_showMemory && !_showPool) return;

        InitStyles();

        var panelW = 280f;
        var panelH = _showFps && _enableChart ? 420f : (_showFps ? 60f : 0f) +
                                                                      (_showMemory ? 60f : 0f) +
                                                                      (_showPool ? 60f : 0f) +
                                                                      (_showSkillExecutions ? 30f : 0f) +
                                                                      (_showVFXPool ? 60f : 0f) +
                                                                      (_customCounters.Count > 0 ? _customCounters.Count * 22f : 0f);

        GUILayout.BeginArea(new Rect(10, Screen.height - panelH - 10, panelW, panelH));

        // 背景框
        var bgStyle = new GUIStyle { normal = { background = _bgTex } };
        GUI.Box(new Rect(0, 0, panelW, panelH), GUIContent.none, bgStyle);

        var y = 8f;
        var lineH = 18f;

        // FPS 行
        if (_showFps)
        {
            var fpsColor = _fps >= 55 ? _goodColor : (_fps >= 30 ? _warnColor : _alertColor);
            var fpsStyle = new GUIStyle(_labelStyle)
            {
                normal = { textColor = fpsColor },
                fontStyle = FontStyle.Bold
            };
            GUI.Label(new Rect(8, y, panelW - 16, lineH),
                $"FPS: <b>{_fps:F0}</b> ({_fps:F1}ms)", fpsStyle);
            y += lineH;

            // FPS 图表
            if (_enableChart && _fpsHistory.Count > 1)
            {
                DrawLineChart(_fpsHistory, new Rect(8, y, _chartWidth, _chartHeight),
                    _alertColor, _goodColor, 60f, 0f);
                y += _chartHeight + 6f;
            }
        }

        // 内存行
        if (_showMemory)
        {
            var memMB = GC.GetTotalMemory(false) / 1_048_576f;
            var memColor = memMB < 200 ? _normalColor : _warnColor;
            GUI.Label(new Rect(8, y, panelW - 16, lineH),
                $"GC Memory: <color=#{ToHex(memColor)}>{memMB:F1} MB</color>", _labelStyle);
            y += lineH;

            if (_enableChart && _memHistory.Count > 1)
            {
                DrawLineChart(_memHistory, new Rect(8, y, _chartWidth, _chartHeight),
                    _alertColor, _normalColor, 500f, 0f);
                y += _chartHeight + 6f;
            }
        }

        // 对象池行
        if (_showPool)
        {
            var poolCount = GetVFXPoolCount();
            var inactive = GetPoolInactive();
            var poolColor = !_isLeakAlert ? _normalColor : _alertColor;
            GUI.Label(new Rect(8, y, panelW - 16, lineH),
                $"VFX Pool: <color=#{ToHex(poolColor)}>{poolCount}</color> (Active) ({inactive})",
                _labelStyle);
            y += lineH;

            if (_enableChart && _poolHistory.Count > 1)
            {
                DrawLineChartInt(_poolHistory, new Rect(8, y, _chartWidth, _chartHeight),
                    _alertColor, _goodColor, 200f, 0f);
                y += _chartHeight + 6f;
            }

            if (_isLeakAlert && _showAlertOnLeak)
            {
                var alertStyle = new GUIStyle(_labelStyle) { fontStyle = FontStyle.Bold };
                GUI.Label(new Rect(8, y, panelW - 16, lineH + 4f),
                    $"<color=red>⚠️ 对象池疑似泄露！</color>", alertStyle);
                y += lineH + 4f;
            }
        }

        // 技能执行数
        if (_showSkillExecutions)
        {
            var execCount = SkillTickManager.Instance?.ActiveCount ?? 0;
            GUI.Label(new Rect(8, y, panelW - 16, lineH),
                $"Active Executions: {execCount}", _labelStyle);
            y += lineH;
        }

        // 自定义计数
        foreach (var kv in _customCounters)
        {
            var c = _customColors.TryGetValue(kv.Key, out var col) ? col : _normalColor;
            GUI.Label(new Rect(8, y, panelW - 16, lineH),
                $"<color=#{ToHex(c)}>{kv.Key}: {kv.Value:F0}</color>", _labelStyle);
            y += lineH;
        }

        GUILayout.EndArea();
    }

    /// <summary>绘制 int 类型折线图。</summary>
    private void DrawLineChartInt(List<int> data, Rect rect, Color lineColor, Color fillColor,
        float maxValue, float minValue)
    {
        if (data.Count < 2) return;

        var bgTex = MakeTex(1, 1, new Color(0, 0, 0, 0.5f));
        GUI.Box(rect, GUIContent.none, new GUIStyle { normal = { background = bgTex } });

        var max = maxValue > 0 ? maxValue : float.MinValue;
        var min = minValue;
        if (maxValue <= 0)
        {
            for (var i = 0; i < data.Count; i++)
            {
                if (data[i] > max) max = data[i];
                if (data[i] < min) min = data[i];
            }
            max = Mathf.Max(max, min + 0.001f);
        }

        var w = rect.width / (data.Count - 1);
        var range = max - min;

        var lineTex = MakeTex(1, 1, lineColor);
        for (var i = 0; i < data.Count - 1; i++)
        {
            var x1 = rect.x + i * w;
            var x2 = rect.x + (i + 1) * w;
            var y1 = rect.y + rect.height * (1f - (data[i] - min) / range);
            var y2 = rect.y + rect.height * (1f - (data[i + 1] - min) / range);

            DrawLine(new Vector3(x1, y1, 0), new Vector3(x2, y2, 0), lineTex, 2f);
        }

        var lastX = rect.x + (data.Count - 1) * w;
        var lastY = rect.y + rect.height * (1f - (data[data.Count - 1] - min) / range);
        GUI.color = lineColor;
        GUI.DrawTexture(new Rect(lastX - 3, lastY - 3, 6, 6), MakeTex(1, 1, lineColor));
        GUI.color = Color.white;
    }

    /// <summary>绘制折线图。</summary>
    private void DrawLineChart(List<float> data, Rect rect, Color lineColor, Color fillColor,
        float maxValue, float minValue)
    {
        if (data.Count < 2) return;

        var bgTex = MakeTex(1, 1, new Color(0, 0, 0, 0.5f));
        GUI.Box(rect, GUIContent.none, new GUIStyle { normal = { background = bgTex } });

        var max = maxValue > 0 ? maxValue : float.MinValue;
        var min = minValue;
        if (maxValue <= 0)
        {
            for (var i = 0; i < data.Count; i++)
            {
                if (data[i] > max) max = data[i];
                if (data[i] < min) min = data[i];
            }
            max = Mathf.Max(max, min + 0.001f);
        }

        var w = rect.width / (data.Count - 1);
        var range = max - min;

        // 绘制折线（使用线条纹理）
        var lineTex = MakeTex(1, 1, lineColor);
        for (var i = 0; i < data.Count - 1; i++)
        {
            var x1 = rect.x + i * w;
            var x2 = rect.x + (i + 1) * w;
            var y1 = rect.y + rect.height * (1f - (data[i] - min) / range);
            var y2 = rect.y + rect.height * (1f - (data[i + 1] - min) / range);

            DrawLine(new Vector3(x1, y1, 0), new Vector3(x2, y2, 0), lineTex, 2f);
        }

        // 当前值高亮点
        var lastX = rect.x + (data.Count - 1) * w;
        var lastY = rect.y + rect.height * (1f - (data[data.Count - 1] - min) / range);
        GUI.color = lineColor;
        GUI.DrawTexture(new Rect(lastX - 3, lastY - 3, 6, 6), MakeTex(1, 1, lineColor));
        GUI.color = Color.white;
    }

    private static void DrawLine(Vector3 p1, Vector3 p2, Texture tex, float width)
    {
        var vec = p2 - p1;
        var angle = Mathf.Atan2(vec.y, vec.x) * Mathf.Rad2Deg;
        GUIUtility.RotateAroundPivot(angle, p1);
        GUI.DrawTexture(new Rect(p1.x, p1.y - width / 2f, vec.magnitude, width), tex);
        GUIUtility.RotateAroundPivot(-angle, p1);
    }

    private static string ToHex(Color c)
        => $"{(int)(c.r * 255):X2}{(int)(c.g * 255):X2}{(int)(c.b * 255):X2}";

    // ===== 虚方法：子类可覆写以接入真实对象池 =====

    protected virtual int GetVFXPoolCount() => 0;
    protected virtual int GetPoolInactive() => 0;

    // ===== 公开 API =====

    /// <summary>注册自定义计数（自动每帧更新显示）。</summary>
    public void RegisterCounter(string name, Color color)
    {
        _customCounters[name] = 0f;
        _customColors[name] = color;
    }

    /// <summary>更新自定义计数值。</summary>
    public void SetCounterValue(string name, float value)
    {
        if (_customCounters.ContainsKey(name))
            _customCounters[name] = value;
    }

    public void IncrementCounter(string name, float delta = 1f)
    {
        if (_customCounters.ContainsKey(name))
            _customCounters[name] += delta;
    }

    public void ClearCounter(string name)
    {
        _customCounters.Remove(name);
        _customColors.Remove(name);
    }

    public void ClearAllCounters()
    {
        _customCounters.Clear();
        _customColors.Clear();
    }

    public bool IsLeakAlert => _isLeakAlert;
    public float CurrentFPS => _fps;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_bgTex != null) Destroy(_bgTex);
        if (_chartLineTex != null) Destroy(_chartLineTex);
    }
}
