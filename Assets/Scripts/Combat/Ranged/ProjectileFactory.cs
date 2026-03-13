using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ProjectileFactory : MonoBehaviour, IProjectileSpawner
{
    [Serializable]
    private struct PoolConfig
    {
        public Projectile prefab;
        public int prewarmCount;
    }

    [SerializeField] private List<PoolConfig> pools = new();
    [SerializeField] private RoomSpawnEvent roomSpawnEvent;


    private readonly Dictionary<Projectile, ProjectilePool> poolByPrefab = new();
    private readonly Dictionary<Projectile, ProjectilePool> poolByInstance = new();

    private void Awake()
    {
        for (int i = 0; i < pools.Count; i++)
        {
            PoolConfig config = pools[i];
            if (config.prefab == null)
                continue;
            if (poolByPrefab.ContainsKey(config.prefab))
                continue;
            poolByPrefab.Add(config.prefab, new ProjectilePool(config.prefab, config.prewarmCount, transform));
        }
    }

    public Projectile Spawn(Projectile prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        ProjectilePool pool = GetOrCreatePool(prefab);
        Projectile instance = pool.Spawn(position, rotation);
        poolByInstance[instance] = pool;
        return instance;
    }

    public void Despawn(Projectile projectile)
    {
        if (projectile == null)
            return;

        if (poolByInstance.TryGetValue(projectile, out ProjectilePool pool))
        {
            poolByInstance.Remove(projectile);
            pool.Despawn(projectile);
            return;
        }

        projectile.gameObject.SetActive(false);
        projectile.transform.SetParent(transform);
    }
    
    public void DespawnAll()
    {
        foreach (var kvp in poolByInstance)
        {
            Projectile projectile = kvp.Key;
            if (projectile != null)
                projectile.gameObject.SetActive(false);
        }

        poolByInstance.Clear();
    }

    private ProjectilePool GetOrCreatePool(Projectile prefab)
    {
        if (poolByPrefab.TryGetValue(prefab, out ProjectilePool pool))
            return pool;

        pool = new ProjectilePool(prefab, 0, transform);
        poolByPrefab.Add(prefab, pool);
        return pool;
    }
    
    private void OnEnable()
    {
        if (roomSpawnEvent != null)
            roomSpawnEvent.OnRoomSpawn += OnRoomSpawned;
    }

    private void OnDisable()
    {
        if (roomSpawnEvent != null)
            roomSpawnEvent.OnRoomSpawn -= OnRoomSpawned;
    }

    private void OnRoomSpawned(Vector3 _)
    {
        DespawnAll();
    }



    private sealed class ProjectilePool
    {
        private readonly Projectile prefab;
        private readonly Transform parent;
        private readonly Queue<Projectile> queue = new();

        public ProjectilePool(Projectile prefab, int prewarmCount, Transform parent)
        {
            this.prefab = prefab;
            this.parent = parent;
            Prewarm(prewarmCount);
        }

        public Projectile Spawn(Vector3 position, Quaternion rotation)
        {
            Projectile instance = queue.Count > 0 ? queue.Dequeue() : CreateInstance();
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.gameObject.SetActive(true);
            return instance;
        }

        public void Despawn(Projectile projectile)
        {
            projectile.gameObject.SetActive(false);
            projectile.transform.SetParent(parent);
            queue.Enqueue(projectile);
        }

        private Projectile CreateInstance()
        {
            Projectile instance = UnityEngine.Object.Instantiate(prefab, parent);
            instance.gameObject.SetActive(false);
            return instance;
        }

        private void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Projectile instance = CreateInstance();
                queue.Enqueue(instance);
            }
        }
    }
}
