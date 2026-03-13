using System.Collections.Generic;
using UnityEngine;

public readonly struct NpcSpawnData
{
    public NpcDefinitionSO Definition { get; }
    public GameObject Prefab { get; }
    public Vector2Int Position { get; }
    public IReadOnlyList<GameplayTag> Tags => Definition != null ? Definition.Tags : null;

    public NpcSpawnData(
        NpcDefinitionSO definition,
        Vector2Int position,
        GameObject fallbackPrefab = null)
    {
        Definition = definition;
        Prefab = definition != null && definition.SharedPrefab != null
            ? definition.SharedPrefab
            : fallbackPrefab;
        Position = position;
    }
}
