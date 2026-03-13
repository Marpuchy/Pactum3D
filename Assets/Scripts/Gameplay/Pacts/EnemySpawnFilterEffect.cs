using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemySpawnFilterEffect",
    menuName = "Gameplay/Pacts/Enemy Spawn Filter Effect")]
public sealed class EnemySpawnFilterEffect : EnemyPactEffect
{
    [SerializeField] private List<GameplayTag> requireAllTags = new List<GameplayTag>();
    [SerializeField] private List<GameplayTag> requireAnyTags = new List<GameplayTag>();
    [SerializeField] private List<GameplayTag> blockedAnyTags = new List<GameplayTag>();
    [SerializeField] private List<EnemyDefinitionSO> allowedEnemyDefinitions = new List<EnemyDefinitionSO>();
    [SerializeField] private List<EnemyDefinitionSO> blockedEnemyDefinitions = new List<EnemyDefinitionSO>();
    [SerializeField] private List<GameObject> allowedEnemyPrefabs = new List<GameObject>();
    [SerializeField] private List<GameObject> blockedEnemyPrefabs = new List<GameObject>();

    public override void Apply(EnemyStatQuery query)
    {
        if (query == null)
            return;

        if (query.Type != EnemyStatType.SpawnWeightMultiplier)
            return;

        if (IsBlockedEnemy(query))
        {
            query.Value = 0f;
            return;
        }

        if (!IsAllowedEnemy(query))
        {
            query.Value = 0f;
            return;
        }

        if (HasAnyTag(query, blockedAnyTags))
        {
            query.Value = 0f;
            return;
        }

        if (!HasAllTags(query, requireAllTags))
        {
            query.Value = 0f;
            return;
        }

        if (!HasAnyOrNone(query, requireAnyTags))
            query.Value = 0f;
    }

    private bool IsAllowedEnemy(EnemyStatQuery query)
    {
        bool hasAllowDefinitions = allowedEnemyDefinitions != null && allowedEnemyDefinitions.Count > 0;
        bool hasAllowPrefabs = allowedEnemyPrefabs != null && allowedEnemyPrefabs.Count > 0;

        if (!hasAllowDefinitions && !hasAllowPrefabs)
            return true;

        if (hasAllowDefinitions && ContainsDefinition(allowedEnemyDefinitions, query.EnemyDefinition))
            return true;

        if (hasAllowPrefabs && ContainsPrefab(allowedEnemyPrefabs, query.EnemyPrefab))
            return true;

        return false;
    }

    private bool IsBlockedEnemy(EnemyStatQuery query)
    {
        if (ContainsDefinition(blockedEnemyDefinitions, query.EnemyDefinition))
            return true;

        if (ContainsPrefab(blockedEnemyPrefabs, query.EnemyPrefab))
            return true;

        return false;
    }

    private static bool ContainsDefinition(IReadOnlyList<EnemyDefinitionSO> list, EnemyDefinitionSO definition)
    {
        if (definition == null || list == null || list.Count == 0)
            return false;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == definition)
                return true;
        }

        return false;
    }

    private static bool ContainsPrefab(IReadOnlyList<GameObject> list, GameObject prefab)
    {
        if (prefab == null || list == null || list.Count == 0)
            return false;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == prefab)
                return true;
        }

        return false;
    }

    private static bool HasAllTags(EnemyStatQuery query, IReadOnlyList<GameplayTag> requiredTags)
    {
        if (requiredTags == null || requiredTags.Count == 0)
            return true;

        for (int i = 0; i < requiredTags.Count; i++)
        {
            GameplayTag tag = requiredTags[i];
            if (tag != null && !query.HasTag(tag))
                return false;
        }

        return true;
    }

    private static bool HasAnyOrNone(EnemyStatQuery query, IReadOnlyList<GameplayTag> requiredAnyTags)
    {
        if (requiredAnyTags == null || requiredAnyTags.Count == 0)
            return true;

        return HasAnyTag(query, requiredAnyTags);
    }

    private static bool HasAnyTag(EnemyStatQuery query, IReadOnlyList<GameplayTag> tags)
    {
        if (tags == null || tags.Count == 0)
            return false;

        for (int i = 0; i < tags.Count; i++)
        {
            GameplayTag tag = tags[i];
            if (tag != null && query.HasTag(tag))
                return true;
        }

        return false;
    }

    protected override string BuildAutoDescription()
    {
        var lines = new List<string>(7);

        string allowedDefinitionsText = FormatEnemyDefinitions(allowedEnemyDefinitions);
        if (allowedDefinitionsText.Length > 0)
            lines.Add($"Enemy spawn allows only [{allowedDefinitionsText}]");

        string blockedDefinitionsText = FormatEnemyDefinitions(blockedEnemyDefinitions);
        if (blockedDefinitionsText.Length > 0)
            lines.Add($"Enemy spawn blocks [{blockedDefinitionsText}]");

        string allowedPrefabsText = FormatPrefabNames(allowedEnemyPrefabs);
        if (allowedPrefabsText.Length > 0)
            lines.Add($"Enemy spawn allows prefabs [{allowedPrefabsText}]");

        string blockedPrefabsText = FormatPrefabNames(blockedEnemyPrefabs);
        if (blockedPrefabsText.Length > 0)
            lines.Add($"Enemy spawn blocks prefabs [{blockedPrefabsText}]");

        string requireAll = PactDescriptionFormatter.FormatTagList(requireAllTags);
        if (requireAll.Length > 0)
            lines.Add($"Enemy spawn requires all [{requireAll}]");

        string requireAny = PactDescriptionFormatter.FormatTagList(requireAnyTags);
        if (requireAny.Length > 0)
            lines.Add($"Enemy spawn requires any [{requireAny}]");

        string blocked = PactDescriptionFormatter.FormatTagList(blockedAnyTags);
        if (blocked.Length > 0)
            lines.Add($"Enemy spawn blocked by [{blocked}]");

        return lines.Count == 0 ? "Enemy spawn filter" : string.Join("\n", lines);
    }

    private static string FormatEnemyDefinitions(IReadOnlyList<EnemyDefinitionSO> list)
    {
        if (list == null || list.Count == 0)
            return string.Empty;

        var names = new List<string>(list.Count);
        for (int i = 0; i < list.Count; i++)
        {
            EnemyDefinitionSO definition = list[i];
            if (definition == null)
                continue;

            string displayName = string.IsNullOrWhiteSpace(definition.DisplayName)
                ? definition.name
                : definition.DisplayName;
            if (displayName.Length > 0)
                names.Add(displayName);
        }

        return string.Join(", ", names);
    }

    private static string FormatPrefabNames(IReadOnlyList<GameObject> list)
    {
        if (list == null || list.Count == 0)
            return string.Empty;

        var names = new List<string>(list.Count);
        for (int i = 0; i < list.Count; i++)
        {
            GameObject prefab = list[i];
            if (prefab == null || string.IsNullOrWhiteSpace(prefab.name))
                continue;

            names.Add(prefab.name);
        }

        return string.Join(", ", names);
    }
}
