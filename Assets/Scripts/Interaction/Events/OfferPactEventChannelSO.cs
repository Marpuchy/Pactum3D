using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public readonly struct OfferPactRequest
{
    public Interactor Interactor { get; }
    public IReadOnlyList<PactDefinition> Pacts { get; }
    public Sprite NpcCanvasSprite { get; }
    public IReadOnlyDictionary<string, PactPoolSO> PactSourcePoolsById { get; }
    private readonly Action<PactDefinition> onSelected;

    public OfferPactRequest(
        Interactor interactor,
        IReadOnlyList<PactDefinition> pacts,
        Action<PactDefinition> onSelected = null,
        Sprite npcCanvasSprite = null,
        IReadOnlyDictionary<string, PactPoolSO> pactSourcePoolsById = null)
    {
        Interactor = interactor;
        Pacts = pacts;
        NpcCanvasSprite = npcCanvasSprite;
        PactSourcePoolsById = pactSourcePoolsById;
        this.onSelected = onSelected;
    }

    public OfferPactRequest(Interactor interactor, PactDefinition pact, Sprite npcCanvasSprite = null)
    {
        Interactor = interactor;
        Pacts = pact != null ? new List<PactDefinition> { pact } : null;
        NpcCanvasSprite = npcCanvasSprite;
        PactSourcePoolsById = null;
        onSelected = null;
    }

    public void NotifySelected(PactDefinition pact)
    {
        onSelected?.Invoke(pact);
    }

    public bool TryGetPactSourcePool(PactDefinition pact, out PactPoolSO pool)
    {
        pool = null;
        if (pact == null || PactSourcePoolsById == null || PactSourcePoolsById.Count == 0)
            return false;

        string pactId = PactIdentity.Normalize(PactIdentity.ResolvePactId(pact));
        if (pactId.Length == 0)
            pactId = PactIdentity.Normalize(pact.name);

        if (pactId.Length == 0)
            return false;

        if (!PactSourcePoolsById.TryGetValue(pactId, out PactPoolSO resolvedPool))
            return false;

        pool = resolvedPool;
        return pool != null;
    }
}

[CreateAssetMenu(menuName = "Events/OfferPact", fileName = "OfferPactEventChannel")]
public sealed class OfferPactEventChannelSO : ScriptableObject
{
    public event Action<OfferPactRequest> OnRaised;

    public void Raise(OfferPactRequest request)
    {
        OnRaised?.Invoke(request);
    }
}
