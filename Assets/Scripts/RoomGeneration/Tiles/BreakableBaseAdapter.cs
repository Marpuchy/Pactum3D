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
        if (!IsPlayerAttacker(attacker)) return;

        breakable.ApplyDamage(Mathf.CeilToInt(1));
        DamageReceived?.Invoke(new DamageReceivedInfo(
            damage,
            1f,
            gameObject,
            attacker));
    }

    private static bool IsPlayerAttacker(GameObject attacker)
    {
        if (attacker == null)
            return false;

        if (attacker.CompareTag("Player"))
            return true;

        Transform root = attacker.transform.root;
        return root != null && root.CompareTag("Player");
    }
}
