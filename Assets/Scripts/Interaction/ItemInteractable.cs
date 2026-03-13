using UnityEngine;

public sealed class PickupInteractable : InteractableBase
{
    [SerializeField] private ItemDataSO itemData;
    [SerializeField] private WorldItem worldItem;
    [SerializeField] private string itemId = "potion_small";
    [SerializeField] private int amount = 1;
    [SerializeField] private PickAudioEvent pickAudioEvent;

    private void Awake()
    {
        if (worldItem == null)
            worldItem = GetComponent<WorldItem>();
    }

    private void OnValidate()
    {
        if (amount < 1) amount = 1;
    }

    protected override void OnInteract(Interactor interactor)
    {
        if (interactor == null || interactor.Inventory == null)
            return;

        var data = ResolveItemData();
        if (data == null)
        {
            Debug.LogWarning($"{name}: No ItemDataSO configured for pickup.", this);
            return;
        }
        
        if (pickAudioEvent != null && data.PickSound != null)
            pickAudioEvent.Raise(data.PickSound);

        int added = 0;
        for (int i = 0; i < amount; i++)
        {
            var itemInstance = ItemFactory.CreateItem(data);
            if (itemInstance == null)
                continue;

            if (!interactor.AddItem(itemInstance))
                break;

            added++;
        }
        

        if (added <= 0)
            return;

        amount = Mathf.Max(0, amount - added);
        if (amount <= 0)
            Destroy(gameObject);
    }

    public override bool CanInteract(Interactor interactor)
    {
        if (interactor == null || interactor.Inventory == null) return false;
        return ResolveItemData() != null;
    }

    public void SetAmount(int newAmount)
    {
        amount = Mathf.Max(1, newAmount);
    }

    public void SetItemData(ItemDataSO data)
    {
        itemData = data;
        if (worldItem != null && worldItem.ItemData != data)
            worldItem.Initialize(data);
    }

    private ItemDataSO ResolveItemData()
    {
        if (itemData != null) return itemData;
        if (worldItem != null && worldItem.ItemData != null) return worldItem.ItemData;
        return null;
    }
}
