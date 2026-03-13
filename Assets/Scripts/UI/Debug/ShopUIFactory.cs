using UnityEngine;

public sealed class ShopUIFactory : MonoBehaviour
{
    [SerializeField] private OpenShopEventChannelSO openShopChannel;
    [SerializeField] private ShopGridCanvas shopPrefab;
    [SerializeField] private Transform parent;
    [SerializeField] private bool closeOnEscape = true;
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    private ShopGridCanvas instance;

    private void OnEnable()
    {
        if (openShopChannel != null)
            openShopChannel.OnRaised += OnOpenShop;
    }

    private void OnDisable()
    {
        if (openShopChannel != null)
            openShopChannel.OnRaised -= OnOpenShop;
    }

    private void OnOpenShop(OpenShopRequest request)
    {
        if (shopPrefab == null)
        {
            Debug.LogError($"{nameof(ShopUIFactory)}: shopPrefab is not assigned.", this);
            return;
        }

        if (instance == null)
        {
            instance = parent != null
                ? Instantiate(shopPrefab, parent)
                : Instantiate(shopPrefab);
        }

        instance.Bind(request.Interactor, request.Catalog);
        var keyToUse = closeKey;
        if (TryGetInteractionKey(request.Interactor, out var interactionKey))
            keyToUse = interactionKey;

        instance.SetCloseKey(keyToUse);
        instance.SetCloseOnKey(closeOnEscape);
        instance.Show();
    }

    private static bool TryGetInteractionKey(Interactor interactor, out KeyCode key)
    {
        key = default;
        if (interactor == null)
            return false;

        var input = interactor.GetComponent<PlayerInteractionInput>();
        if (input == null)
            return false;

        key = input.InteractionKey;
        return true;
    }
}
