using UnityEngine;

[RequireComponent(typeof(CombatStatusHost))]
public class TargetDummy : MonoBehaviour, IDamageable
{
    [SerializeField] private float health = 1000f;

    private CombatStatusHost _statusHost;

    private void Awake()
    {
        _statusHost = GetComponent<CombatStatusHost>();
    }

    public void TakeDamage(float amount, Transform instigator)
    {
        var finalAmount = amount;
        if (_statusHost != null)
        {
            finalAmount *= _statusHost.GetDamageTakenMultiplier();
        }

        health -= finalAmount;
        var sourceName = instigator != null ? instigator.name : "Unknown";
        Debug.Log($"<color=red><b>[受击]</b></color> {gameObject.name} 受到 {finalAmount:F1} 点伤害! 来源: {sourceName} 剩余生命: {health:F1}");
        if (_statusHost != null && _statusHost.IsCrowdControlled())
        {
            Debug.Log($"<color=cyan>[控制]</color> {gameObject.name} 当前处于控制状态。");
        }
    }
}
