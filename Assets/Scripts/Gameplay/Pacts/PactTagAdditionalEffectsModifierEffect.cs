using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "PactTagAdditionalEffectsModifierEffect",
    menuName = "Gameplay/Pacts/Pact Tag Additional Effects Modifier Effect")]
public sealed class PactTagAdditionalEffectsModifierEffect : PactModifierAsset
{
    [Header("Target Pact Tags")]
    [SerializeField] private List<GameplayTag> targetPactTags = new List<GameplayTag>();

    [Header("Effects To Add")]
    [SerializeField] private List<PactModifierAsset> effectsToAdd = new List<PactModifierAsset>();

    public IReadOnlyList<GameplayTag> TargetPactTags => targetPactTags;
    public IReadOnlyList<PactModifierAsset> EffectsToAdd => effectsToAdd;

    public bool Matches(PactDefinition pact)
    {
        if (pact == null || targetPactTags == null || targetPactTags.Count == 0)
            return false;

        IReadOnlyList<GameplayTag> pactTags = pact.Tags;
        if (pactTags == null || pactTags.Count == 0)
            return false;

        for (int i = 0; i < targetPactTags.Count; i++)
        {
            GameplayTag targetTag = targetPactTags[i];
            if (targetTag != null && PactTagUtility.ContainsTag(pactTags, targetTag))
                return true;
        }

        return false;
    }

    protected override string BuildAutoDescription()
    {
        string targetTags = PactDescriptionFormatter.FormatTagList(targetPactTags);
        if (targetTags.Length == 0)
            return string.Empty;

        var additionalDescriptions = new List<string>();
        if (effectsToAdd != null)
        {
            for (int i = 0; i < effectsToAdd.Count; i++)
            {
                PactModifierAsset effect = effectsToAdd[i];
                if (effect == null)
                    continue;

                string description = effect.Description;
                if (string.IsNullOrWhiteSpace(description))
                    description = effect.name;

                if (!string.IsNullOrWhiteSpace(description))
                    additionalDescriptions.Add(description.Trim());
            }
        }

        if (additionalDescriptions.Count == 0)
            return $"Pacts with tags [{targetTags}] gain additional effects";

        return $"Pacts with tags [{targetTags}] gain: {string.Join("; ", additionalDescriptions)}";
    }
}
