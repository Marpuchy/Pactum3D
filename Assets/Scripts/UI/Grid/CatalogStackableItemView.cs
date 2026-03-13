using UnityEngine;

public sealed class CatalogStackableItemView : CatalogItemView, IStackableItem
{
    private int count;

    public CatalogStackableItemView(ItemDataSO data, int count = 1) : base(data)
    {
        this.count = Mathf.Max(1, count);
    }

    public ItemDataSO Data => data;
    public int Count => count;

    public void Add(int amount)
    {
        if (amount <= 0) return;
        count += amount;
    }

    public void Remove(int amount)
    {
        if (amount <= 0) return;
        count = Mathf.Max(0, count - amount);
    }
}
