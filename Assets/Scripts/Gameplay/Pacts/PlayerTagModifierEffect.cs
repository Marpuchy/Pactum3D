using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerTagModifierEffect", menuName = "Gameplay/Pacts/Player Tag Modifier Effect")]
public sealed class PlayerTagModifierEffect : PactEffect
{
    public enum FilterMode
    {
        Inclusive,
        Exclusive,
        Combined
    }

    public enum InclusionMode
    {
        Any,
        All
    }

    [Header("Target Filter")]
    [SerializeField] private FilterMode filterMode = FilterMode.Combined;
    [SerializeField] private InclusionMode inclusionMode = InclusionMode.All;
    [SerializeField] private List<GameplayTag> includeTags = new List<GameplayTag>();
    [SerializeField] private List<GameplayTag> excludeTags = new List<GameplayTag>();

    [Header("Add Tags")]
    [SerializeField] private List<GameplayTag> addTags = new List<GameplayTag>();

    [Header("Remove Tags")]
    [SerializeField] private List<GameplayTag> removeTags = new List<GameplayTag>();

    public override void Apply(StatQuery query)
    {
        if (query == null)
            return;

        if (!MatchesTargetFilter(query))
            return;

        query.RemoveTags(removeTags);
        query.AddTags(addTags);
    }

    private bool MatchesTargetFilter(StatQuery query)
    {
        bool includeMatch = MatchesIncludeTags(query);
        bool excludeMatch = MatchesExcludeTags(query);

        switch (filterMode)
        {
            case FilterMode.Inclusive:
                return includeMatch;
            case FilterMode.Exclusive:
                return excludeMatch;
            case FilterMode.Combined:
                return includeMatch && excludeMatch;
            default:
                return includeMatch && excludeMatch;
        }
    }

    private bool MatchesIncludeTags(StatQuery query)
    {
        if (includeTags == null || includeTags.Count == 0)
            return true;

        switch (inclusionMode)
        {
            case InclusionMode.Any:
                for (int i = 0; i < includeTags.Count; i++)
                {
                    GameplayTag includeTag = includeTags[i];
                    if (includeTag != null && query.HasTag(includeTag))
                        return true;
                }

                return false;

            case InclusionMode.All:
            default:
                for (int i = 0; i < includeTags.Count; i++)
                {
                    GameplayTag includeTag = includeTags[i];
                    if (includeTag == null)
                        continue;

                    if (!query.HasTag(includeTag))
                        return false;
                }

                return true;
        }
    }

    private bool MatchesExcludeTags(StatQuery query)
    {
        if (excludeTags == null || excludeTags.Count == 0)
            return true;

        for (int i = 0; i < excludeTags.Count; i++)
        {
            GameplayTag excludeTag = excludeTags[i];
            if (excludeTag == null)
                continue;

            if (query.HasTag(excludeTag))
                return false;
        }

        return true;
    }

    protected override string BuildAutoDescription()
    {
        string changes = BuildTagChangesDescription();
        string filters = BuildFilterDescription();

        if (filters.Length == 0)
            return $"Player tags: {changes}";

        return $"Player tags: {changes} ({filters})";
    }

    private string BuildTagChangesDescription()
    {
        string add = PactDescriptionFormatter.FormatTagList(addTags);
        string remove = PactDescriptionFormatter.FormatTagList(removeTags);

        if (add.Length > 0 && remove.Length > 0)
            return $"add [{add}], remove [{remove}]";

        if (add.Length > 0)
            return $"add [{add}]";

        if (remove.Length > 0)
            return $"remove [{remove}]";

        return "no changes";
    }

    private string BuildFilterDescription()
    {
        string include = PactDescriptionFormatter.FormatTagList(includeTags);
        string exclude = PactDescriptionFormatter.FormatTagList(excludeTags);

        string includeClause = include.Length == 0
            ? string.Empty
            : inclusionMode == InclusionMode.Any
                ? $"if has any of [{include}]"
                : $"if has all of [{include}]";

        string excludeClause = exclude.Length == 0
            ? string.Empty
            : $"if has none of [{exclude}]";

        switch (filterMode)
        {
            case FilterMode.Inclusive:
                return includeClause;
            case FilterMode.Exclusive:
                return excludeClause;
            case FilterMode.Combined:
            default:
                if (includeClause.Length > 0 && excludeClause.Length > 0)
                    return $"{includeClause}; {excludeClause}";

                return includeClause.Length > 0 ? includeClause : excludeClause;
        }
    }
}
