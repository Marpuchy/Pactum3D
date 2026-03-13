using UnityEngine;

public sealed class StackedItem : IStackableItem
{
    private readonly IItem wrapped;
    private readonly ItemDataSO data;
    private int count;

    public StackedItem(IItem wrapped, ItemDataSO data, int initialCount = 1)
    {
        this.wrapped = wrapped;
        this.data = data;
        count = Mathf.Max(1, initialCount);
    }

    public ItemDataSO Data => data;
    public int Count => count;

    public string Name => wrapped.Name;
    public Sprite Icon => wrapped.Icon;
    public string Description => wrapped.Description;
    public ItemRaritySO Rarity => wrapped.Rarity;
    public int SellValue => wrapped.SellValue;
    public AudioClip UseSound { get; }
    public float VolumeUse { get; }

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

    public void Use()
    {
        if (count <= 0) return;
        wrapped.Use();
    }
}
