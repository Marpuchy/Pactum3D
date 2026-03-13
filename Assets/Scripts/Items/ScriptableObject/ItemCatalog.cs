using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

[CreateAssetMenu(fileName = "ItemCatalog", menuName = "Items/ItemCatalog")]
public class ItemCatalog : ScriptableObject
{
    [SerializeField] private List<ItemDataSO> allItems = new();

    private Dictionary<ItemRaritySO, List<ItemDataSO>> _itemsByRarity;
    private Dictionary<string, ItemDataSO> _itemsBySaveId;

    private void OnEnable()
    {
        BuildCache();
    }

    private void BuildCache()
    {
        _itemsByRarity = new Dictionary<ItemRaritySO, List<ItemDataSO>>();
        _itemsBySaveId = new Dictionary<string, ItemDataSO>(StringComparer.Ordinal);

        foreach (var item in allItems)
        {
            if (item == null)
                continue;

            if (!string.IsNullOrWhiteSpace(item.SaveId) && !_itemsBySaveId.ContainsKey(item.SaveId))
                _itemsBySaveId[item.SaveId] = item;

            if (!string.IsNullOrWhiteSpace(item.name) && !_itemsBySaveId.ContainsKey(item.name))
                _itemsBySaveId[item.name] = item;

            if (item.Rarity == null)
                continue;

            if (!_itemsByRarity.TryGetValue(item.Rarity, out var list))
            {
                list = new List<ItemDataSO>();
                _itemsByRarity[item.Rarity] = list;
            }
            
            list.Add(item);
        }
    }

    public bool TryGetRandom(ItemRaritySO rarity, out ItemDataSO item)
    {
        item = null;

        if (rarity == null)
        {
            return false;
        }

        if (!_itemsByRarity.TryGetValue(rarity, out var list) || list.Count == 0)
        {
            return false;
        }

        item = list[Random.Range(0, list.Count)];
        return true;
    }

    public bool TryGetBySaveId(string saveId, out ItemDataSO item)
    {
        item = null;
        if (string.IsNullOrWhiteSpace(saveId))
            return false;

        if (_itemsBySaveId == null)
            BuildCache();

        return _itemsBySaveId.TryGetValue(saveId, out item);
    }

    public IReadOnlyList<ItemDataSO> AllItems => allItems;
}
