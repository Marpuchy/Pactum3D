using System.Collections.Generic;
using UnityEngine;

public static class RaritySelector
{
    public static ItemRaritySO PickRarity(List<RoomItemRarityEntry> entries)
    {
        return PickRarity(entries, null, null);
    }

    public static RoomChestRarityEntry PickChestEntry(List<RoomChestRarityEntry> entries)
    {
        return PickChestEntry(entries, null, null);
    }

    public static ItemRaritySO PickRarity(
        List<RoomItemRarityEntry> entries,
        LootStatsProvider provider,
        IReadOnlyList<GameplayTag> baseTags)
    {
        if (entries == null || entries.Count == 0)
            return null;

        float total = 0f;
        float[] weights = new float[entries.Count];
        List<GameplayTag> tagBuffer = provider != null ? new List<GameplayTag>(8) : null;

        for (int i = 0; i < entries.Count; i++)
        {
            RoomItemRarityEntry entry = entries[i];
            float weight = Mathf.Max(0f, entry.spawnWeight);
            if (provider != null)
            {
                IReadOnlyList<GameplayTag> tags = BuildTags(baseTags, entry.tags, tagBuffer);
                float multiplier = provider.Get(LootStatType.RarityWeightMultiplier, 1f, tags);
                weight *= Mathf.Max(0f, multiplier);
            }

            weights[i] = weight;
            total += weight;
        }

        if (total <= 0f)
            return null;

        float roll = Random.value * total;
        float cumulative = 0f;

        for (int i = 0; i < entries.Count; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
                return entries[i].rarity;
        }

        return entries[0].rarity;
    }

    public static RoomChestRarityEntry PickChestEntry(
        List<RoomChestRarityEntry> entries,
        LootStatsProvider provider,
        IReadOnlyList<GameplayTag> baseTags)
    {
        if (entries == null || entries.Count == 0)
            return null;

        float total = 0f;
        float[] weights = new float[entries.Count];
        List<GameplayTag> tagBuffer = provider != null ? new List<GameplayTag>(8) : null;

        for (int i = 0; i < entries.Count; i++)
        {
            RoomChestRarityEntry entry = entries[i];
            float weight = Mathf.Max(0f, entry.spawnWeight);
            if (provider != null)
            {
                IReadOnlyList<GameplayTag> tags = BuildTags(baseTags, entry.tags, tagBuffer);
                float multiplier = provider.Get(LootStatType.RarityWeightMultiplier, 1f, tags);
                weight *= Mathf.Max(0f, multiplier);
            }

            weights[i] = weight;
            total += weight;
        }

        if (total <= 0f)
            return null;

        float roll = Random.value * total;
        float cumulative = 0f;

        for (int i = 0; i < entries.Count; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
                return entries[i];
        }

        return entries[0];
    }

    private static IReadOnlyList<GameplayTag> BuildTags(
        IReadOnlyList<GameplayTag> baseTags,
        IReadOnlyList<GameplayTag> entryTags,
        List<GameplayTag> buffer)
    {
        bool hasBase = baseTags != null && baseTags.Count > 0;
        bool hasEntry = entryTags != null && entryTags.Count > 0;

        if (!hasBase && !hasEntry)
            return null;

        buffer.Clear();

        if (hasBase)
            buffer.AddRange(baseTags);

        if (hasEntry)
            buffer.AddRange(entryTags);

        return buffer;
    }
}
