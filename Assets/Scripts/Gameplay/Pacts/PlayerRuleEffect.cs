using UnityEngine;

public abstract class PlayerRuleEffect : PactModifierAsset, IPlayerRuleModifier
{
    [SerializeField] private int priority;

    public int Priority => priority;

    public abstract void Apply(PlayerRuleQuery query);
}
