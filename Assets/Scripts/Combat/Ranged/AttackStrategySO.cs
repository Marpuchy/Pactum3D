using UnityEngine;

public abstract class AttackStrategySO : ScriptableObject
{
    public abstract void Execute(in AttackRequest request, AttackExecutionContext context);
}

public readonly struct AttackExecutionContext
{
    public readonly IProjectileSpawner Spawner;
    public readonly float DamageOverride;

    public AttackExecutionContext(IProjectileSpawner spawner, float damageOverride)
    {
        Spawner = spawner;
        DamageOverride = damageOverride;
    }
}
