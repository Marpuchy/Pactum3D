using UnityEngine;

public sealed class ChestInteractable : InteractableBase
{
    [SerializeField] private InventorySO chestInventory;
    [SerializeField] private LootTableSO lootTable;
    [SerializeField] private OpenChestEventChannelSO openChestChannel;

    private InventorySO runtimeInventory;
    private bool initialized;

    protected override void OnInteract(Interactor interactor)
    {
        if (openChestChannel == null) return;

        EnsureInventoryInitialized();
        openChestChannel.Raise(new OpenChestRequest(interactor, runtimeInventory));
    }

    public override bool CanInteract(Interactor interactor)
    {
        return base.CanInteract(interactor) && openChestChannel != null;
    }

    public void SetLootTable(LootTableSO newLootTable)
    {
        lootTable = newLootTable;
        chestInventory = null;
        runtimeInventory = null;
        initialized = false;
    }

    private void EnsureInventoryInitialized()
    {
        if (initialized) return;

        if (chestInventory != null)
            runtimeInventory = Instantiate(chestInventory);
        else if (lootTable != null)
            runtimeInventory = lootTable.CreateInventoryInstance();
        else
            runtimeInventory = ScriptableObject.CreateInstance<InventorySO>();

        if (lootTable != null)
            lootTable.Fill(runtimeInventory);

        initialized = true;
    }
}
