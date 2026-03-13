using UnityEngine;

public abstract class EnemyPactEffect : PactModifierAsset, IEnemyStatModifier
{
    [SerializeField] private int priority;

    public int Priority => priority;

    public abstract void Apply(EnemyStatQuery query);
}
