using System.Collections.Generic;

public sealed class EnemyModifierStack
{
    private readonly List<IEnemyStatModifier> modifiers = new List<IEnemyStatModifier>();

    public int Count => modifiers.Count;

    public void Register(IEnemyStatModifier modifier)
    {
        if (modifier == null)
            return;

        modifiers.Add(modifier);
        modifiers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    public void Unregister(IEnemyStatModifier modifier)
    {
        if (modifier == null)
            return;

        modifiers.Remove(modifier);
    }

    public void Clear()
    {
        modifiers.Clear();
    }

    public void Apply(EnemyStatQuery query)
    {
        if (query == null)
            return;

        for (int i = 0; i < modifiers.Count; i++)
            modifiers[i].Apply(query);
    }
}
