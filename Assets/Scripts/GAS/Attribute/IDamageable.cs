using UnityEngine;

public interface IDamageable
{
    void TakeDamage(float amount, Transform instigator);
}