using System.Collections.Generic;
using UnityEngine;

public sealed class EnemyStatsProvider
{
    private readonly EnemyModifierStack modifierStack;
    private readonly GameplayTag domainTag;
    private readonly List<GameplayTag> tagBuffer = new List<GameplayTag>(8);

    public EnemyStatsProvider(EnemyModifierStack modifierStack, GameplayTag domainTag = null)
    {
        this.modifierStack = modifierStack;
        this.domainTag = domainTag;
    }

    public float Get(
        EnemyStatType type,
        float baseValue,
        IReadOnlyList<GameplayTag> tags = null,
        EnemyDefinitionSO enemyDefinition = null,
        GameObject enemyPrefab = null)
    {
        if (modifierStack == null)
            return baseValue;

        EnemyStatQuery query = new EnemyStatQuery(
            type,
            baseValue,
            BuildTags(tags),
            enemyDefinition,
            enemyPrefab);
        modifierStack.Apply(query);
        return query.Value;
    }

    public float Get(EnemyStatType type, float baseValue, params GameplayTag[] tags)
    {
        if (modifierStack == null)
            return baseValue;

        EnemyStatQuery query = new EnemyStatQuery(type, baseValue, BuildTags(tags));
        modifierStack.Apply(query);
        return query.Value;
    }

    public IReadOnlyList<GameplayTag> GetEffectiveTags(IReadOnlyList<GameplayTag> tags = null)
    {
        if (modifierStack == null)
            return tags;

        EnemyStatQuery query = new EnemyStatQuery(EnemyStatType.MaxHealth, 0f, BuildTags(tags));
        modifierStack.Apply(query);
        return query.Tags;
    }

    private IReadOnlyList<GameplayTag> BuildTags(IReadOnlyList<GameplayTag> tags)
    {
        if (domainTag == null)
            return tags;

        tagBuffer.Clear();
        tagBuffer.Add(domainTag);

        if (tags != null)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                GameplayTag tag = tags[i];
                if (tag != null)
                    tagBuffer.Add(tag);
            }
        }

        return tagBuffer;
    }
}
