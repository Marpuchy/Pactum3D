using System;

public static class PactIdentity
{
    public static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public static string ResolvePactId(PactDefinition pact)
    {
        if (pact == null)
            return string.Empty;

        string explicitId = Normalize(pact.SaveId);
        return explicitId.Length > 0 ? explicitId : Normalize(pact.name);
    }

    public static string ResolveLineId(PactDefinition pact)
    {
        if (pact == null)
            return string.Empty;

        string fromTags = Normalize(PactTagUtility.ResolveLineIdFromTags(pact.Tags));
        if (fromTags.Length > 0)
            return fromTags;

        return ResolveLineId(pact.Line);
    }

    public static string ResolveLineId(GameplayTag lineTag)
    {
        if (lineTag == null)
            return string.Empty;

        string tagName = PactTagUtility.NormalizeTagName(lineTag.TagName);
        if (tagName.Length == 0)
            tagName = PactTagUtility.NormalizeTagName(lineTag.name);

        return Normalize(PactTagUtility.RemoveTagSuffix(tagName));
    }

    public static string ResolveLineId(PactLineSO line)
    {
        if (line == null)
            return string.Empty;

        string fromName = Normalize(PactTagUtility.RemoveTagSuffix(line.name));
        if (fromName.Length > 0)
            return fromName;

        string explicitId = Normalize(line.Id);
        return explicitId.Length > 0 ? explicitId : fromName;
    }

    public static bool AreEqual(string left, string right)
    {
        return string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);
    }
}
