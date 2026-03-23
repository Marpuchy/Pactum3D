using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Game/Items/Stats/Armor")]
public class ArmorStatsSO : EquipmentStatsSO
{
    [FormerlySerializedAs("defense")]
    [SerializeField, HideInInspector] private float legacyDefense;

    [FormerlySerializedAs("healthBonus")]
    [SerializeField, HideInInspector] private float legacyHealthBonus;

    public float Defense => GetFlatPreviewValue(StatType.ShieldArmor);
    public float HealthBonus => GetFlatPreviewValue(StatType.MaxHealth);

    protected override void CollectLegacyModifiers(List<ItemStatModifierEntry> target)
    {
        AddLegacyFlatModifier(target, StatType.ShieldArmor, legacyDefense);
        AddLegacyFlatModifier(target, StatType.MaxHealth, legacyHealthBonus);
    }
}
