using UnityEngine;

public sealed class PactUIFactory : MonoBehaviour
{
    [SerializeField] private OfferPactEventChannelSO offerPactChannel;
    [SerializeField] private PactCanvasDebug pactPrefab;
    [SerializeField] private Transform parent;
    [SerializeField] private bool closeOnKey = true;
    [SerializeField] private KeyCode closeKey = KeyCode.E;

    private PactCanvasDebug instance;

    private void OnEnable()
    {
        if (offerPactChannel != null)
            offerPactChannel.OnRaised += OnOfferPact;
    }

    private void OnDisable()
    {
        if (offerPactChannel != null)
            offerPactChannel.OnRaised -= OnOfferPact;
    }

    private void OnOfferPact(OfferPactRequest request)
    {
        if (pactPrefab == null)
        {
            Debug.LogError($"{nameof(PactUIFactory)}: pactPrefab is not assigned.", this);
            return;
        }

        if (instance == null)
        {
            instance = parent != null
                ? Instantiate(pactPrefab, parent)
                : Instantiate(pactPrefab);
        }

        if (instance is IInteractorBoundUI bound)
            bound.Bind(request.Interactor);

        var keyToUse = closeKey;
        if (TryGetInteractionKey(request.Interactor, out var interactionKey))
            keyToUse = interactionKey;

        instance.SetManualCloseAllowed(false);
        instance.SetCloseKey(keyToUse);
        instance.SetCloseOnKey(closeOnKey);
        instance.Show(request);
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
