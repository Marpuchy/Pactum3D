using UnityEngine;

public static class StatModifierFactory
{
    public const int FlatPriority = 0;
    public const int MultiplyPriority = 100;

    public static IStatModifier Create(StatType type, StatModifierOperation operation, float value, int priorityOffset = 0)
    {
        if (IsNoOp(operation, value))
            return null;

        int priority = GetBasePriority(operation) + priorityOffset;
        return operation == StatModifierOperation.Multiply
            ? new MultiplicativeStatModifier(type, value, priority)
            : new FlatStatModifier(type, value, priority);
    }

    public static IStatModifier Create(ItemStatModifierEntry entry)
    {
        if (entry == null)
            return null;

        return Create(entry.StatType, entry.Operation, entry.Value, entry.PriorityOffset);
    }

    public static bool IsNoOp(StatModifierOperation operation, float value)
    {
        return operation == StatModifierOperation.Multiply
            ? Mathf.Approximately(value, 1f)
            : Mathf.Approximately(value, 0f);
    }

    private static int GetBasePriority(StatModifierOperation operation)
    {
        return operation == StatModifierOperation.Multiply ? MultiplyPriority : FlatPriority;
    }
}
