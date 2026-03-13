using System;
using UnityEngine;

[Serializable]
public struct ItemStack
{
    [SerializeField] private ItemDataSO item;
    [SerializeField] private int amount;

    public ItemDataSO Item => item;
    public int Amount => amount;

    public ItemStack(ItemDataSO item, int amount)
    {
        this.item = item;
        this.amount = amount;
    }

    public ItemStack WithAmount(int newAmount)
    {
        return new ItemStack(item, newAmount);
    }
}
