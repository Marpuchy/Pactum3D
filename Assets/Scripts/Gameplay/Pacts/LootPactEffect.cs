using UnityEngine;

public abstract class LootPactEffect : PactModifierAsset, ILootStatModifier
{
    [SerializeField] private int priority;

    public int Priority => priority;

    public abstract void Apply(LootStatQuery query);
}
