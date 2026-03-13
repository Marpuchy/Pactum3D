using UnityEngine;

public class WorldItem : MonoBehaviour, IPickable
{
    [SerializeField] private ItemDataSO itemData;
    public ItemDataSO ItemData => itemData;
    
    public void Initialize(ItemDataSO data)
    {
        itemData = data;
        UpdateVisual();
    }
    
    private void UpdateVisual()
    {
        var spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && itemData != null)
        {
            spriteRenderer.sprite = itemData.Icon;
        }
    }
    
    public void OnPick(GameObject picker)
    {
        if (picker == null || itemData == null)
            return;

        Inventory inventory = null;
        var interactor = picker.GetComponent<Interactor>();
        if (interactor != null)
            inventory = interactor.Inventory;

        if (inventory == null)
            inventory = picker.GetComponent<PlayerInventoryHolder>()?.Inventory;

        if (inventory == null)
            return;

        IItem runtimeItem = ItemFactory.CreateItem(itemData);
        if (runtimeItem == null)
            return;

        if (interactor != null)
        {
            if (interactor.AddItem(runtimeItem))
                Destroy(gameObject);
            return;
        }

        if (inventory.AddItem(runtimeItem))
            Destroy(gameObject);
        else
            MiniInventoryGridCanvas.TryShowInventoryFull();
    }
}
