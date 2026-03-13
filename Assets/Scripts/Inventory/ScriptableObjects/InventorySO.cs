using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Inventory/Inventory", fileName = "Inventory")]
public sealed class InventorySO : ScriptableObject
{
    [SerializeField] private List<ItemStack> items = new();

    public IReadOnlyList<ItemStack> Items => items;

    public void Add(ItemDataSO item, int amount)
    {
        if (item == null || amount <= 0) return;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].Item == item)
            {
                items[i] = items[i].WithAmount(items[i].Amount + amount);
                return;
            }
        }

        items.Add(new ItemStack(item, amount));
    }

    public bool Remove(ItemDataSO item, int amount)
    {
        if (item == null || amount <= 0) return false;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].Item != item) continue;

            int remaining = items[i].Amount - amount;
            if (remaining < 0) return false;

            if (remaining == 0)
                items.RemoveAt(i);
            else
                items[i] = items[i].WithAmount(remaining);

            return true;
        }

        return false;
    }

    public void Clear()
    {
        items.Clear();
    }
}
