using System;
using System.Collections;
using UnityEngine;

/// <summary>
///     投射物 MonoBehaviour —— 沿方向/目标飞行，命中后触发 VFX + 伤害，自动回收。
///     支持直线弹道 / 追踪弹道 / 抛物线（通过 trajectory 枚举）。
///     统一走对象池，禁止直接 Instantiate/Destroy。
/// </summary>
public class Projectile : MonoBehaviour
{
    public enum TrajectoryMode
    {
        /// <summary>直线飞行</summary>
        Linear,

        /// <summary>追踪目标</summary>
        Homing,

        /// <summary>抛物线（需设置 gravity）</summary>
        Parabolic
    }

    [Header("Motion")]
    public TrajectoryMode trajectory = TrajectoryMode.Linear;

    [Tooltip("飞行速度 (m/s)")]
    public float speed = 12f;

    [Tooltip("最大存活时间（秒）")]
    public float lifetime = 5f;

    [Tooltip("命中判定距离")]
    public float hitRadius = 0.5f;

    [Tooltip("抛物线重力系数")]
    public float gravity = 9.8f;

    [Header("VFX")]
    public string impactVfxKey = "HitSpark";

    public string flyVfxKey = string.Empty;

    [Header("Damage")]
    public float damageAmount;

    public float damageMultiplier = 1f;

    public float critChance;

    // -- runtime state --
    private Vector3 _direction;
    private Transform _target;
    private float _elapsed;
    private bool _isActive;
    private Vector3 _velocity;
    private Action<Projectile> _onFinished;

    /// <summary>是否已命中</summary>
    public bool HasHit { get; private set; }

    /// <summary>命中位置</summary>
    public Vector3 HitPosition { get; private set; }

    /// <summary>命中的目标</summary>
    public Transform HitTarget { get; private set; }

    /// <summary>
    ///     从对象池取出后调用，初始化飞行参数。
    /// </summary>
    public void Launch(VFXRequest request, Action<Projectile> onFinishedCallback = null)
    {
        _isActive = true;
        HasHit = false;
        _elapsed = 0f;
        _onFinished = onFinishedCallback;
        HitTarget = null;
        HitPosition = Vector3.zero;

        speed = Mathf.Max(request.Length, 1f); // Length 复用为 speed
        damageAmount = request.Intensity > 0f ? request.Intensity : 10f;
        _direction = request.Direction.normalized;
        _target = request.Parent;
        _velocity = _direction * speed;

        // 位置
        transform.position = request.Position;
        transform.rotation = Quaternion.LookRotation(_direction);

        // 缩放（投射物本体）
        var s = Mathf.Max(request.ScaleMultiplier, 0.2f);
        transform.localScale = Vector3.one * s;

        // 飞行 VFX
        if (!string.IsNullOrWhiteSpace(flyVfxKey))
        {
            var manager = VFXManager.EnsureInstance();
            manager?.Play(new VFXRequest
            {
                VFXKey = flyVfxKey,
                StyleKey = request.StyleKey,
                Position = request.Position,
                Direction = _direction,
                Parent = transform,
                Duration = lifetime
            });
        }

        gameObject.SetActive(true);
    }

    /// <summary>
    ///     设置追踪目标。
    /// </summary>
    public void SetTarget(Transform target)
    {
        _target = target;
        if (target != null && trajectory == TrajectoryMode.Linear)
        {
            trajectory = TrajectoryMode.Homing;
        }
    }

    private void Update()
    {
        if (!_isActive || HasHit) return;

        _elapsed += Time.deltaTime;

        // 超时回收
        if (_elapsed >= lifetime)
        {
            Finish();
            return;
        }

        // 移动
        switch (trajectory)
        {
            case TrajectoryMode.Linear:
                transform.position += _velocity * Time.deltaTime;
                break;

            case TrajectoryMode.Homing:
                if (_target != null)
                {
                    var toTarget = (_target.position - transform.position).normalized;
                    _velocity = Vector3.Lerp(_velocity, toTarget * speed, Time.deltaTime * 4f).normalized * speed;
                }

                transform.position += _velocity * Time.deltaTime;
                transform.rotation = Quaternion.LookRotation(_velocity);
                break;

            case TrajectoryMode.Parabolic:
                _velocity.y -= gravity * Time.deltaTime;
                transform.position += _velocity * Time.deltaTime;
                if (_velocity.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(_velocity);
                break;
        }

        // 命中检测
        CheckHit();
    }

    private void CheckHit()
    {
        // 1. 追踪模式下距离检测
        if (trajectory == TrajectoryMode.Homing && _target != null)
        {
            var dist = Vector3.Distance(transform.position, _target.position);
            if (dist <= hitRadius * 2f)
            {
                OnHit(_target);
                return;
            }
        }

        // 2. 抛物线模式落地检测
        if (trajectory == TrajectoryMode.Parabolic && transform.position.y <= 0.05f)
        {
            OnHit(null);
            return;
        }

        // 3. 通用碰撞检测
        if (Physics.SphereCast(transform.position - _velocity.normalized * 0.1f, hitRadius,
                _velocity.normalized, out var hit, _velocity.magnitude * Time.deltaTime + 0.1f))
        {
            OnHit(hit.transform);
        }
    }

    private void OnHit(Transform hitTransform)
    {
        if (HasHit) return;
        HasHit = true;
        HitTarget = hitTransform;
        HitPosition = hitTransform != null ? hitTransform.position : transform.position;

        // 播放命中 VFX
        if (!string.IsNullOrWhiteSpace(impactVfxKey))
        {
            var manager = VFXManager.EnsureInstance();
            manager?.Play(new VFXRequest
            {
                VFXKey = impactVfxKey,
                Position = HitPosition,
                Direction = -_velocity.normalized,
                ScaleMultiplier = 1.2f,
                Duration = 0.5f
            });
        }

        // 伤害判定
        if (damageAmount > 0f && hitTransform != null)
        {
            var damageable = hitTransform.GetComponent<IDamageable>();
            if (damageable == null)
                damageable = hitTransform.GetComponentInParent<IDamageable>();

            if (damageable != null)
            {
                var isCrit = UnityEngine.Random.value < critChance;
                var finalDmg = damageAmount * damageMultiplier * (isCrit ? 2f : 1f);
                damageable.TakeDamage(finalDmg, transform);
            }
        }

        // 延迟回收
        StartCoroutine(FinishAfterDelay(0.15f));
    }

    private IEnumerator FinishAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Finish();
    }

    private void Finish()
    {
        _isActive = false;
        gameObject.SetActive(false);
        _onFinished?.Invoke(this);
    }

    private void OnDisable()
    {
        _isActive = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, hitRadius);
        if (_isActive)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, _velocity.normalized * 1.5f);
        }
    }
}
