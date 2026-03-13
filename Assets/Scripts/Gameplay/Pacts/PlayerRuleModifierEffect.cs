using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerRuleModifierEffect", menuName = "Gameplay/Pacts/Player Rule Modifier Effect")]
public sealed class PlayerRuleModifierEffect : PlayerRuleEffect
{
    public enum Operation
    {
        Enable,
        Disable
    }

    [SerializeField] private PlayerRuleType ruleType;
    [SerializeField] private Operation operation = Operation.Enable;
    [SerializeField] private List<GameplayTag> requiredTags = new List<GameplayTag>();

    public override void Apply(PlayerRuleQuery query)
    {
        if (query == null)
            return;

        if (query.Type != ruleType)
            return;

        if (!HasRequiredTags(query))
            return;

        switch (operation)
        {
            case Operation.Enable:
                query.Value = true;
                break;
            case Operation.Disable:
                query.Value = false;
                break;
        }
    }

    private bool HasRequiredTags(PlayerRuleQuery query)
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
        string action = operation == Operation.Enable ? "Enable" : "Disable";
        string ruleLabel = PactDescriptionFormatter.HumanizeEnum(ruleType);
        return $"{action} {ruleLabel}{PactDescriptionFormatter.FormatRequiredTagsSuffix(requiredTags)}";
    }
}
