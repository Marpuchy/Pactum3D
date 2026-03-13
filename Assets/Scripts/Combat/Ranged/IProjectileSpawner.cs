using UnityEngine;

public interface IProjectileSpawner
{
    Projectile Spawn(Projectile prefab, Vector3 position, Quaternion rotation);
    void Despawn(Projectile projectile);
}
