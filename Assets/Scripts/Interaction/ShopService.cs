using UnityEngine;

/// <summary>
/// Example service implementation that can be referenced by interactables via a ScriptableObject.
/// </summary>
public sealed class ShopService : MonoBehaviour, IShopService
{
    [SerializeField] private ShopServiceReference reference;

    private void Awake()
    {
        if (reference != null)
            reference.Set(this);
    }

    public void OpenShop(string shopId)
    {
        Debug.Log($"Opening shop: {shopId}", this);
        // TODO: Instantiate UI, load inventories, etc.
    }
}
