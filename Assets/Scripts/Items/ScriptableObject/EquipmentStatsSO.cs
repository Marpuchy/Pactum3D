using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public abstract class EquipmentStatsSO : ScriptableObject
{
    [Header("Modifiers")]
    [SerializeField] private List<ItemStatModifierEntry> modifiers = new List<ItemStatModifierEntry>();
    [SerializeField, HideInInspector] private bool legacyModifiersMigrated;

    public IReadOnlyList<ItemStatModifierEntry> Modifiers
    {
        get
        {
            EnsureLegacyModifiersMigrated();
            return modifiers;
        }
    }

    protected virtual void OnEnable()
    {
        EnsureLegacyModifiersMigrated();
    }

    protected virtual void OnValidate()
    {
        EnsureLegacyModifiersMigrated();
    }

    protected abstract void CollectLegacyModifiers(List<ItemStatModifierEntry> target);

    protected float GetFlatPreviewValue(StatType statType, float fallback = 0f)
    {
        EnsureLegacyModifiersMigrated();

        bool found = false;
        float value = 0f;
        for (int i = 0; i < modifiers.Count; i++)
        {
            ItemStatModifierEntry modifier = modifiers[i];
            if (modifier == null || modifier.StatType != statType || modifier.Operation != StatModifierOperation.AddFlat)
                continue;

            value += modifier.Value;
            found = true;
        }

        return found ? value : fallback;
    }

    protected static void AddLegacyFlatModifier(List<ItemStatModifierEntry> target, StatType statType, float value)
    {
        if (target == null || Mathf.Approximately(value, 0f))
            return;

        target.Add(new ItemStatModifierEntry(statType, StatModifierOperation.AddFlat, value));
    }

    private void EnsureLegacyModifiersMigrated()
    {
        if (legacyModifiersMigrated)
            return;

        if (modifiers == null)
            modifiers = new List<ItemStatModifierEntry>();

        if (modifiers.Count == 0)
            CollectLegacyModifiers(modifiers);

        legacyModifiersMigrated = true;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorUtility.SetDirty(this);
#endif
    }
}
