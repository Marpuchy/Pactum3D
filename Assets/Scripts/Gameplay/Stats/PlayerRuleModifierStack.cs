using System.Collections.Generic;

public sealed class PlayerRuleModifierStack
{
    private readonly List<IPlayerRuleModifier> modifiers = new List<IPlayerRuleModifier>();

    public int Count => modifiers.Count;

    public void Register(IPlayerRuleModifier modifier)
    {
        if (modifier == null)
            return;

        modifiers.Add(modifier);
        modifiers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    public void Unregister(IPlayerRuleModifier modifier)
    {
        if (modifier == null)
            return;

        modifiers.Remove(modifier);
    }

    public void Clear()
    {
        modifiers.Clear();
    }

    public void Apply(PlayerRuleQuery query)
    {
        if (query == null)
            return;

        for (int i = 0; i < modifiers.Count; i++)
            modifiers[i].Apply(query);
    }
}
