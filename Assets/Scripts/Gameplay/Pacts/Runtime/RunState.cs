using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class RunState : IRunState
{
    private readonly HashSet<string> activePactIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> lineTierById = new Dictionary<string, int>(StringComparer.Ordinal);

    public string LockedNpcId { get; private set; } = string.Empty;
    public string LockedLineId { get; private set; } = string.Empty;
    public IReadOnlyCollection<string> ActivePactIds => activePactIds;

    public void RecordPact(PactDefinition pact)
    {
        if (pact == null)
            return;

        string pactId = PactIdentity.ResolvePactId(pact);
        if (pactId.Length > 0)
            activePactIds.Add(pactId);

        string lineId = PactIdentity.ResolveLineId(pact);
        if (lineId.Length == 0)
            return;

        int tier = Mathf.Max(1, pact.LineTier);
        SetLineTier(lineId, tier);
    }

    public void RemovePact(PactDefinition pact)
    {
        if (pact == null)
            return;

        string pactId = PactIdentity.ResolvePactId(pact);
        if (pactId.Length > 0)
            activePactIds.Remove(pactId);
    }

    public bool HasPact(string pactId)
    {
        string normalized = PactIdentity.Normalize(pactId);
        return normalized.Length > 0 && activePactIds.Contains(normalized);
    }

    public int GetLineTier(string lineId)
    {
        string normalized = PactIdentity.Normalize(lineId);
        if (normalized.Length == 0)
            return 0;

        return lineTierById.TryGetValue(normalized, out int tier) ? tier : 0;
    }

    public void SetLineTier(string lineId, int tier)
    {
        string normalized = PactIdentity.Normalize(lineId);
        if (normalized.Length == 0)
            return;

        int sanitizedTier = Mathf.Max(1, tier);

        if (!lineTierById.TryGetValue(normalized, out int currentTier))
        {
            lineTierById[normalized] = sanitizedTier;
            return;
        }

        if (sanitizedTier > currentTier)
            lineTierById[normalized] = sanitizedTier;
    }

    public void LockTo(string npcId, string lineId)
    {
        string normalizedLine = PactIdentity.Normalize(lineId);
        if (normalizedLine.Length > 0)
            LockedLineId = normalizedLine;

        string normalizedNpc = PactIdentity.Normalize(npcId);
        if (normalizedNpc.Length > 0)
            LockedNpcId = normalizedNpc;
    }

    public bool IsNpcEligible(string npcId, string lineId)
    {
        if (!string.IsNullOrEmpty(LockedNpcId))
            return PactIdentity.AreEqual(LockedNpcId, npcId);

        if (!string.IsNullOrEmpty(LockedLineId))
            return PactIdentity.AreEqual(LockedLineId, lineId);

        return true;
    }

    public void ResetRun()
    {
        activePactIds.Clear();
        lineTierById.Clear();
        LockedNpcId = string.Empty;
        LockedLineId = string.Empty;
    }
}
