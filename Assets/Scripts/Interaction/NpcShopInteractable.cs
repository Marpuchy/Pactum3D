using UnityEngine;

/// <summary>
/// NPC interactable that delegates to a shop service when the player interacts.
/// </summary>
public sealed class NpcShopInteractable : InteractableBase
{
    [SerializeField] private string shopId = "default_shop";
    [SerializeField] private ShopCatalogSO catalog;
    [SerializeField] private OpenShopEventChannelSO openShopChannel;
    [SerializeField] private ShopServiceReference shopService;
    [SerializeField] private OpenDialogueEventChannelSO openDialogueChannel;
    [SerializeField] private DialogueDefinition dialogue;

    protected override void OnInteract(Interactor interactor)
    {
        if (dialogue != null && openDialogueChannel != null)
            openDialogueChannel.Raise(new OpenDialogueRequest(interactor, dialogue));

        if (openShopChannel != null)
        {
            openShopChannel.Raise(new OpenShopRequest(interactor, shopId, catalog));
            return;
        }

        if (shopService == null || shopService.Value == null)
        {
            Debug.LogWarning($"Shop service reference not configured for {name}.", this);
            return;
        }

        shopService.Value.OpenShop(shopId);
    }

    public override bool CanInteract(Interactor interactor)
    {
        if (!base.CanInteract(interactor)) return false;
        return openShopChannel != null || (shopService != null && shopService.Value != null);
    }
}
