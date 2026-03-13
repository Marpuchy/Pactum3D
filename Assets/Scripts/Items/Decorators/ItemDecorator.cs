using System;
using UnityEngine;

public class ItemDecorator : IItem, IItemDataProvider
{
    protected readonly IItem wrappedItem;

    protected ItemDecorator(IItem item)
    {
        wrappedItem = item;
    }

    public virtual string Name => wrappedItem.Name;
    public virtual Sprite Icon => wrappedItem.Icon;
    public virtual string Description => wrappedItem.Description;
    public virtual ItemRaritySO Rarity => wrappedItem.Rarity;
    public virtual int SellValue => wrappedItem.SellValue;
    public AudioClip UseSound { get; }
    public float VolumeUse { get; }
    public ItemDataSO Data => (wrappedItem as IItemDataProvider)?.Data;

    public virtual void Use()
    {
        wrappedItem.Use();
    }
}
