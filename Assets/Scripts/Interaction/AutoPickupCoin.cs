using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class AutoPickupCoin : InteractableBase
{
    [SerializeField] private ItemDataSO itemData;
    [SerializeField] private int amount = 1;

    public override InteractionMode Mode => InteractionMode.Automatic;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnValidate()
    {
        if (amount < 1) amount = 1;
    }

    protected override void OnInteract(Interactor interactor)
    {
        if (interactor == null || interactor.Inventory == null || itemData == null)
            return;

        for (int i = 0; i < amount; i++)
        {
            var itemInstance = ItemFactory.CreateItem(itemData);
            if (itemInstance != null)
                interactor.AddItem(itemInstance);
        }

        Destroy(gameObject);
    }

    public override bool CanInteract(Interactor interactor)
    {
        return interactor != null && interactor.Inventory != null && itemData != null;
    }

    public void Configure(ItemDataSO data, int newAmount)
    {
        itemData = data;
        amount = Mathf.Max(1, newAmount);
    }
}
