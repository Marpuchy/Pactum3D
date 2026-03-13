using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "PoolRequiredAnyTagModifierEffect",
    menuName = "Gameplay/Pacts/Pool Required Any Tag Modifier Effect")]
public sealed class PoolRequiredAnyTagModifierEffect : PactModifierAsset
{
    [Header("Target Pulls")]
    [SerializeField] private List<GameplayTag> targetPoolTags = new List<GameplayTag>();

    [Header("Add Required Any Pact Tags")]
    [SerializeField] private List<GameplayTag> requiredAnyTagsToAdd = new List<GameplayTag>();

    public IReadOnlyList<GameplayTag> TargetPoolTags => targetPoolTags;
    public IReadOnlyList<GameplayTag> RequiredAnyTagsToAdd => requiredAnyTagsToAdd;

    protected override string BuildAutoDescription()
    {
        string pullTags = PactDescriptionFormatter.FormatTagList(targetPoolTags);
        string allowedPactTags = PactDescriptionFormatter.FormatTagList(requiredAnyTagsToAdd);

        if (allowedPactTags.Length == 0)
            return string.Empty;

        if (pullTags.Length == 0)
            return $"Pulls accept any pact with tags [{allowedPactTags}]";

        return $"Pulls with tags [{pullTags}] accept any pact with tags [{allowedPactTags}]";
    }
}
