using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

[Serializable]
public class LootEntry
{
    [SerializeField] private ItemDataSO item;
    [SerializeField] private int minAmount = 1;
    [SerializeField] private int maxAmount = 1;
    [Range(0f, 1f)] [SerializeField] private float probability = 1f;

    public ItemDataSO Item => item;
    public int MinAmount => minAmount;
    public int MaxAmount => maxAmount;
    public float Probability => probability;
}

[CreateAssetMenu(menuName = "Items/LootTable", fileName = "LootTable")]
public sealed class LootTableSO : ScriptableObject
{
    [SerializeField] private InventorySO inventoryPrototype;
    [SerializeField] private List<GameplayTag> lootTags = new();
    [SerializeField] private GameplayTag itemTag;
    [SerializeField] private GameplayTag coinTag;
    [SerializeField] private List<LootEntry> entries = new();

    [System.NonSerialized] private List<GameplayTag> tagBuffer;

    public InventorySO CreateInventoryInstance()
    {
        return inventoryPrototype != null
            ? Instantiate(inventoryPrototype)
            : ScriptableObject.CreateInstance<InventorySO>();
    }

    public void Fill(InventorySO target)
    {
        if (target == null) return;

        PactManager manager = PactManager.Instance;
        LootStatsProvider provider = manager != null ? manager.Loot : null;

        foreach (var entry in entries)
        {
            if (entry.Item == null) continue;
            IReadOnlyList<GameplayTag> tags = BuildTags(entry.Item.IsCurrency);

            float chance = entry.Probability;
            if (provider != null)
            {
                float chanceMultiplier = provider.Get(LootStatType.DropChanceMultiplier, 1f, tags);
                chance = Mathf.Clamp01(chance * Mathf.Max(0f, chanceMultiplier));
            }

            if (Random.value > chance) continue;

            int amount = Random.Range(entry.MinAmount, entry.MaxAmount + 1);
            if (provider != null)
            {
                if (entry.Item.IsCurrency)
                {
                    float coinMultiplier = provider.Get(LootStatType.CoinValueMultiplier, 1f, tags);
                    amount = Mathf.RoundToInt(amount * coinMultiplier);
                }
                else
                {
                    float countMultiplier = provider.Get(LootStatType.DropCountMultiplier, 1f, tags);
                    amount = Mathf.FloorToInt(amount * countMultiplier);
                }
            }

            if (amount <= 0)
                continue;

            target.Add(entry.Item, amount);
        }
    }

    private IReadOnlyList<GameplayTag> BuildTags(bool isCurrency)
    {
        bool hasBaseTags = lootTags != null && lootTags.Count > 0;
        GameplayTag typeTag = isCurrency ? coinTag : itemTag;

        if (!hasBaseTags && typeTag == null)
            return null;

        if (tagBuffer == null)
            tagBuffer = new List<GameplayTag>(8);

        tagBuffer.Clear();

        if (hasBaseTags)
            tagBuffer.AddRange(lootTags);

        if (typeTag != null)
            tagBuffer.Add(typeTag);

        return tagBuffer;
    }
}
