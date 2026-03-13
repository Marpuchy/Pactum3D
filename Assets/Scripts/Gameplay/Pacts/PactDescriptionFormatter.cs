using System.Collections.Generic;
using System.Text;

public static class PactDescriptionFormatter
{
    public static string HumanizeEnum(System.Enum value)
    {
        return value == null ? string.Empty : HumanizeIdentifier(value.ToString());
    }

    public static string HumanizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string trimmed = value.Trim();
        var builder = new StringBuilder(trimmed.Length + 8);

        for (int i = 0; i < trimmed.Length; i++)
        {
            char current = trimmed[i];
            if (i > 0 && char.IsUpper(current) && !char.IsWhiteSpace(trimmed[i - 1]))
                builder.Append(' ');

            builder.Append(current);
        }

        return builder.ToString();
    }

    public static string FormatSignedValue(float value)
    {
        return value >= 0f ? $"+{value:0.##}" : value.ToString("0.##");
    }

    public static string FormatMultiplier(float value)
    {
        return $"x{value:0.##}";
    }

    public static string FormatTagList(IReadOnlyList<GameplayTag> tags)
    {
        if (tags == null || tags.Count == 0)
            return string.Empty;

        var names = new List<string>(tags.Count);
        for (int i = 0; i < tags.Count; i++)
        {
            GameplayTag tag = tags[i];
            if (tag == null)
                continue;

            string resolved = ResolveTagName(tag);
            if (resolved.Length > 0)
                names.Add(resolved);
        }

        return names.Count == 0 ? string.Empty : string.Join(", ", names);
    }

    public static string FormatRequiredTagsSuffix(IReadOnlyList<GameplayTag> requiredTags)
    {
        string tags = FormatTagList(requiredTags);
        return tags.Length == 0 ? string.Empty : $" if has [{tags}]";
    }

    public static string ResolveTagName(GameplayTag tag)
    {
        if (tag == null)
            return string.Empty;

        string tagName = PactTagUtility.NormalizeTagName(tag.TagName);
        if (tagName.Length == 0)
            tagName = PactTagUtility.NormalizeTagName(tag.name);

        string withoutSuffix = PactTagUtility.RemoveTagSuffix(tagName);
        return HumanizeIdentifier(withoutSuffix);
    }
}
