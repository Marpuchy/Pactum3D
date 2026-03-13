using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(menuName = "Player/Player Definition", fileName = "PlayerDefinition")]
public sealed class PlayerDefinitionSO : ScriptableObject
{
    [SerializeField] private string playerId;
    [SerializeField] private string displayName;
    [SerializeField] private GameObject prefab;
    [SerializeField] private CharacterStats stats;

    [Header("Player")]
    [SerializeField] private GameplayTag playerTag;

    [Header("Movement")]
    [SerializeField] private List<GameplayTag> movementTags = new List<GameplayTag>();

    [Header("Type")]
    [SerializeField] private List<GameplayTag> typeTags = new List<GameplayTag>();

    [SerializeField, HideInInspector, FormerlySerializedAs("tags")]
    private List<GameplayTag> legacyTags = new List<GameplayTag>();

    [NonSerialized] private List<GameplayTag> mergedTags;
    [NonSerialized] private bool mergedTagsDirty = true;

    public string PlayerId => playerId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public GameObject Prefab => prefab;
    public CharacterStats Stats => stats;
    public IReadOnlyList<GameplayTag> MovementTags => movementTags;
    public IReadOnlyList<GameplayTag> Tags
    {
        get
        {
            EnsureMergedTags();
            return mergedTags;
        }
    }

    private void OnEnable()
    {
        mergedTagsDirty = true;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            EnsureId();
#endif
    }

#if UNITY_EDITOR
    private const string PlayerIdPrefix = "player_";
    private const string DefaultPlayerTagName = "Player";
    private const string DefaultLandingTagName = "Landing";

    private void OnValidate()
    {
        EnsureId();
        MigrateLegacyTags();
        EnsureDefaultPlayerTag();
        EnsureDefaultMovementTag();
        CleanupNullTags();
        mergedTagsDirty = true;
    }

    private void EnsureId()
    {
        if (!string.IsNullOrWhiteSpace(playerId))
            return;

        int nextId = ResolveNextPlayerId();
        playerId = $"{PlayerIdPrefix}{nextId:000}";
        EditorUtility.SetDirty(this);
    }

    private static int ResolveNextPlayerId()
    {
        string[] guids = AssetDatabase.FindAssets("t:PlayerDefinitionSO");
        int maxId = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            PlayerDefinitionSO existing = AssetDatabase.LoadAssetAtPath<PlayerDefinitionSO>(path);
            if (existing == null)
                continue;

            if (TryParsePlayerId(existing.playerId, out int parsedId))
                maxId = Mathf.Max(maxId, parsedId);
        }

        return maxId + 1;
    }

    private static bool TryParsePlayerId(string id, out int parsedValue)
    {
        parsedValue = 0;
        if (string.IsNullOrWhiteSpace(id))
            return false;

        string normalized = id.Trim();
        if (!normalized.StartsWith(PlayerIdPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        string numericPart = normalized.Substring(PlayerIdPrefix.Length);
        return int.TryParse(numericPart, out parsedValue) && parsedValue > 0;
    }

    private void MigrateLegacyTags()
    {
        if (legacyTags == null || legacyTags.Count == 0)
            return;

        if (typeTags == null)
            typeTags = new List<GameplayTag>();

        for (int i = 0; i < legacyTags.Count; i++)
        {
            GameplayTag tag = legacyTags[i];
            if (tag == null || typeTags.Contains(tag))
                continue;

            typeTags.Add(tag);
        }

        legacyTags.Clear();
        EditorUtility.SetDirty(this);
    }

    private void EnsureDefaultPlayerTag()
    {
        if (playerTag != null)
            return;

        GameplayTag defaultPlayerTag = ResolveDefaultTag(DefaultPlayerTagName);
        if (defaultPlayerTag == null)
            return;

        playerTag = defaultPlayerTag;
        EditorUtility.SetDirty(this);
    }

    private void EnsureDefaultMovementTag()
    {
        GameplayTag defaultLandingTag = ResolveDefaultTag(DefaultLandingTagName);
        if (defaultLandingTag == null)
            return;

        if (movementTags == null)
            movementTags = new List<GameplayTag>();

        if (movementTags.Count > 0)
            return;

        movementTags.Add(defaultLandingTag);
        EditorUtility.SetDirty(this);
    }

    private void CleanupNullTags()
    {
        RemoveNulls(movementTags);
        RemoveNulls(typeTags);
    }

    private static void RemoveNulls(List<GameplayTag> list)
    {
        if (list == null)
            return;

        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i] == null)
                list.RemoveAt(i);
        }
    }

    private static GameplayTag ResolveDefaultTag(string defaultTagName)
    {
        string[] guids = AssetDatabase.FindAssets("t:GameplayTag");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameplayTag tag = AssetDatabase.LoadAssetAtPath<GameplayTag>(path);
            if (tag == null)
                continue;

            if (string.Equals(tag.TagName, defaultTagName, StringComparison.OrdinalIgnoreCase))
                return tag;

            if (string.Equals(tag.name, $"{defaultTagName}Tag", StringComparison.OrdinalIgnoreCase))
                return tag;
        }

        return null;
    }
#endif

    private void EnsureMergedTags()
    {
        if (!mergedTagsDirty && mergedTags != null)
            return;

        if (mergedTags == null)
            mergedTags = new List<GameplayTag>(8);
        else
            mergedTags.Clear();

        AddUnique(mergedTags, playerTag);
        AddUniqueRange(mergedTags, movementTags);
        AddUniqueRange(mergedTags, typeTags);

        mergedTagsDirty = false;
    }

    private static void AddUniqueRange(List<GameplayTag> destination, List<GameplayTag> source)
    {
        if (source == null || source.Count == 0)
            return;

        for (int i = 0; i < source.Count; i++)
            AddUnique(destination, source[i]);
    }

    private static void AddUnique(List<GameplayTag> destination, GameplayTag tag)
    {
        if (tag == null || destination == null)
            return;

        if (!destination.Contains(tag))
            destination.Add(tag);
    }
}
