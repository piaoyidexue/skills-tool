using UnityEngine;

/// <summary>
///     靶子实体 —— 受击目标，使用 GEHost 接管状态和伤害倍率计算。
/// </summary>
[RequireComponent(typeof(GEHost))]
public class TargetDummy : MonoBehaviour, IDamageable
{
    [SerializeField] private float health = 1000f;

    private GEHost _geHost;

    private void Awake()
    {
        _geHost = GetComponent<GEHost>();
    }

    public void TakeDamage(float amount, Transform instigator)
    {
        var finalAmount = amount;
        if (_geHost != null)
        {
            // GAS架构：通过 GEHost.EvaluateAttribute 获取受伤倍率
            var multiplier = _geHost.EvaluateAttribute(GEAttribute.DamageTakenMultiplier, 1f);
            finalAmount *= multiplier;
        }

        health -= finalAmount;
        var sourceName = instigator != null ? instigator.name : "Unknown";
        Debug.Log($"<color=red><b>[受击]</b></color> {gameObject.name} 受到 {finalAmount:F1} 点伤害! 来源: {sourceName} 剩余生命: {health:F1}");

        // 检查硬控状态（通过 Tag 查询）
        if (_geHost != null && (_geHost.HasTag("stun") || _geHost.HasTag("freeze") || _geHost.HasTag("root")))
        {
            Debug.Log($"<color=cyan>[控制]</color> {gameObject.name} 当前处于控制状态。");
        }

        // 死亡判定
        if (health <= 0f)
        {
            // 全局事件总线：抛出实体死亡事件
            GlobalEventBus.Publish(new EntityDeathEvent
            {
                Entity = transform,
                Killer = instigator,
                OverkillDamage = -health
            });
        }
    }
}
