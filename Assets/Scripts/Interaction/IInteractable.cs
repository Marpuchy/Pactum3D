using UnityEngine;

/// <summary>
/// Contract that every interactable must satisfy. Keeps UI, detection,
/// and gameplay code decoupled from concrete implementations.
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// Point that should be used for distance checks and gizmos.
    /// </summary>
    Transform InteractionPoint { get; }

    /// <summary>
    /// Text prompt that is shown on the interaction UI.
    /// </summary>
    string Prompt { get; }

    /// <summary>
    /// Defines whether the interaction is manual or automatic.
    /// </summary>
    InteractionMode Mode { get; }

    /// <summary>
    /// Returns true if the interactable can process an interaction at this moment.
    /// </summary>
    bool CanInteract(Interactor interactor);

    /// <summary>
    /// Executes the interaction logic for the provided interactor.
    /// </summary>
    void Interact(Interactor interactor);
}
