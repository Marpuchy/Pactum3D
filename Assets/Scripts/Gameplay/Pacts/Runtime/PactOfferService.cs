using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public sealed class PactOfferService : IPactOfferService
{
    private readonly IRunState runState;

    public PactOfferService(IRunState runState)
    {
        this.runState = runState;
    }

    public IReadOnlyList<PactDefinition> BuildOffers(
        PactPoolSO generalPool,
        PactPoolSO linePool,
        IReadOnlyList<PactDefinition> activePacts,
        int offerCount)
    {
        List<PactDefinition> offers = new List<PactDefinition>();
        int maxOffers = Mathf.Max(1, offerCount);

        List<PactDefinition> lineCandidates = CollectCandidates(linePool, activePacts);
        List<PactDefinition> generalCandidates = CollectCandidates(generalPool, activePacts);
        List<PactDefinition> combinedCandidates = new List<PactDefinition>(
            lineCandidates.Count + generalCandidates.Count);
        combinedCandidates.AddRange(lineCandidates);
        combinedCandidates.AddRange(generalCandidates);

        int amount = Mathf.Min(maxOffers, combinedCandidates.Count);
        AddRandomSubset(offers, combinedCandidates, amount);

        return offers;
    }

    public int CountEligiblePacts(
        PactPoolSO generalPool,
        PactPoolSO linePool,
        IReadOnlyList<PactDefinition> activePacts)
    {
        int lineCount = CollectCandidates(linePool, activePacts).Count;
        int generalCount = CollectCandidates(generalPool, activePacts).Count;
        return lineCount + generalCount;
    }

    public bool IsPactEligible(
        PactDefinition pact,
        IReadOnlyList<PactDefinition> activePacts)
    {
        if (pact == null || IsActivePact(activePacts, pact))
            return false;

        return IsLineProgressionSatisfied(pact, activePacts);
    }

    private List<PactDefinition> CollectCandidates(
        PactPoolSO pool,
        IReadOnlyList<PactDefinition> activePacts)
    {
        List<PactDefinition> candidates = new List<PactDefinition>();
        if (pool == null)
            return candidates;

        IReadOnlyList<PactDefinition> pacts = pool.ResolvePacts();
        for (int i = 0; i < pacts.Count; i++)
        {
            PactDefinition pact = pacts[i];
            if (!IsPactEligible(pact, activePacts))
                continue;

            candidates.Add(pact);
        }

        return candidates;
    }

    private static void AddRandomSubset(
        List<PactDefinition> destination,
        List<PactDefinition> source,
        int amount)
    {
        if (destination == null || source == null || amount <= 0)
            return;

        int count = Mathf.Min(amount, source.Count);
        for (int i = 0; i < count; i++)
        {
            int index = Random.Range(0, source.Count);
            destination.Add(source[index]);
            source.RemoveAt(index);
        }
    }

    private int GetTargetTier(string lineId)
    {
        if (string.IsNullOrEmpty(lineId))
            return 1;

        int currentTier = runState != null ? runState.GetLineTier(lineId) : 0;
        return Mathf.Max(1, currentTier + 1);
    }

    private bool IsLineProgressionSatisfied(PactDefinition pact, IReadOnlyList<PactDefinition> activePacts)
    {
        if (pact == null)
            return false;

        PactDefinition requiredPreviousPact = pact.RequiredPreviousPact;
        if (requiredPreviousPact != null)
            return IsActivePact(activePacts, requiredPreviousPact);

        string lineId = PactIdentity.ResolveLineId(pact);
        if (lineId.Length == 0)
            return true;

        int pactTier = Mathf.Max(1, pact.LineTier);
        if (pactTier <= 1)
            return true;

        int targetTier = GetTargetTier(lineId);
        return pactTier == targetTier;
    }

    private bool IsActivePact(IReadOnlyList<PactDefinition> activePacts, PactDefinition pactToCheck)
    {
        if (pactToCheck == null)
            return false;

        string pactId = PactIdentity.ResolvePactId(pactToCheck);
        if (runState != null && runState.HasPact(pactId))
            return true;

        if (activePacts == null)
            return false;

        for (int i = 0; i < activePacts.Count; i++)
        {
            PactDefinition active = activePacts[i];
            if (active == pactToCheck)
                return true;

            if (PactIdentity.AreEqual(PactIdentity.ResolvePactId(active), pactId))
                return true;
        }

        return false;
    }
}
