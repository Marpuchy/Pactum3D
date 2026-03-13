using System;
using UnityEngine;

[Serializable]
public readonly struct OpenChestRequest
{
    public Interactor Interactor { get; }
    public InventorySO ChestInventory { get; }

    public OpenChestRequest(Interactor interactor, InventorySO chestInventory)
    {
        Interactor = interactor;
        ChestInventory = chestInventory;
    }
}

[CreateAssetMenu(menuName = "Events/OpenChest", fileName = "OpenChestEventChannel")]
public sealed class OpenChestEventChannelSO : ScriptableObject
{
    public event Action<OpenChestRequest> OnRaised;

    public void Raise(OpenChestRequest request)
    {
        OnRaised?.Invoke(request);
    }
}
