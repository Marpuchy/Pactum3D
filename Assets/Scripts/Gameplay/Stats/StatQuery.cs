using System;
using System.Collections.Generic;

public sealed class StatQuery
{
    private readonly List<GameplayTag> tags;

    public StatQuery(StatType type, float baseValue, IReadOnlyList<GameplayTag> tags)
    {
        Type = type;
        BaseValue = baseValue;
        Value = baseValue;

        this.tags = new List<GameplayTag>(8);
        if (tags == null || tags.Count == 0)
            return;

        for (int i = 0; i < tags.Count; i++)
        {
            GameplayTag tag = tags[i];
            if (tag == null || ContainsEquivalentTag(this.tags, tag))
                continue;

            this.tags.Add(tag);
        }
    }

    public StatType Type { get; }
    public float BaseValue { get; }
    public float Value { get; set; }
    public IReadOnlyList<GameplayTag> Tags => tags;

    public bool HasTag(GameplayTag tag)
    {
        if (tag == null || tags.Count == 0)
            return false;

        for (int i = 0; i < tags.Count; i++)
        {
            if (AreEquivalent(tags[i], tag))
                return true;
        }

        return false;
    }

    public void AddTag(GameplayTag tag)
    {
        if (tag == null || ContainsEquivalentTag(tags, tag))
            return;

        tags.Add(tag);
    }

    public void AddTags(IReadOnlyList<GameplayTag> newTags)
    {
        if (newTags == null || newTags.Count == 0)
            return;

        for (int i = 0; i < newTags.Count; i++)
            AddTag(newTags[i]);
    }

    public void RemoveTag(GameplayTag tag)
    {
        if (tag == null || tags.Count == 0)
            return;

        for (int i = tags.Count - 1; i >= 0; i--)
        {
            if (AreEquivalent(tags[i], tag))
                tags.RemoveAt(i);
        }
    }

    public void RemoveTags(IReadOnlyList<GameplayTag> tagsToRemove)
    {
        if (tagsToRemove == null || tagsToRemove.Count == 0)
            return;

        for (int i = 0; i < tagsToRemove.Count; i++)
            RemoveTag(tagsToRemove[i]);
    }

    private static bool ContainsEquivalentTag(IReadOnlyList<GameplayTag> source, GameplayTag tag)
    {
        if (source == null || tag == null)
            return false;

        for (int i = 0; i < source.Count; i++)
        {
            if (AreEquivalent(source[i], tag))
                return true;
        }

        return false;
    }

    private static bool AreEquivalent(GameplayTag left, GameplayTag right)
    {
        if (left == null || right == null)
            return false;

        if (left == right)
            return true;

        if (!string.IsNullOrWhiteSpace(left.TagName) &&
            !string.IsNullOrWhiteSpace(right.TagName) &&
            string.Equals(left.TagName, right.TagName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(left.name) &&
            !string.IsNullOrWhiteSpace(right.name) &&
            string.Equals(left.name, right.name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
