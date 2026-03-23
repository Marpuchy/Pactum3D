using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Game/Items/Stats/Weapon")]
public class WeaponStatsSO : EquipmentStatsSO
{
    [FormerlySerializedAs("damage")]
    [SerializeField, HideInInspector] private float legacyDamage;

    [FormerlySerializedAs("attackSpeed")]
    [SerializeField, HideInInspector] private float legacyAttackSpeed;

    public float Damage => GetFlatPreviewValue(StatType.AttackDamage);
    public float AttackSpeed => GetFlatPreviewValue(StatType.AttackSpeed);

    protected override void CollectLegacyModifiers(List<ItemStatModifierEntry> target)
    {
        AddLegacyFlatModifier(target, StatType.AttackDamage, legacyDamage);
        AddLegacyFlatModifier(target, StatType.AttackSpeed, legacyAttackSpeed);
    }
}
