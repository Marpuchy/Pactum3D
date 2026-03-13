using UnityEngine;

[CreateAssetMenu(menuName = "Game/Items/Stats/Consumable")]
public class ConsumableStatsSO : ScriptableObject
{
    [Header("Instant Heal")]
    [Min(0f)]
    public float healAmount;

    [Header("Regeneration")]
    [Min(0f)]
    public float regenAmountPerTick;
    [Min(0f)]
    public float regenTickInterval = 1f;
    [Min(0f)]
    public float regenDuration;

    [Header("Max Health Up")]
    [Min(0f)]
    public float maxHealth;

    [Header("Speed")] 
    [Min(0f)] public float extraSpeed;
    [Min(0f)] public float speedTickInterval = 1;
    [Min(0f)] public float speedDuration;
    
    [Header("Attack")] 
    [Min(0f)] public float extraAttack;
    [Min(0f)] public float attackTickInterval = 1;
    [Min(0f)] public float attackDuration;
}
