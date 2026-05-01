using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  HealthBarUI —— 数据驱动血条组件
//  演示 BindableProperty 的正确使用模式：
//  - OnEnable 时注册 BindableHealth.OnChanged 回调
//  - OnDisable 时注销回调（防止泄漏）
//  - 回调中更新进度条，绝不在 Update 中轮询
//
//  设计准则：
//  - UI 组件绝对不碰业务逻辑
//  - 只负责监听数据变化并刷新表现
//  - 生命周期安全管理回调注册/注销
// ============================================================

/// <summary>
///     数据驱动血条 UI 组件。
///     在 OnEnable 中将进度条更新方法注册到 AttributeSet.BindableHealth 回调，
///     属性值变化时自动刷新 UI，无需在 Update 中轮询。
/// </summary>
public class HealthBarUI : MonoBehaviour
{
    // ──────────── Inspector 配置 ────────────

    [Header("=== UI 引用 ===")]
    [Tooltip("血条填充 Image")]
    [SerializeField] private Image _fillImage;

    [Tooltip("血条延迟填充（受伤后缓慢追赶）")]
    [SerializeField] private Image _delayFillImage;

    [Tooltip("生命值文本（可选）")]
    [SerializeField] private Text _healthText;

    [Header("=== 追赶动画 ===")]
    [Tooltip("延迟血条追赶速度")]
    [SerializeField] private float _delaySpeed = 2f;

    [Header("=== 颜色配置 ===")]
    [SerializeField] private Color _fullColor = new(0.2f, 0.9f, 0.2f, 1f);
    [SerializeField] private Color _halfColor = new(1f, 0.8f, 0.2f, 1f);
    [SerializeField] private Color _lowColor = new(1f, 0.2f, 0.2f, 1f);

    // ──────────── 运行时数据 ────────────

    private AttributeSet _attributeSet;
    private float _delayedFill = 1f;
    private float _targetFill = 1f;

    // ──────────── 公开 API ────────────

    /// <summary>
    ///     绑定 AttributeSet。
    ///     在持有 AttributeSet 的实体初始化时调用。
    /// </summary>
    public void Bind(AttributeSet attributeSet)
    {
        // 先解绑旧的
        Unbind();

        _attributeSet = attributeSet;

        if (_attributeSet != null)
        {
            // 注册回调 —— 数据驱动核心
            _attributeSet.BindableHealth.OnChanged += OnHealthChanged;
            _attributeSet.BindableMaxHealth.OnChanged += OnMaxHealthChanged;

            // 初始刷新
            OnMaxHealthChanged(_attributeSet.MaxHealth, _attributeSet.MaxHealth);
            OnHealthChanged(_attributeSet.CurrentHealth, _attributeSet.CurrentHealth);
        }
    }

    /// <summary>
    ///     解绑 AttributeSet。
    /// </summary>
    public void Unbind()
    {
        if (_attributeSet != null)
        {
            _attributeSet.BindableHealth.OnChanged -= OnHealthChanged;
            _attributeSet.BindableMaxHealth.OnChanged -= OnMaxHealthChanged;
            _attributeSet = null;
        }
    }

    // ──────────── Unity 生命周期 ────────────

    private void OnEnable()
    {
        // 如果已有绑定，重新注册回调
        if (_attributeSet != null)
        {
            _attributeSet.BindableHealth.OnChanged += OnHealthChanged;
            _attributeSet.BindableMaxHealth.OnChanged += OnMaxHealthChanged;
        }
    }

    private void OnDisable()
    {
        // 安全注销：防止禁用后回调仍被调用
        if (_attributeSet != null)
        {
            _attributeSet.BindableHealth.OnChanged -= OnHealthChanged;
            _attributeSet.BindableMaxHealth.OnChanged -= OnMaxHealthChanged;
        }
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void Update()
    {
        // 仅驱动延迟血条的追赶动画
        if (_delayFillImage != null && Mathf.Abs(_delayedFill - _targetFill) > 0.001f)
        {
            _delayedFill = Mathf.Lerp(_delayedFill, _targetFill, _delaySpeed * Time.deltaTime);
            _delayFillImage.fillAmount = _delayedFill;
        }
    }

    // ──────────── BindableProperty 回调 ────────────

    /// <summary>
    ///     生命值变化回调 —— 由 BindableHealth.OnChanged 触发。
    ///     不包含任何业务逻辑，仅更新 UI 表现。
    /// </summary>
    private void OnHealthChanged(float newValue, float oldValue)
    {
        var maxHealth = _attributeSet?.MaxHealth ?? 1f;
        _targetFill = maxHealth > 0f ? newValue / maxHealth : 0f;

        // 立即更新主血条
        if (_fillImage != null)
        {
            _fillImage.fillAmount = _targetFill;
            _fillImage.color = GetHealthColor(_targetFill);
        }

        // 更新文本
        if (_healthText != null)
        {
            _healthText.text = $"{Mathf.CeilToInt(newValue)}/{Mathf.CeilToInt(maxHealth)}";
        }

        // 如果是扣血，延迟条稍后追赶；如果是回血，延迟条立即跟随
        if (newValue > oldValue)
        {
            _delayedFill = _targetFill;
            if (_delayFillImage != null) _delayFillImage.fillAmount = _delayedFill;
        }
    }

    /// <summary>
    ///     最大生命值变化回调 —— 由 BindableMaxHealth.OnChanged 触发。
    /// </summary>
    private void OnMaxHealthChanged(float newValue, float oldValue)
    {
        // 最大值变化时重新刷新当前血条比例
        if (_attributeSet != null)
            OnHealthChanged(_attributeSet.CurrentHealth, _attributeSet.CurrentHealth);
    }

    // ──────────── 辅助 ────────────

    private Color GetHealthColor(float ratio)
    {
        return ratio switch
        {
            > 0.5f => Color.Lerp(_halfColor, _fullColor, (ratio - 0.5f) * 2f),
            > 0f => Color.Lerp(_lowColor, _halfColor, ratio * 2f),
            _ => _lowColor
        };
    }
}
