using UnityEngine;

public class ChestSpawnData
{
    public GameObject Prefab { get; }
    public Vector2Int Position { get; }
    public LootTableSO LootTableOverride { get; }

    public ChestSpawnData(
        GameObject prefab,
        Vector2Int position,
        LootTableSO lootTableOverride = null)
    {
        Prefab = prefab;
        Position = position;
        LootTableOverride = lootTableOverride;
    }
}
