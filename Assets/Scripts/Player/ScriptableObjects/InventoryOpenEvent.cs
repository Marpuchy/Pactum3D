using System;
using UnityEngine;

[CreateAssetMenu(fileName = "InventoryOpenEvent", menuName = "Player/InventoryOpenEvent")]
public class InventoryOpenEvent : ScriptableObject
{
    public event Action<float, float, float, float, float> OnRaise;
    
    public void Raise(float health, float damage, float armor, float attackSpeed, float moveSpeed)
    {
        OnRaise?.Invoke(health, damage, armor, attackSpeed, moveSpeed);
    }
}
