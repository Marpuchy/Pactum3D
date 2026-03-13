using UnityEngine;

public readonly struct DamageReceivedInfo
{
    public readonly float RequestedDamage;
    public readonly float AppliedDamage;
    public readonly GameObject Target;
    public readonly GameObject Attacker;

    public DamageReceivedInfo(
        float requestedDamage,
        float appliedDamage,
        GameObject target,
        GameObject attacker)
    {
        RequestedDamage = requestedDamage;
        AppliedDamage = appliedDamage;
        Target = target;
        Attacker = attacker;
    }
}
