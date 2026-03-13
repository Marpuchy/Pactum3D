using System;
using UnityEngine;

[Serializable]
public readonly struct PactSelectionContext
{
    public PactDefinition Pact { get; }
    public string NpcId { get; }
    public string LineId { get; }

    public PactSelectionContext(PactDefinition pact, string npcId, string lineId)
    {
        Pact = pact;
        NpcId = PactIdentity.Normalize(npcId);
        LineId = PactIdentity.Normalize(lineId);
    }
}

[CreateAssetMenu(menuName = "Events/PactSelected", fileName = "PactSelectedEventChannel")]
public sealed class PactSelectedEventChannelSO : ScriptableObject
{
    public event Action<PactSelectionContext> OnRaised;

    public void Raise(PactSelectionContext context)
    {
        OnRaised?.Invoke(context);
    }
}
