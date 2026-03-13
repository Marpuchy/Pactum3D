using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyStatModifierEffect", menuName = "Gameplay/Pacts/Enemy Stat Modifier Effect")]
public sealed class EnemyStatModifierEffect : EnemyPactEffect
{
    public enum Operation
    {
        AddFlat,
        Multiply
    }

    [SerializeField] private EnemyStatType statType;
    [SerializeField] private Operation operation = Operation.AddFlat;
    [SerializeField] private float amount = 1f;
    [SerializeField] private List<GameplayTag> requiredTags = new List<GameplayTag>();

    public override void Apply(EnemyStatQuery query)
    {
        if (query == null)
            return;

        if (query.Type != statType)
            return;

        if (!HasRequiredTags(query))
            return;

        switch (operation)
        {
            case Operation.AddFlat:
                query.Value += amount;
                break;
            case Operation.Multiply:
                query.Value *= amount;
                break;
        }
    }

    private bool HasRequiredTags(EnemyStatQuery query)
    {
        if (requiredTags == null || requiredTags.Count == 0)
            return true;

        for (int i = 0; i < requiredTags.Count; i++)
        {
            if (!query.HasTag(requiredTags[i]))
                return false;
        }

        return true;
    }

    protected override string BuildAutoDescription()
    {
        string statLabel = PactDescriptionFormatter.HumanizeEnum(statType);
        string valueText = operation == Operation.AddFlat
            ? PactDescriptionFormatter.FormatSignedValue(amount)
            : PactDescriptionFormatter.FormatMultiplier(amount);

        string baseText = operation == Operation.AddFlat
            ? $"Enemy {valueText} {statLabel}"
            : $"Enemy {statLabel} {valueText}";

        return $"{baseText}{PactDescriptionFormatter.FormatRequiredTagsSuffix(requiredTags)}";
    }
}
