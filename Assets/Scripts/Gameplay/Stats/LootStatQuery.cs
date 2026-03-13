using System;
using System.Collections.Generic;

public sealed class LootStatQuery
{
    public LootStatQuery(LootStatType type, float baseValue, IReadOnlyList<GameplayTag> tags)
    {
        Type = type;
        BaseValue = baseValue;
        Value = baseValue;
        Tags = tags ?? Array.Empty<GameplayTag>();
    }

    public LootStatType Type { get; }
    public float BaseValue { get; }
    public float Value { get; set; }
    public IReadOnlyList<GameplayTag> Tags { get; }

    public bool HasTag(GameplayTag tag)
    {
        if (tag == null || Tags.Count == 0)
            return false;

        for (int i = 0; i < Tags.Count; i++)
        {
            if (Tags[i] == tag)
                return true;
        }

        return false;
    }
}
