using UnityEngine;
using System.Collections.Generic;

public class Inventory
{
    public const int DefaultMaxSlots = 20;

    private readonly List<IItem> items = new();
    private int currencyAmount;
    private readonly int maxSlots;

    public IReadOnlyList<IItem> Items => items;
    public int CurrencyAmount => currencyAmount;
    public int MaxSlots => maxSlots;
    public int UsedSlots => items.Count;

    public Inventory(int maxSlots = DefaultMaxSlots)
    {
        this.maxSlots = Mathf.Max(1, maxSlots);
    }

    public bool AddItem(IItem item)
    {
        if (item == null)
        {
            return false;
        }

        if (item is IStackableItem stackable)
        {
            if (stackable.Data != null && stackable.Data.IsCurrency)
            {
                AddCurrency(stackable.Count);
                return true;
            }

            int index = FindStackIndex(stackable);
            if (index >= 0)
            {
                ((IStackableItem)items[index]).Add(stackable.Count);
                return true;
            }

            if (items.Count >= maxSlots)
                return false;

            items.Add(item);
            return true;
        }

        if (items.Count >= maxSlots)
            return false;

        items.Add(item);
        return true;
    }

    public bool RemoveItem(IItem item)
    {
        if (item == null)
        {
            return false;
        }

        if (item is IStackableItem stackable)
        {
            if (stackable.Data != null && stackable.Data.IsCurrency)
                return SpendCurrency(stackable.Count);

            int index = FindStackIndex(stackable);
            if (index < 0) return false;

            var existing = (IStackableItem)items[index];
            existing.Remove(1);

            if (existing.Count <= 0)
                items.RemoveAt(index);

            return true;
        }

        return items.Remove(item);
    }

    public bool RemoveAll(IItem item)
    {
        if (item == null)
        {
            return false;
        }

        if (item is IStackableItem stackable)
        {
            if (stackable.Data != null && stackable.Data.IsCurrency)
                return SpendCurrency(stackable.Count);

            int index = FindStackIndex(stackable);
            if (index < 0) return false;

            items.RemoveAt(index);
            return true;
        }

        return items.Remove(item);
    }

    public bool UseItem(IItem item)
    {
        if (item == null)
        {
            return false;
        }
        
        item.Use();

        if (item is IStackableItem stackable)
        {
            if (stackable.Data != null && stackable.Data.IsCurrency)
                return SpendCurrency(1);

            int index = FindStackIndex(stackable);
            if (index >= 0)
            {
                var existing = (IStackableItem)items[index];
                existing.Remove(1);
                if (existing.Count <= 0)
                    items.RemoveAt(index);
            }
        }

        return true;
    }

    public bool Contains(IItem item)
    {
        return items.Contains(item);
    }

    public bool CanAddItem(IItem item)
    {
        if (item == null)
            return false;

        if (item is IStackableItem stackable)
        {
            if (stackable.Data != null && stackable.Data.IsCurrency)
                return true;

            if (FindStackIndex(stackable) >= 0)
                return true;

            return items.Count < maxSlots;
        }

        return items.Count < maxSlots;
    }

    public void Clear()
    {
        items.Clear();
        currencyAmount = 0;
    }

    private int FindStackIndex(IStackableItem stackable)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] is IStackableItem existing && existing.Data == stackable.Data)
                return i;
        }

        return -1;
    }

    public void AddCurrency(int amount)
    {
        if (amount <= 0) return;
        currencyAmount += amount;
    }

    public bool SpendCurrency(int amount)
    {
        if (amount <= 0) return false;
        if (currencyAmount < amount) return false;
        currencyAmount -= amount;
        return true;
    }
}
