using System.Collections.Generic;

public sealed class LootModifierStack
{
    private readonly List<ILootStatModifier> modifiers = new List<ILootStatModifier>();

    public int Count => modifiers.Count;

    public void Register(ILootStatModifier modifier)
    {
        if (modifier == null)
            return;

        modifiers.Add(modifier);
        modifiers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    public void Unregister(ILootStatModifier modifier)
    {
        if (modifier == null)
            return;

        modifiers.Remove(modifier);
    }

    public void Clear()
    {
        modifiers.Clear();
    }

    public void Apply(LootStatQuery query)
    {
        if (query == null)
            return;

        for (int i = 0; i < modifiers.Count; i++)
            modifiers[i].Apply(query);
    }
}
