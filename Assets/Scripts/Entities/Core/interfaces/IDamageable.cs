using System;
using UnityEngine;

public interface IDamageable
{
    event Action<DamageReceivedInfo> DamageReceived;
    void TakeDamage(float damage, GameObject attacker);
}
