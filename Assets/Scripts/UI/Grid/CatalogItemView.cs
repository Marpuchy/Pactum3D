using UnityEngine;

public class CatalogItemView : IItem, IItemDataProvider
{
    protected readonly ItemDataSO data;

    public CatalogItemView(ItemDataSO data)
    {
        this.data = data;
    }

    public ItemDataSO Data => data;

    public string Name
    {
        get
        {
            if (data == null) return string.Empty;
            return string.IsNullOrWhiteSpace(data.DisplayName) ? data.name : data.DisplayName;
        }
    }

    public Sprite Icon => data != null ? data.Icon : null;
    public string Description => data != null ? data.Description : string.Empty;
    public ItemRaritySO Rarity => data != null ? data.Rarity : default;
    public int SellValue => data != null ? data.SellValue : 0;
    public AudioClip UseSound { get; }
    public float VolumeUse { get; }

    public virtual void Use()
    {
        // UI-only wrapper; no runtime behavior.
    }
}
