using System.Collections.Generic;

public sealed class StatsProvider
{
    private readonly ModifierStack modifierStack;

    public StatsProvider(ModifierStack modifierStack)
    {
        this.modifierStack = modifierStack;
    }

    public float Get(StatType type, float baseValue, IReadOnlyList<GameplayTag> tags)
    {
        if (modifierStack == null)
            return baseValue;

        StatQuery query = new StatQuery(type, baseValue, tags);
        modifierStack.Apply(query);
        return query.Value;
    }

    public IReadOnlyList<GameplayTag> GetEffectiveTags(IReadOnlyList<GameplayTag> tags = null)
    {
        if (modifierStack == null)
            return tags;

        StatQuery query = new StatQuery(StatType.MaxHealth, 0f, tags);
        modifierStack.Apply(query);
        return query.Tags;
    }
}
