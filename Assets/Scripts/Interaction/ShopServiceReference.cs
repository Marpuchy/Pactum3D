using UnityEngine;

/// <summary>
/// Simple ScriptableObject reference that enables drag-and-drop dependency injection.
/// </summary>
[CreateAssetMenu(menuName = "Services/Shop Service Reference", fileName = "ShopServiceReference")]
public sealed class ShopServiceReference : ScriptableObject
{
    public IShopService Value { get; private set; }

    public void Set(IShopService service) => Value = service;
}
