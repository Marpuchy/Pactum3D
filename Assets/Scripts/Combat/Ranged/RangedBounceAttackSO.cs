using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Ranged Bounce Attack", fileName = "RangedBounceAttack")]
public sealed class RangedBounceAttackSO : AttackStrategySO
{
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private float speed = 12f;
    [SerializeField] private float damage = 1f;
    [SerializeField] private int maxBounces = 2;
    [SerializeField] private float lifeSeconds = 2f;
    [SerializeField] private float spawnOffset = 0.5f;

    public override void Execute(in AttackRequest request, AttackExecutionContext context)
    {
        if (projectilePrefab == null || context.Spawner == null)
            return;

        Vector2 direction = request.Direction.sqrMagnitude > 0f ? request.Direction.normalized : Vector2.right;
        Vector3 spawnPosition = request.Origin + direction * spawnOffset;

        Projectile projectile = context.Spawner.Spawn(projectilePrefab, spawnPosition, Quaternion.identity);
        if (projectile == null)
            return;

        float finalDamage = context.DamageOverride > 0f ? context.DamageOverride : damage;
        projectile.Initialize(direction, speed, finalDamage, maxBounces, lifeSeconds, request.Attacker, context.Spawner);
    }
}
