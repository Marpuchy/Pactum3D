using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct ShopItemEntry
{
    public ItemDataSO item;
    public int priceOverride;
}

[CreateAssetMenu(menuName = "Shop/Shop Catalog", fileName = "ShopCatalog")]
public sealed class ShopCatalogSO : ScriptableObject
{
    [SerializeField] private string shopId;
    [SerializeField] private string displayName;
    [SerializeField] private List<ShopItemEntry> items = new();

    public string ShopId => shopId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public IReadOnlyList<ShopItemEntry> Items => items;

    public int GetPrice(ShopItemEntry entry)
    {
        if (entry.priceOverride > 0)
            return entry.priceOverride;

        return entry.item != null ? entry.item.SellValue : 0;
    }
}
