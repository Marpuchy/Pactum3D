using System;
using UnityEngine;

[Serializable]
public readonly struct OpenShopRequest
{
    public Interactor Interactor { get; }
    public string ShopId { get; }
    public ShopCatalogSO Catalog { get; }

    public OpenShopRequest(Interactor interactor, string shopId, ShopCatalogSO catalog = null)
    {
        Interactor = interactor;
        ShopId = shopId;
        Catalog = catalog;
    }
}

[CreateAssetMenu(menuName = "Events/OpenShop", fileName = "OpenShopEventChannel")]
public sealed class OpenShopEventChannelSO : ScriptableObject
{
    public event Action<OpenShopRequest> OnRaised;

    public void Raise(OpenShopRequest request)
    {
        OnRaised?.Invoke(request);
    }
}
