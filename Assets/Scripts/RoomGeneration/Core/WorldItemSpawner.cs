using UnityEngine;
using Zenject;

public class WorldItemSpawner
{
    private readonly GameObject _defaultPrefab;
    private readonly IInstantiator _instantiator;

    public WorldItemSpawner(GameObject defaultPrefab, IInstantiator instantiator = null)
    {
        _defaultPrefab = defaultPrefab;
        _instantiator = instantiator;
    }

    public GameObject SpawnItem(ItemDataSO itemData, int amount, Vector3 worldPos, Transform parent)
    {
        if (itemData == null || _defaultPrefab == null)
            return null;

        GameObject go = _instantiator != null
            ? _instantiator.InstantiatePrefab(_defaultPrefab, worldPos, Quaternion.identity, parent)
            : Object.Instantiate(_defaultPrefab, worldPos, Quaternion.identity, parent);

        WorldItem worldItem = go.GetComponent<WorldItem>();
        if (worldItem != null)
            worldItem.Initialize(itemData);

        PickupInteractable pickup = go.GetComponent<PickupInteractable>();
        if (pickup != null)
        {
            pickup.SetItemData(itemData);
            pickup.SetAmount(amount);
        }

        AutoPickupCoin autoPickup = go.GetComponent<AutoPickupCoin>();
        if (autoPickup != null)
            autoPickup.Configure(itemData, amount);

        Room2_5DPresentationUtility.EnsureDepthSorting(go, Room2_5DRenderPreset.Item);

        return go;
    }
}
