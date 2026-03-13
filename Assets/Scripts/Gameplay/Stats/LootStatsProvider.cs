using System.Collections.Generic;

public sealed class LootStatsProvider
{
    private readonly LootModifierStack modifierStack;
    private readonly GameplayTag domainTag;
    private readonly List<GameplayTag> tagBuffer = new List<GameplayTag>(8);

    public LootStatsProvider(LootModifierStack modifierStack, GameplayTag domainTag = null)
    {
        this.modifierStack = modifierStack;
        this.domainTag = domainTag;
    }

    public float Get(LootStatType type, float baseValue, IReadOnlyList<GameplayTag> tags = null)
    {
        if (modifierStack == null)
            return baseValue;

        LootStatQuery query = new LootStatQuery(type, baseValue, BuildTags(tags));
        modifierStack.Apply(query);
        return query.Value;
    }

    public float Get(LootStatType type, float baseValue, params GameplayTag[] tags)
    {
        if (modifierStack == null)
            return baseValue;

        LootStatQuery query = new LootStatQuery(type, baseValue, BuildTags(tags));
        modifierStack.Apply(query);
        return query.Value;
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
