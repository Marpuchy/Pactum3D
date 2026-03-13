using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(menuName = "NPC/NPC Definition", fileName = "NpcDefinition")]
public sealed class NpcDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string npcId;
    [SerializeField] private string displayName;

    [Header("Visuals")]
    [SerializeField] private Sprite inGameSprite;
    [SerializeField] private Sprite pactCanvasSprite;

    [Header("Runtime")]
    [SerializeField] private GameObject sharedPrefab;
    [Header("Pact Pools (Required)")]
    [SerializeField] private PactPoolSO generalPactPool;
    [SerializeField] private PactPoolSO ownPactPool;
    [SerializeField] private List<NpcExtraPactPullEntry> extraPactPulls = new List<NpcExtraPactPullEntry>();
    [SerializeField, HideInInspector, FormerlySerializedAs("pactPools")]
    private List<PactPoolSO> legacyPactPools = new List<PactPoolSO>();

    [Header("Tags")]
    [SerializeField] private List<GameplayTag> tags = new List<GameplayTag>();

    public string NpcId => npcId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public Sprite InGameSprite => inGameSprite;
    public Sprite PactCanvasSprite => pactCanvasSprite;
    public GameObject SharedPrefab => sharedPrefab;
    public PactPoolSO GeneralPactPool => generalPactPool;
    public PactPoolSO OwnPactPool => ownPactPool;
    public IReadOnlyList<NpcExtraPactPullEntry> ExtraPactPulls => extraPactPulls;
    public IReadOnlyList<GameplayTag> Tags => tags;

#if UNITY_EDITOR
    private const string NpcIdPrefix = "npc_";
    private const string DefaultNpcTagName = "NPCPactTag";

    private void OnEnable()
    {
        if (!Application.isPlaying)
            EnsureId();
    }

    private void OnValidate()
    {
        EnsureId();
        MigrateLegacyPools();
        CleanupExtraPactPulls();
        EnsureDefaultTag();
        CleanupTags();
    }

    private void MigrateLegacyPools()
    {
        bool changed = false;

        if (generalPactPool == null && legacyPactPools != null && legacyPactPools.Count > 0 && legacyPactPools[0] != null)
        {
            generalPactPool = legacyPactPools[0];
            changed = true;
        }

        if (ownPactPool == null && legacyPactPools != null && legacyPactPools.Count > 1 && legacyPactPools[1] != null)
        {
            ownPactPool = legacyPactPools[1];
            changed = true;
        }

        if (legacyPactPools != null && legacyPactPools.Count > 2)
        {
            if (extraPactPulls == null)
                extraPactPulls = new List<NpcExtraPactPullEntry>();

            for (int i = 2; i < legacyPactPools.Count; i++)
            {
                PactPoolSO legacyPool = legacyPactPools[i];
                if (legacyPool == null || ContainsExtraPull(legacyPool))
                    continue;

                // Legacy pools beyond index 1 are migrated as General-like extras.
                extraPactPulls.Add(new NpcExtraPactPullEntry(legacyPool, NpcExtraPactPullMode.LikeGeneral));
                changed = true;
            }
        }

        if (changed)
            EditorUtility.SetDirty(this);
    }

    private bool ContainsExtraPull(PactPoolSO pool)
    {
        if (pool == null || extraPactPulls == null)
            return false;

        for (int i = 0; i < extraPactPulls.Count; i++)
        {
            NpcExtraPactPullEntry entry = extraPactPulls[i];
            if (entry != null && entry.Pool == pool)
                return true;
        }

        return false;
    }

    private void CleanupExtraPactPulls()
    {
        if (extraPactPulls == null)
        {
            extraPactPulls = new List<NpcExtraPactPullEntry>();
            EditorUtility.SetDirty(this);
            return;
        }

        bool changed = false;
        var seenPools = new HashSet<PactPoolSO>();
        for (int i = extraPactPulls.Count - 1; i >= 0; i--)
        {
            NpcExtraPactPullEntry entry = extraPactPulls[i];
            if (entry == null || entry.Pool == null || !seenPools.Add(entry.Pool))
            {
                extraPactPulls.RemoveAt(i);
                changed = true;
            }
        }

        if (changed)
            EditorUtility.SetDirty(this);
    }

    private void EnsureId()
    {
        if (!string.IsNullOrWhiteSpace(npcId))
            return;

        int nextId = ResolveNextNpcId();
        npcId = $"{NpcIdPrefix}{nextId:000}";
        EditorUtility.SetDirty(this);
    }

    private static int ResolveNextNpcId()
    {
        string[] guids = AssetDatabase.FindAssets("t:NpcDefinitionSO");
        int maxId = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            NpcDefinitionSO existing = AssetDatabase.LoadAssetAtPath<NpcDefinitionSO>(path);
            if (existing == null)
                continue;

            if (TryParseNpcId(existing.npcId, out int parsedId))
                maxId = Mathf.Max(maxId, parsedId);
        }

        return maxId + 1;
    }

    private static bool TryParseNpcId(string id, out int parsedValue)
    {
        parsedValue = 0;
        if (string.IsNullOrWhiteSpace(id))
            return false;

        string normalized = id.Trim();
        if (!normalized.StartsWith(NpcIdPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        string numericPart = normalized.Substring(NpcIdPrefix.Length);
        return int.TryParse(numericPart, out parsedValue) && parsedValue > 0;
    }

    private void EnsureDefaultTag()
    {
        if (tags == null)
            tags = new List<GameplayTag>();

        for (int i = 0; i < tags.Count; i++)
        {
            GameplayTag existing = tags[i];
            if (existing != null && IsTagMatch(existing, DefaultNpcTagName))
                return;
        }

        GameplayTag defaultTag = ResolveDefaultTag(DefaultNpcTagName);
        if (defaultTag == null)
            return;

        tags.Add(defaultTag);
        EditorUtility.SetDirty(this);
    }

    private void CleanupTags()
    {
        if (tags == null)
            return;

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
    }

    private static GameplayTag ResolveDefaultTag(string defaultTagName)
    {
        string[] guids = AssetDatabase.FindAssets("t:GameplayTag");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameplayTag tag = AssetDatabase.LoadAssetAtPath<GameplayTag>(path);
            if (tag != null && IsTagMatch(tag, defaultTagName))
                return tag;
        }

        return null;
    }

    private static bool IsTagMatch(GameplayTag tag, string expectedTagName)
    {
        if (tag == null || string.IsNullOrWhiteSpace(expectedTagName))
            return false;

        string normalizedExpected = expectedTagName.Trim();
        string expectedWithoutSuffix = normalizedExpected.EndsWith("Tag", StringComparison.OrdinalIgnoreCase)
            ? normalizedExpected.Substring(0, normalizedExpected.Length - 3)
            : normalizedExpected;
        string expectedWithSuffix = normalizedExpected.EndsWith("Tag", StringComparison.OrdinalIgnoreCase)
            ? normalizedExpected
            : $"{normalizedExpected}Tag";

        return string.Equals(tag.TagName, normalizedExpected, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tag.TagName, expectedWithoutSuffix, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tag.TagName, expectedWithSuffix, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tag.name, normalizedExpected, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tag.name, expectedWithoutSuffix, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tag.name, expectedWithSuffix, StringComparison.OrdinalIgnoreCase);
    }
#endif
}

[Serializable]
public sealed class NpcExtraPactPullEntry
{
    [SerializeField] private PactPoolSO pool;
    [SerializeField] private NpcExtraPactPullMode mode = NpcExtraPactPullMode.LikeGeneral;

    public NpcExtraPactPullEntry()
    {
    }

    public NpcExtraPactPullEntry(PactPoolSO pool, NpcExtraPactPullMode mode)
    {
        this.pool = pool;
        this.mode = mode;
    }

    public PactPoolSO Pool => pool;
    public NpcExtraPactPullMode Mode => mode;
}

public enum NpcExtraPactPullMode
{
    LikeGeneral = 0,
    LikeOwn = 1
}
