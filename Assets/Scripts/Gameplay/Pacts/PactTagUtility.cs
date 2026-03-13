using System;
using System.Collections.Generic;

public static class PactTagUtility
{
    public const string PactTagName = "PactTag";

    public const string Tier1TagName = "Tier1Tag";
    public const string Tier2TagName = "Tier2Tag";
    public const string Tier3TagName = "Tier3Tag";

    public const string CommonTagName = "CommonTag";
    public const string RareTagName = "RareTag";
    public const string EpicTagName = "EpicTag";
    public const string LegendaryTagName = "LegendaryTag";

    public const string PullTagName = "PullTag";
    public const string EyeLineTagName = "EyeLineTag";
    public const string BloodyLineTagName = "BloodyLineTag";
    public const string MomLineTagName = "MomLineTag";
    public const string GenericLineTagName = "GenericLineTag";

    public static bool IsTagMatch(GameplayTag tag, string expectedTagName)
    {
        if (tag == null || string.IsNullOrWhiteSpace(expectedTagName))
            return false;

        string normalizedExpected = NormalizeTagName(expectedTagName);
        if (normalizedExpected.Length == 0)
            return false;

        string expectedWithoutSuffix = RemoveTagSuffix(normalizedExpected);
        string expectedWithSuffix = normalizedExpected.EndsWith("Tag", StringComparison.OrdinalIgnoreCase)
            ? normalizedExpected
            : $"{normalizedExpected}Tag";

        string tagName = NormalizeTagName(tag.TagName);
        string assetName = NormalizeTagName(tag.name);

        return string.Equals(tagName, normalizedExpected, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tagName, expectedWithoutSuffix, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tagName, expectedWithSuffix, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(assetName, normalizedExpected, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(assetName, expectedWithoutSuffix, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(assetName, expectedWithSuffix, StringComparison.OrdinalIgnoreCase);
    }

    public static bool ContainsTag(IReadOnlyList<GameplayTag> tags, GameplayTag tagToCheck)
    {
        if (tags == null || tagToCheck == null)
            return false;

        for (int i = 0; i < tags.Count; i++)
        {
            GameplayTag current = tags[i];
            if (current == tagToCheck)
                return true;

            if (current == null)
                continue;

            if (IsTagMatch(current, tagToCheck.TagName) || IsTagMatch(current, tagToCheck.name))
                return true;
        }

        return false;
    }

    public static bool ContainsTagNamed(IReadOnlyList<GameplayTag> tags, string expectedTagName)
    {
        if (tags == null || string.IsNullOrWhiteSpace(expectedTagName))
            return false;

        for (int i = 0; i < tags.Count; i++)
        {
            if (IsTagMatch(tags[i], expectedTagName))
                return true;
        }

        return false;
    }

    public static GameplayTag FindTagNamed(IReadOnlyList<GameplayTag> tags, string expectedTagName)
    {
        if (tags == null || string.IsNullOrWhiteSpace(expectedTagName))
            return null;

        for (int i = 0; i < tags.Count; i++)
        {
            GameplayTag current = tags[i];
            if (IsTagMatch(current, expectedTagName))
                return current;
        }

        return null;
    }

    public static int ResolveTier(IReadOnlyList<GameplayTag> tags)
    {
        if (ContainsTagNamed(tags, Tier3TagName))
            return 3;

        if (ContainsTagNamed(tags, Tier2TagName))
            return 2;

        if (ContainsTagNamed(tags, Tier1TagName))
            return 1;

        return 0;
    }

    public static string ResolveTierTagName(int tier)
    {
        switch (tier)
        {
            case 1:
                return Tier1TagName;
            case 2:
                return Tier2TagName;
            case 3:
                return Tier3TagName;
            default:
                return string.Empty;
        }
    }

    public static bool IsLineTag(GameplayTag tag)
    {
        if (tag == null)
            return false;

        string normalized = NormalizeTagName(tag.TagName);
        if (normalized.Length == 0)
            return false;

        string withoutSuffix = RemoveTagSuffix(normalized);
        return withoutSuffix.EndsWith("Line", StringComparison.OrdinalIgnoreCase);
    }

    public static GameplayTag ResolveLineTag(IReadOnlyList<GameplayTag> tags)
    {
        if (tags == null)
            return null;

        for (int i = 0; i < tags.Count; i++)
        {
            GameplayTag tag = tags[i];
            if (IsLineTag(tag))
                return tag;
        }

        return null;
    }

    public static string ResolveLineIdFromTags(IReadOnlyList<GameplayTag> tags)
    {
        GameplayTag lineTag = ResolveLineTag(tags);
        if (lineTag == null)
            return string.Empty;

        string normalized = NormalizeTagName(lineTag.TagName);
        if (normalized.Length == 0)
            normalized = NormalizeTagName(lineTag.name);

        return RemoveTagSuffix(normalized);
    }

    public static string RemoveTagSuffix(string value)
    {
        string normalized = NormalizeTagName(value);
        if (normalized.EndsWith("Tag", StringComparison.OrdinalIgnoreCase))
            return normalized.Substring(0, normalized.Length - 3);

        return normalized;
    }

    public static string NormalizeTagName(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
