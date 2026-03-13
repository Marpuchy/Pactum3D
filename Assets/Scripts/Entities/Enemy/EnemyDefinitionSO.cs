using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(menuName = "Enemy/Enemy Definition", fileName = "EnemyDefinition")]
public sealed class EnemyDefinitionSO : ScriptableObject
{
    [SerializeField] private string enemyId;
    [SerializeField] private string displayName;
    [SerializeField] private GameObject prefab;

    [Header("Enemy")]
    [SerializeField] private GameplayTag enemyTag;

    [Header("Movement")]
    [SerializeField] private List<GameplayTag> movementTags = new List<GameplayTag>();

    [Header("Type")]
    [SerializeField] private List<GameplayTag> typeTags = new List<GameplayTag>();

    [SerializeField, HideInInspector, FormerlySerializedAs("tags")]
    private List<GameplayTag> legacyTags = new List<GameplayTag>();

    [NonSerialized] private List<GameplayTag> mergedTags;
    [NonSerialized] private bool mergedTagsDirty = true;

    public string EnemyId => enemyId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public GameObject Prefab => prefab;
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
    private const string EnemyIdPrefix = "enemy_";
    private const string DefaultEnemyTagName = "Enemy";
    private const string DefaultLandingTagName = "Landing";

    private void OnValidate()
    {
        EnsureId();
        MigrateLegacyTags();
        EnsureDefaultEnemyTag();
        EnsureDefaultMovementTag();
        CleanupNullTags();
        mergedTagsDirty = true;
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
            if (tag == null)
                continue;

            if (!typeTags.Contains(tag))
                typeTags.Add(tag);
        }

        legacyTags.Clear();
        EditorUtility.SetDirty(this);
    }

    private void EnsureId()
    {
        if (!string.IsNullOrWhiteSpace(enemyId))
            return;

        int nextId = ResolveNextEnemyId();
        enemyId = $"{EnemyIdPrefix}{nextId:000}";
        EditorUtility.SetDirty(this);
    }

    private static int ResolveNextEnemyId()
    {
        string[] guids = AssetDatabase.FindAssets("t:EnemyDefinitionSO");
        int maxId = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            EnemyDefinitionSO existing = AssetDatabase.LoadAssetAtPath<EnemyDefinitionSO>(path);
            if (existing == null)
                continue;

            if (TryParseEnemyId(existing.enemyId, out int parsedId))
                maxId = Mathf.Max(maxId, parsedId);
        }

        return maxId + 1;
    }

    private static bool TryParseEnemyId(string id, out int parsedValue)
    {
        parsedValue = 0;
        if (string.IsNullOrWhiteSpace(id))
            return false;

        string normalized = id.Trim();
        if (!normalized.StartsWith(EnemyIdPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        string numericPart = normalized.Substring(EnemyIdPrefix.Length);
        return int.TryParse(numericPart, out parsedValue) && parsedValue > 0;
    }

    private void EnsureDefaultEnemyTag()
    {
        if (enemyTag != null)
            return;

        GameplayTag defaultEnemyTag = ResolveDefaultTag(DefaultEnemyTagName);
        if (defaultEnemyTag == null)
            return;

        enemyTag = defaultEnemyTag;
        EditorUtility.SetDirty(this);
    }

    private void EnsureDefaultMovementTag()
    {
        GameplayTag defaultLandingTag = ResolveDefaultTag(DefaultLandingTagName);
        if (defaultLandingTag == null)
            return;

        if (movementTags == null)
            movementTags = new List<GameplayTag>();

        // Default only when movement section is empty. Allows custom sets like Flying-only.
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

        AddUnique(mergedTags, enemyTag);
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
