using System.Collections.Generic;

public sealed class PlayerRuleProvider
{
    private readonly PlayerRuleModifierStack modifierStack;

    public PlayerRuleProvider(PlayerRuleModifierStack modifierStack)
    {
        this.modifierStack = modifierStack;
    }

    public bool Get(PlayerRuleType type, bool baseValue, IReadOnlyList<GameplayTag> tags)
    {
        if (modifierStack == null)
            return baseValue;

        PlayerRuleQuery query = new PlayerRuleQuery(type, baseValue, tags);
        modifierStack.Apply(query);
        return query.Value;
    }

    public bool Get(PlayerRuleType type, bool baseValue, params GameplayTag[] tags)
    {
        if (modifierStack == null)
            return baseValue;

        PlayerRuleQuery query = new PlayerRuleQuery(type, baseValue, tags);
        modifierStack.Apply(query);
        return query.Value;
    }
}
