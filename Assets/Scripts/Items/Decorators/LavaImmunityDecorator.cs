using UnityEngine;

public sealed class LavaImmunityDecorator : ItemDecorator, ILavaImmunity
{
    public bool IsImmuneToLava => true;

    public LavaImmunityDecorator(IItem item) : base(item)
    {
    }
}
