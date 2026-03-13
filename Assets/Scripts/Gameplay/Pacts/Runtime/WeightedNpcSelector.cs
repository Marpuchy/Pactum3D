using System.Collections.Generic;
using SaveSystem;
using UnityEngine;
using Random = UnityEngine.Random;

public sealed class WeightedNpcSelector : INpcSelector
{
    private readonly IRunState runState;

    public WeightedNpcSelector(IRunState runState)
    {
        this.runState = runState;
    }

    public bool IsNpcEligible(string npcId, string lineId)
    {
        return runState == null || runState.IsNpcEligible(npcId, lineId);
    }

    public bool IsEntryEligible(NpcSpawnEntry entry)
    {
        if (!IsEntryValid(entry))
            return false;

        if (!TryResolvePactNpcIdentity(entry, out string npcId, out string lineId))
            return true;

        return IsNpcEligible(npcId, lineId);
    }

    public NpcSpawnEntry SelectNpc(IReadOnlyList<NpcSpawnEntry> candidates)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        if (LoadedNpcRoomOfferState.TryGetExpectedNpcForCurrentRoom(out string expectedNpcId))
        {
            NpcSpawnEntry forcedEntry = TrySelectExpectedNpc(candidates, expectedNpcId);
            if (forcedEntry != null)
                return forcedEntry;
        }

        float totalWeight = 0f;
        List<NpcSpawnEntry> eligible = new List<NpcSpawnEntry>(candidates.Count);

        for (int i = 0; i < candidates.Count; i++)
        {
            NpcSpawnEntry entry = candidates[i];
            if (!IsEntryEligible(entry))
                continue;

            eligible.Add(entry);

            float weight = Mathf.Max(0f, entry.spawnRate);
            totalWeight += weight;
        }

        if (eligible.Count == 0)
            return null;

        if (totalWeight <= 0f)
        {
            int randomIndex = Random.Range(0, eligible.Count);
            return eligible[randomIndex];
        }

        float roll = Random.value * totalWeight;
        float current = 0f;

        for (int i = 0; i < eligible.Count; i++)
        {
            NpcSpawnEntry entry = eligible[i];
            current += Mathf.Max(0f, entry.spawnRate);
            if (roll <= current)
                return entry;
        }

        return eligible[eligible.Count - 1];
    }

    private NpcSpawnEntry TrySelectExpectedNpc(IReadOnlyList<NpcSpawnEntry> candidates, string expectedNpcId)
    {
        if (string.IsNullOrEmpty(expectedNpcId) || candidates == null || candidates.Count == 0)
            return null;

        for (int i = 0; i < candidates.Count; i++)
        {
            NpcSpawnEntry candidate = candidates[i];
            if (!IsEntryEligible(candidate))
                continue;

            if (!TryResolvePactNpcIdentity(candidate, out string candidateNpcId, out _))
                continue;

            if (PactIdentity.AreEqual(candidateNpcId, expectedNpcId))
                return candidate;
        }

        return null;
    }

    private static bool IsEntryValid(NpcSpawnEntry entry)
    {
        return entry != null && entry.isInRoom && entry.Prefab != null;
    }

    private static bool TryResolvePactNpcIdentity(NpcSpawnEntry entry, out string npcId, out string lineId)
    {
        npcId = string.Empty;
        lineId = string.Empty;

        if (entry == null)
            return false;

        if (entry.NpcDefinition != null)
        {
            npcId = PactIdentity.Normalize(entry.NpcDefinition.NpcId);
            lineId = ResolveLineIdFromPool(entry.NpcDefinition.OwnPactPool);
        }

        GameObject prefab = entry.Prefab;
        if (prefab == null)
            return !string.IsNullOrEmpty(npcId);

        PactNpc pactNpc = prefab.GetComponent<PactNpc>();
        if (pactNpc == null)
            return !string.IsNullOrEmpty(npcId);

        if (string.IsNullOrEmpty(npcId))
            npcId = PactIdentity.Normalize(pactNpc.NpcId);

        if (string.IsNullOrEmpty(lineId))
            lineId = PactIdentity.Normalize(pactNpc.PactLineId);

        return !string.IsNullOrEmpty(npcId) || !string.IsNullOrEmpty(lineId);
    }

    private static string ResolveLineIdFromPool(PactPoolSO pool)
    {
        if (pool == null)
            return string.Empty;

        string fromRequiredAll = ResolveLineIdFromTags(pool.RequiredAllPactTags);
        if (!string.IsNullOrEmpty(fromRequiredAll))
            return fromRequiredAll;

        string fromRequiredAny = ResolveLineIdFromTags(pool.RequiredAnyPactTags);
        if (!string.IsNullOrEmpty(fromRequiredAny))
            return fromRequiredAny;

        string fromPoolTags = ResolveLineIdFromTags(pool.PoolTags);
        if (!string.IsNullOrEmpty(fromPoolTags))
            return fromPoolTags;

        return string.Empty;
    }

    private static string ResolveLineIdFromTags(IReadOnlyList<GameplayTag> tags)
    {
        if (tags == null || tags.Count == 0)
            return string.Empty;

        return PactIdentity.Normalize(PactTagUtility.ResolveLineIdFromTags(tags));
    }
}
