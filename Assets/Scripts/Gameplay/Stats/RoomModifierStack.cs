using System.Collections.Generic;

public sealed class RoomModifierStack
{
    private readonly List<IRoomParamModifier> modifiers = new List<IRoomParamModifier>();

    public int Count => modifiers.Count;

    public void Register(IRoomParamModifier modifier)
    {
        if (modifier == null)
            return;

        modifiers.Add(modifier);
        modifiers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    public void Unregister(IRoomParamModifier modifier)
    {
        if (modifier == null)
            return;

        modifiers.Remove(modifier);
    }

    public void Clear()
    {
        modifiers.Clear();
    }

    public void Apply(RoomParamQuery query)
    {
        if (query == null)
            return;

        for (int i = 0; i < modifiers.Count; i++)
            modifiers[i].Apply(query);
    }
}
