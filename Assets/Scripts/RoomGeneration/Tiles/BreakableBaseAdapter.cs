using System;
using UnityEngine;

[RequireComponent(typeof(BreakableBase))]
public sealed class BreakableDamageAdapter : MonoBehaviour, IDamageable, INonConsumableHit
{
    private BreakableBase breakable;
    public event Action<DamageReceivedInfo> DamageReceived;

    private void Awake()
    {
        breakable = GetComponent<BreakableBase>();
    }

    public void TakeDamage(float damage, GameObject attacker)
    {
        if (damage <= 0f) return;

        breakable.ApplyDamage(Mathf.CeilToInt(1));
        DamageReceived?.Invoke(new DamageReceivedInfo(
            damage,
            1f,
            gameObject,
            attacker));
    }
}
