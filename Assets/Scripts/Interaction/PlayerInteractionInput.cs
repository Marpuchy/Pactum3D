using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Thin adapter that converts input (legacy, PlayerInput events, etc.) into
/// TryInteract calls on the Interactor.
/// </summary>
public sealed class PlayerInteractionInput : MonoBehaviour
{
    [SerializeField] private Interactor interactor;
    [SerializeField] private bool pollKey = true;
    [SerializeField] private KeyCode key = KeyCode.E;

    private void Awake()
    {
        if (interactor == null)
            interactor = GetComponent<Interactor>();
    }

    private void Update()
    {
        if (!pollKey) return;
        if (GameplayUIState.IsGameplayInputBlocked) return;
        if (Input.GetKeyDown(key))
            interactor?.TryInteract();
    }

    public void OnInteract()
    {
        if (GameplayUIState.IsGameplayInputBlocked) return;
        interactor?.TryInteract();
    }

    public void OnInteract(InputValue value)
    {
        if (GameplayUIState.IsGameplayInputBlocked) return;
        if (value.isPressed)
            interactor?.TryInteract();
    }

    public KeyCode InteractionKey => key;
}
