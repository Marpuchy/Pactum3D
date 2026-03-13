using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct EnemySpawnData
{
    private readonly IReadOnlyList<GameplayTag> tags;
    private readonly IReadOnlyList<GameplayTag> movementTags;

    public EnemyDefinitionSO Definition { get; }
    public GameObject Prefab { get; }
    public Vector2Int Position { get; }
    public IReadOnlyList<GameplayTag> MovementTags => movementTags;
    public IReadOnlyList<GameplayTag> Tags => tags;

    public EnemySpawnData(
        EnemyDefinitionSO definition,
        Vector2Int position,
        GameObject fallbackPrefab = null,
        IReadOnlyList<GameplayTag> fallbackTags = null,
        IReadOnlyList<GameplayTag> fallbackMovementTags = null)
    {
        Definition = definition;
        Prefab = definition != null ? definition.Prefab : fallbackPrefab;
        Position = position;
        tags = CopyTags(definition != null ? definition.Tags : fallbackTags);
        movementTags = CopyTags(definition != null ? definition.MovementTags : fallbackMovementTags);
    }

    private static IReadOnlyList<GameplayTag> CopyTags(IReadOnlyList<GameplayTag> source)
    {
        if (source == null || source.Count == 0)
            return Array.Empty<GameplayTag>();

        var copied = new List<GameplayTag>(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            GameplayTag tag = source[i];
            if (tag != null && !copied.Contains(tag))
                copied.Add(tag);
        }

        return copied.Count == 0 ? Array.Empty<GameplayTag>() : copied;
    }
}
