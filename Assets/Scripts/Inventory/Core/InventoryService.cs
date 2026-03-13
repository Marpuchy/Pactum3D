using UnityEngine;

public class InventoryService
{
    public Inventory Inventory { get; } = new();

    public bool Add(IItem item)
    {
        return Inventory.AddItem(item);
    }

    public bool Use(IItem item)
    {
        return Inventory.UseItem(item);
    }

    public bool Remove(IItem item)
    {
        return Inventory.RemoveItem(item);
    }
}
