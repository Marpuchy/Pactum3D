using UnityEngine;

public sealed class MerchantNpc : InteractableBase
{
    [SerializeField] private string shopId = "default_shop";
    [SerializeField] private ShopCatalogSO catalog;
    [SerializeField] private OpenShopEventChannelSO openShopChannel;
    [SerializeField] private OpenDialogueEventChannelSO openDialogueChannel;
    [SerializeField] private DialogueDefinition dialogue;

    protected override void OnInteract(Interactor interactor)
    {
        if (dialogue != null && openDialogueChannel != null)
            openDialogueChannel.Raise(new OpenDialogueRequest(interactor, dialogue));

        if (openShopChannel != null)
            openShopChannel.Raise(new OpenShopRequest(interactor, shopId, catalog));
    }

    public override bool CanInteract(Interactor interactor)
    {
        return base.CanInteract(interactor) && openShopChannel != null && !string.IsNullOrWhiteSpace(shopId);
    }
}
