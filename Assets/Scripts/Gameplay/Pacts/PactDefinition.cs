using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using System;
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "PactDefinition", menuName = "Gameplay/Pacts/PactDefinition")]
public class PactDefinition : ScriptableObject
{
    private const string PactIdPrefix = "pact_";

    [Header("Save")]
    [SerializeField] private string saveId;

    [SerializeField] private string title;
    [TextArea]
    [SerializeField, FormerlySerializedAs("legacyDescription")] private string description;
    [SerializeField] private Sprite icon;

    [Header("Legacy Line (Auto-Migrated To Tags)")]
    [SerializeField, HideInInspector] private PactLineSO line;
    [SerializeField, HideInInspector, FormerlySerializedAs("lineTier")] private int legacyLineTier;

    [Header("Tags")]
    [SerializeField] private List<GameplayTag> tags = new List<GameplayTag>();

    [SerializeField] private PactDefinition requiredPreviousPact;

    [Header("Effects")]
    [SerializeField] private List<PactModifierAsset> effects = new List<PactModifierAsset>();
    

    public string Title => string.IsNullOrWhiteSpace(title) ? name : title;
    public string SaveId => string.IsNullOrWhiteSpace(saveId) ? name : saveId;
    public string Description => BuildDescription();
    public Sprite Icon => icon;
    public PactLineSO Line => line;
    public IReadOnlyList<GameplayTag> Tags => tags;
    public GameplayTag LineTag => PactTagUtility.ResolveLineTag(tags);
    public int LineTier
    {
        get
        {
            int tierFromTags = PactTagUtility.ResolveTier(tags);
            if (tierFromTags > 0)
                return tierFromTags;

            string lineId = PactIdentity.ResolveLineId(this);
            if (lineId.Length == 0)
                return 0;

            return Mathf.Max(1, legacyLineTier);
        }
    }

    public PactDefinition RequiredPreviousPact => requiredPreviousPact;
    public IReadOnlyList<PactModifierAsset> Effects => effects;

#if UNITY_EDITOR
    private void OnEnable()
    {
        if (!Application.isPlaying)
            EnsureId();
    }
#endif
    

    public bool HasTag(GameplayTag tag)
    {
        return PactTagUtility.ContainsTag(tags, tag);
    }

    public bool HasTagNamed(string tagName)
    {
        return PactTagUtility.ContainsTagNamed(tags, tagName);
    }

    private string BuildDescription()
    {
        string manualDescription = NormalizeDescription(description);
        string effectsDescription = BuildEffectsDescription();

        var sections = new List<string>(2);
        if (manualDescription.Length > 0)
            sections.Add(manualDescription);

        if (effectsDescription.Length > 0)
            sections.Add(effectsDescription);

        return sections.Count == 0 ? string.Empty : string.Join("\n", sections);
    }

    private string BuildEffectsDescription()
    {
        if (effects == null || effects.Count == 0)
            return string.Empty;

        var lines = new List<string>(effects.Count);
        for (int i = 0; i < effects.Count; i++)
        {
            PactModifierAsset effect = effects[i];
            if (effect == null)
                continue;

            string effectDescription = NormalizeDescription(effect.Description);
            if (effectDescription.Length == 0)
                continue;

            lines.Add($"- {effectDescription}");
        }

        return lines.Count == 0 ? string.Empty : string.Join("\n", lines);
    }

    private static string NormalizeDescription(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private void MigrateLegacyLineAndTierToTags()
    {
        if (tags == null)
            tags = new List<GameplayTag>();

#if UNITY_EDITOR
        bool changed = false;

        if (line != null && LineTag == null)
        {
            GameplayTag lineTag = ResolveLineTagFromLegacyLine(line);
            if (lineTag != null && !PactTagUtility.ContainsTag(tags, lineTag))
            {
                tags.Add(lineTag);
                changed = true;
            }
        }

        int tierFromTags = PactTagUtility.ResolveTier(tags);
        if (tierFromTags <= 0 && legacyLineTier > 0)
        {
            GameplayTag tierTag = ResolveDefaultTag(PactTagUtility.ResolveTierTagName(legacyLineTier));
            if (tierTag != null && !PactTagUtility.ContainsTag(tags, tierTag))
            {
                tags.Add(tierTag);
                changed = true;
            }
        }

        if (changed)
            EditorUtility.SetDirty(this);
#endif
    }

    private void EnsureDefaultPactTag()
    {
        if (tags == null)
            tags = new List<GameplayTag>();

        if (PactTagUtility.ContainsTagNamed(tags, PactTagUtility.PactTagName))
            return;

#if UNITY_EDITOR
        GameplayTag defaultTag = ResolveDefaultTag(PactTagUtility.PactTagName);
        if (defaultTag == null)
            return;

        tags.Add(defaultTag);
        EditorUtility.SetDirty(this);
#endif
    }

    private void CleanupTags()
    {
        if (tags == null)
            return;

#if UNITY_EDITOR
        bool changed = false;
        var seen = new HashSet<GameplayTag>();
        for (int i = tags.Count - 1; i >= 0; i--)
        {
            GameplayTag tag = tags[i];
            if (tag == null || !seen.Add(tag))
            {
                tags.RemoveAt(i);
                changed = true;
            }
        }

        if (changed)
            EditorUtility.SetDirty(this);
#endif
    }

    private bool AddUnique<T>(List<T> source) where T : PactModifierAsset
    {
        if (source == null || source.Count == 0)
            return false;

        bool changed = false;
        for (int i = 0; i < source.Count; i++)
        {
            T effect = source[i];
            if (effect == null)
                continue;

            if (effects.Contains(effect))
                continue;

            effects.Add(effect);
            changed = true;
        }

        return changed;
    }

#if UNITY_EDITOR
    private void EnsureId()
    {
        if (!string.IsNullOrWhiteSpace(saveId) && TryParsePactId(saveId, out _))
            return;

        int nextId = ResolveNextPactId();
        saveId = $"{PactIdPrefix}{nextId:000}";
        EditorUtility.SetDirty(this);
    }

    private static int ResolveNextPactId()
    {
        string[] guids = AssetDatabase.FindAssets("t:PactDefinition");
        int maxId = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            PactDefinition existing = AssetDatabase.LoadAssetAtPath<PactDefinition>(path);
            if (existing == null)
                continue;

            if (TryParsePactId(existing.saveId, out int parsedId))
                maxId = Mathf.Max(maxId, parsedId);
        }

        return maxId + 1;
    }

    private static bool TryParsePactId(string id, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(id))
            return false;

        string normalized = id.Trim();
        if (normalized.StartsWith(PactIdPrefix, StringComparison.OrdinalIgnoreCase))
            normalized = normalized.Substring(PactIdPrefix.Length);

        return int.TryParse(normalized, out value) && value > 0;
    }

    private static GameplayTag ResolveLineTagFromLegacyLine(PactLineSO legacyLine)
    {
        if (legacyLine == null)
            return null;

        string baseName = PactTagUtility.RemoveTagSuffix(legacyLine.name);
        if (baseName.Length == 0)
            return null;

        return ResolveDefaultTag($"{baseName}Tag");
    }

    private static GameplayTag ResolveDefaultTag(string expectedTagName)
    {
        if (string.IsNullOrWhiteSpace(expectedTagName))
            return null;

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
#endif
}
