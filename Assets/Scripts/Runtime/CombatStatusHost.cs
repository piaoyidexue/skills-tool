using System.Collections.Generic;
using UnityEngine;

public class CombatStatusHost : MonoBehaviour, IStatusReceiver
{
    [SerializeField] private float burnTickInterval = 1f;

    private readonly Dictionary<StatusType, StatusRuntime> _statuses = new();
    private float _burnTickTimer;

    private void Update()
    {
        var delta = Time.deltaTime;
        _burnTickTimer += delta;

        foreach (var pair in _statuses)
        {
            if (pair.Value.IsActive)
            {
                pair.Value.Remaining -= delta;
            }
        }

        if (_burnTickTimer >= burnTickInterval)
        {
            _burnTickTimer = 0f;
            TickBurn();
        }
    }

    public void ApplyStatus(StatusRuntime status)
    {
        if (status == null || status.Type == StatusType.None)
        {
            return;
        }

        if (!_statuses.TryGetValue(status.Type, out var existing))
        {
            existing = new StatusRuntime { Type = status.Type };
            _statuses[status.Type] = existing;
        }

        existing.Reset(status.Value, status.Duration, status.SourceTag, status.Instigator);
    }

    public bool HasStatus(StatusType type)
    {
        return TryGetStatus(type, out _);
    }

    public bool TryGetStatus(StatusType type, out StatusRuntime status)
    {
        if (_statuses.TryGetValue(type, out status) && status.IsActive)
        {
            return true;
        }

        status = null;
        return false;
    }

    public bool ConsumeStatus(StatusType type, out StatusRuntime status)
    {
        if (TryGetStatus(type, out status))
        {
            status.Remaining = 0f;
            return true;
        }

        return false;
    }

    public IReadOnlyCollection<StatusRuntime> GetActiveStatuses()
    {
        var result = new List<StatusRuntime>();
        foreach (var pair in _statuses)
        {
            if (pair.Value.IsActive)
            {
                result.Add(pair.Value);
            }
        }

        return result;
    }

    public float GetDamageTakenMultiplier()
    {
        var multiplier = 1f;
        if (HasStatus(StatusType.Mark))
        {
            multiplier += 0.15f;
        }

        if (HasStatus(StatusType.Chill))
        {
            multiplier += 0.1f;
        }

        if (HasStatus(StatusType.Freeze))
        {
            multiplier += 0.2f;
        }

        return multiplier;
    }

    public bool IsCrowdControlled()
    {
        return HasStatus(StatusType.Freeze) || HasStatus(StatusType.Stun) || HasStatus(StatusType.Root);
    }

    private void TickBurn()
    {
        if (!TryGetStatus(StatusType.Burn, out var burn))
        {
            return;
        }

        var damageable = GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(Mathf.Max(1f, burn.Value), burn.Instigator);
        }
    }
}
