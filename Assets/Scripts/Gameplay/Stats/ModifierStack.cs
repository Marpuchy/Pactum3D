using System.Collections.Generic;

public sealed class ModifierStack
{
    private readonly List<IStatModifier> modifiers = new List<IStatModifier>();

    public int Count => modifiers.Count;

    public void Register(IStatModifier modifier)
    {
        if (modifier == null)
            return;

        modifiers.Add(modifier);
        modifiers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    public void Unregister(IStatModifier modifier)
    {
        if (modifier == null)
            return;

        modifiers.Remove(modifier);
    }

    public void Clear()
    {
        modifiers.Clear();
    }

    public void Apply(StatQuery query)
    {
        if (query == null)
            return;

        for (int i = 0; i < modifiers.Count; i++)
            modifiers[i].Apply(query);
    }
}
