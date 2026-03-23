using System;
using UnityEngine;

[Serializable]
public sealed class ItemStatModifierEntry
{
    [SerializeField] private StatType statType;
    [SerializeField] private StatModifierOperation operation = StatModifierOperation.AddFlat;
    [SerializeField] private float value;
    [SerializeField] private int priorityOffset;

    public ItemStatModifierEntry()
    {
    }

    public ItemStatModifierEntry(StatType statType, StatModifierOperation operation, float value, int priorityOffset = 0)
    {
        this.statType = statType;
        this.operation = operation;
        this.value = value;
        this.priorityOffset = priorityOffset;
    }

    public StatType StatType => statType;
    public StatModifierOperation Operation => operation;
    public float Value => value;
    public int PriorityOffset => priorityOffset;
}
