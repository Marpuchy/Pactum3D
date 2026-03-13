using System.Collections.Generic;

public sealed class RoomStatsProvider
{
    private readonly RoomModifierStack modifierStack;
    private readonly GameplayTag domainTag;
    private readonly List<GameplayTag> tagBuffer = new List<GameplayTag>(8);

    public RoomStatsProvider(RoomModifierStack modifierStack, GameplayTag domainTag = null)
    {
        this.modifierStack = modifierStack;
        this.domainTag = domainTag;
    }

    public float Get(RoomParamType type, float baseValue, IReadOnlyList<GameplayTag> tags = null)
    {
        if (modifierStack == null)
            return baseValue;

        RoomParamQuery query = new RoomParamQuery(type, baseValue, BuildTags(tags));
        modifierStack.Apply(query);
        return query.Value;
    }

    public float Get(RoomParamType type, float baseValue, params GameplayTag[] tags)
    {
        if (modifierStack == null)
            return baseValue;

        RoomParamQuery query = new RoomParamQuery(type, baseValue, BuildTags(tags));
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
