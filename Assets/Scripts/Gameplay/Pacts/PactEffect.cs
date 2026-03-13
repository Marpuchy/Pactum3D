using UnityEngine;

public abstract class PactEffect : PactModifierAsset, IStatModifier
{
    [SerializeField] private int priority;

    public int Priority => priority;

    public abstract void Apply(StatQuery query);
}
