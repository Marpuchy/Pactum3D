using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "PactPool", menuName = "Gameplay/Pacts/PactPool")]
public sealed class PactPoolSO : ScriptableObject
{
    private const int TierCount = 4;

    [Header("Pool Tags")]
    [SerializeField] private List<GameplayTag> poolTags = new List<GameplayTag>();
    [SerializeField] private Sprite[] lineScrollIcon = new Sprite[TierCount];

    [Header("Pact Conditions")]
    [SerializeField] private List<GameplayTag> requiredAllPactTags = new List<GameplayTag>();
    [SerializeField] private List<GameplayTag> requiredAnyPactTags = new List<GameplayTag>();
    [SerializeField] private List<GameplayTag> excludedPactTags = new List<GameplayTag>();

    [SerializeField, HideInInspector] private List<PactDefinition> pacts = new List<PactDefinition>();

    public IReadOnlyList<GameplayTag> PoolTags => poolTags;
    public IReadOnlyList<GameplayTag> RequiredAllPactTags => requiredAllPactTags;
    public IReadOnlyList<GameplayTag> RequiredAnyPactTags => requiredAnyPactTags;
    public IReadOnlyList<GameplayTag> ExcludedPactTags => excludedPactTags;

    public IReadOnlyList<PactDefinition> Pacts => ResolvePacts();

    public Sprite GetLineScrollIcon(int tier)
    {
        if (lineScrollIcon == null || lineScrollIcon.Length == 0)
            return null;

        int index = Mathf.Clamp(tier, 1, TierCount) - 1;
        if (index < 0 || index >= lineScrollIcon.Length)
            return null;

        return lineScrollIcon[index];
    }

    public IReadOnlyList<PactDefinition> ResolvePacts(IReadOnlyList<GameplayTag> runtimeRequiredAnyPactTags = null)
    {
        if (ShouldUseLegacyList())
            return pacts;

        IReadOnlyList<PactDefinition> source = pacts;
        if (source == null || source.Count == 0)
            source = Resources.FindObjectsOfTypeAll<PactDefinition>();

        var result = new List<PactDefinition>(source.Count);

        for (int i = 0; i < source.Count; i++)
        {
            PactDefinition pact = source[i];
            if (pact == null)
                continue;

            if (!MatchesRequiredAllTags(pact.Tags))
                continue;

            if (!MatchesRequiredAnyTags(pact.Tags, runtimeRequiredAnyPactTags))
                continue;

            if (MatchesExcludedTags(pact.Tags))
                continue;

            result.Add(pact);
        }

        return result;
    }

    public bool HasAllPoolTags(IReadOnlyList<GameplayTag> requiredPoolTags)
    {
        if (requiredPoolTags == null || requiredPoolTags.Count == 0)
            return true;

        for (int i = 0; i < requiredPoolTags.Count; i++)
        {
            GameplayTag required = requiredPoolTags[i];
            if (required == null)
                continue;

            if (!PactTagUtility.ContainsTag(poolTags, required))
                return false;
        }

        return true;
    }

    private bool ShouldUseLegacyList()
    {
        return !HasConditionFilters() && pacts != null && pacts.Count > 0;
    }

    private bool HasConditionFilters()
    {
        return (requiredAllPactTags != null && requiredAllPactTags.Count > 0) ||
               (requiredAnyPactTags != null && requiredAnyPactTags.Count > 0) ||
               (excludedPactTags != null && excludedPactTags.Count > 0);
    }

    private bool MatchesRequiredAllTags(IReadOnlyList<GameplayTag> pactTags)
    {
        if (requiredAllPactTags == null || requiredAllPactTags.Count == 0)
            return true;

        for (int i = 0; i < requiredAllPactTags.Count; i++)
        {
            GameplayTag required = requiredAllPactTags[i];
            if (required == null)
                continue;

            if (!PactTagUtility.ContainsTag(pactTags, required))
                return false;
        }

        return true;
    }

    private bool MatchesRequiredAnyTags(
        IReadOnlyList<GameplayTag> pactTags,
        IReadOnlyList<GameplayTag> runtimeRequiredAnyPactTags)
    {
        bool hasBaseAny = requiredAnyPactTags != null && requiredAnyPactTags.Count > 0;
        bool hasRuntimeAny = runtimeRequiredAnyPactTags != null && runtimeRequiredAnyPactTags.Count > 0;

        if (!hasBaseAny && !hasRuntimeAny)
            return true;

        if (hasBaseAny)
        {
            for (int i = 0; i < requiredAnyPactTags.Count; i++)
            {
                GameplayTag required = requiredAnyPactTags[i];
                if (required != null && PactTagUtility.ContainsTag(pactTags, required))
                    return true;
            }
        }

        if (hasRuntimeAny)
        {
            for (int i = 0; i < runtimeRequiredAnyPactTags.Count; i++)
            {
                GameplayTag required = runtimeRequiredAnyPactTags[i];
                if (required != null && PactTagUtility.ContainsTag(pactTags, required))
                    return true;
            }
        }

        return false;
    }

    private bool MatchesExcludedTags(IReadOnlyList<GameplayTag> pactTags)
    {
        if (excludedPactTags == null || excludedPactTags.Count == 0)
            return false;

        for (int i = 0; i < excludedPactTags.Count; i++)
        {
            GameplayTag excluded = excludedPactTags[i];
            if (excluded != null && PactTagUtility.ContainsTag(pactTags, excluded))
                return true;
        }

        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureTierSlots();
        EnsureDefaultPoolTag();
        CleanupTags(poolTags);
        CleanupTags(requiredAllPactTags);
        CleanupTags(requiredAnyPactTags);
        CleanupTags(excludedPactTags);
        RefreshPactReferenceCache();
    }

    private void EnsureTierSlots()
    {
        if (lineScrollIcon != null && lineScrollIcon.Length == TierCount)
            return;

        Sprite[] resized = new Sprite[TierCount];
        if (lineScrollIcon != null)
        {
            int copyLength = Mathf.Min(lineScrollIcon.Length, resized.Length);
            for (int i = 0; i < copyLength; i++)
                resized[i] = lineScrollIcon[i];
        }

        lineScrollIcon = resized;
        EditorUtility.SetDirty(this);
    }

    private void EnsureDefaultPoolTag()
    {
        if (poolTags == null)
            poolTags = new List<GameplayTag>();

        if (PactTagUtility.ContainsTagNamed(poolTags, PactTagUtility.PullTagName))
            return;

        GameplayTag pullTag = ResolveDefaultTag(PactTagUtility.PullTagName);
        if (pullTag == null)
            return;

        poolTags.Add(pullTag);
        EditorUtility.SetDirty(this);
    }

    private void CleanupTags(List<GameplayTag> source)
    {
        if (source == null)
            return;

        bool changed = false;
        var seen = new HashSet<GameplayTag>();
        for (int i = source.Count - 1; i >= 0; i--)
        {
            GameplayTag tag = source[i];
            if (tag == null || !seen.Add(tag))
            {
                source.RemoveAt(i);
                changed = true;
            }
        }

        if (changed)
            EditorUtility.SetDirty(this);
    }

    private static GameplayTag ResolveDefaultTag(string expectedTagName)
    {
        string[] guids = AssetDatabase.FindAssets("t:GameplayTag");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameplayTag tag = AssetDatabase.LoadAssetAtPath<GameplayTag>(path);
            if (PactTagUtility.IsTagMatch(tag, expectedTagName))
                return tag;
        }

        return null;
    }

    private void RefreshPactReferenceCache()
    {
        if (!HasConditionFilters())
            return;

        string[] guids = AssetDatabase.FindAssets("t:PactDefinition");
        var resolvedPacts = new List<PactDefinition>(guids.Length);

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            PactDefinition pact = AssetDatabase.LoadAssetAtPath<PactDefinition>(path);
            if (pact != null)
                resolvedPacts.Add(pact);
        }

        bool changed = pacts == null || pacts.Count != resolvedPacts.Count;
        if (!changed && pacts != null)
        {
            for (int i = 0; i < pacts.Count; i++)
            {
                if (pacts[i] != resolvedPacts[i])
                {
                    changed = true;
                    break;
                }
            }
        }

        if (!changed)
            return;

        pacts = resolvedPacts;
        EditorUtility.SetDirty(this);
    }
#endif
}
