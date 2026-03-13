using UnityEngine;

public sealed class ChestUIFactory : MonoBehaviour
{
    [SerializeField] private OpenChestEventChannelSO openChestChannel;
    [SerializeField] private ChestGridCanvas chestPrefab;
    [SerializeField] private Transform parent;
    [SerializeField] private bool closeOnEscape = true;
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    private ChestGridCanvas instance;

    private void OnEnable()
    {
        if (openChestChannel != null)
            openChestChannel.OnRaised += OnOpenChest;
    }

    private void OnDisable()
    {
        if (openChestChannel != null)
            openChestChannel.OnRaised -= OnOpenChest;
    }

    private void OnOpenChest(OpenChestRequest request)
    {
        if (chestPrefab == null)
        {
            Debug.LogError($"{nameof(ChestUIFactory)}: chestPrefab is not assigned.", this);
            return;
        }

        if (instance == null)
        {
            instance = parent != null
                ? Instantiate(chestPrefab, parent)
                : Instantiate(chestPrefab);
        }

        instance.Bind(request.Interactor, request.ChestInventory);
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
