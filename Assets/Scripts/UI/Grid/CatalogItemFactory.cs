using System.Collections.Generic;
using UnityEngine;

public static class CatalogItemFactory
{
    public static void FillFromCatalog(ShopCatalogSO catalog, List<IItem> output)
    {
        if (output == null) return;
        output.Clear();

        if (catalog == null || catalog.Items == null) return;

        var entries = catalog.Items;
        for (int i = 0; i < entries.Count; i++)
        {
            var data = entries[i].item;
            if (data == null) continue;

            if (data.Stackable || data.IsCurrency)
                output.Add(new CatalogStackableItemView(data, 1));
            else
                output.Add(new CatalogItemView(data));
        }
    }

    public static void FillFromStacks(IReadOnlyList<ItemStack> stacks, List<IItem> output)
    {
        if (output == null) return;
        output.Clear();

        if (stacks == null) return;

        for (int i = 0; i < stacks.Count; i++)
        {
            var data = stacks[i].Item;
            if (data == null) continue;

            int amount = Mathf.Max(1, stacks[i].Amount);
            output.Add(new CatalogStackableItemView(data, amount));
        }
    }
}
