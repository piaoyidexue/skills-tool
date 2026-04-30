using System.Collections;
using UnityEngine;

/// <summary>
///     战斗浮动文字 —— 显示伤害数字、暴击、治疗、Buff 效果。
///     支持：伤害（红）、暴击伤害（橙）、治疗（绿）、Buff（蓝）、状态反应（紫）。
/// </summary>
public class QAFloatingText : MonoBehaviour
{
    [Header("显示参数")]
    [SerializeField] private float _liftSpeed = 1.5f;
    [SerializeField] private float _lifetime = 1.2f;
    [SerializeField] private float _fadeStartRatio = 0.6f;
    [SerializeField] private float _randomOffsetX = 0.5f;

    [Header("文字样式")]
    [SerializeField] private int _fontSize = 24;
    [SerializeField] private Font _font;
    [SerializeField] private bool _useOutLine = true;

    private float _timer;
    private float _age;
    private Vector3 _initialPos;
    private Camera _refCamera;
    private UnityEngine.UI.Text _textComp;
    private UnityEngine.UI.Outline _outlineComp;
    private UnityEngine.UI.Shadow _shadowComp;
    private Canvas _canvas;
    private RectTransform _rect;

    private void Awake()
    {
        _refCamera = Camera.main;

        // 创建 Canvas
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = 100;

        var scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 100f;

        // 创建 Text
        var textGo = new GameObject("FloatText");
        textGo.transform.SetParent(transform);
        textGo.transform.localPosition = Vector3.zero;
        textGo.transform.localScale = Vector3.one;

        _rect = textGo.AddComponent<RectTransform>();
        _rect.sizeDelta = new Vector2(200f, 50f);

        _textComp = textGo.AddComponent<UnityEngine.UI.Text>();
        _textComp.font = _font ?? UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _textComp.fontSize = _fontSize;
        _textComp.alignment = TextAnchor.MiddleCenter;
        _textComp.raycastTarget = false;

        if (_useOutLine)
        {
            _outlineComp = textGo.AddComponent<UnityEngine.UI.Outline>();
            _outlineComp.effectColor = new Color(0f, 0f, 0f, 0.8f);
            _outlineComp.effectDistance = new Vector2(1.5f, 1.5f);
        }
    }

    /// <summary>显示伤害值。</summary>
    public void ShowDamage(float damage, bool isCritical, bool isHeal = false)
    {
        var sign = isHeal ? "+" : "-";
        var prefix = isCritical ? "暴!" : "";
        var text = $"{prefix}{sign}{damage:F0}";

        ApplyStyle(text,
            isHeal ? new Color(0.2f, 0.9f, 0.2f) : (isCritical ? new Color(1f, 0.6f, 0f) : new Color(1f, 0.2f, 0.2f)),
            isCritical ? _fontSize + 6 : _fontSize);
    }

    /// <summary>显示 Buff 挂载/移除。</summary>
    public void ShowBuff(string buffName, bool isApplied)
    {
        var text = isApplied ? $"[+{buffName}]" : $"[-{buffName}]";
        ApplyStyle(text, new Color(0.3f, 0.6f, 1f), _fontSize - 4);
    }

    /// <summary>显示元素反应。</summary>
    public void ShowReaction(string reactionName, float bonusDamage)
    {
        var text = $"{reactionName}\n+{bonusDamage:F0}";
        ApplyStyle(text, new Color(0.8f, 0.2f, 1f), _fontSize + 2);
    }

    /// <summary>显示自定义文字。</summary>
    public void ShowCustom(string text, Color color, int fontSize = 0)
    {
        ApplyStyle(text, color, fontSize > 0 ? fontSize : _fontSize);
    }

    private void ApplyStyle(string text, Color color, int fontSize)
    {
        if (_textComp == null) return;

        _textComp.text = text;
        _textComp.color = color;
        _textComp.fontSize = fontSize;
        _timer = 0f;
        _age = 0f;
        _initialPos = transform.position;
        enabled = true;

        var xOffset = UnityEngine.Random.Range(-_randomOffsetX, _randomOffsetX);
        _initialPos.x += xOffset;
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        _age += Time.deltaTime;

        // 上升
        transform.position = _initialPos + Vector3.up * (_liftSpeed * _timer);

        // 面向相机
        if (_refCamera != null)
            transform.rotation = Quaternion.LookRotation(_refCamera.transform.forward);

        // 透明度淡出
        var fadeRatio = _timer / _lifetime;
        if (fadeRatio > _fadeStartRatio)
        {
            var alpha = 1f - ((fadeRatio - _fadeStartRatio) / (1f - _fadeStartRatio));
            var c = _textComp.color;
            c.a = alpha;
            _textComp.color = c;
            if (_outlineComp != null) { c = _outlineComp.effectColor; c.a = alpha; _outlineComp.effectColor = c; }
        }

        // 缩放弹跳（轻微）
        var scale = 1f + Mathf.Sin(_timer * 8f) * 0.05f * (1f - fadeRatio);
        transform.localScale = Vector3.one * scale;

        if (_timer >= _lifetime)
        {
            Destroy(gameObject);
        }
    }
}
