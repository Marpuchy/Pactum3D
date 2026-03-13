using UnityEngine;

/// <summary>
/// Base interactable that exposes the prompt and interaction point along with an optional
/// strategy-based action. Override <see cref="OnInteract"/> for bespoke behaviours or
/// assign a component that implements <see cref="IInteractionAction"/> to reuse logic.
/// </summary>
public abstract class InteractableBase : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform interactionPoint;
    [SerializeField] private string prompt = "Interact";
    [SerializeField] private InteractionMode mode = InteractionMode.Manual;
    [SerializeField] private MonoBehaviour interactionActionBehaviour;

    private IInteractionAction cachedAction;

    public Transform InteractionPoint => interactionPoint != null ? interactionPoint : transform;
    public virtual string Prompt => prompt;
    public virtual InteractionMode Mode => mode;

    public virtual bool CanInteract(Interactor interactor) => true;

    public void Interact(Interactor interactor)
    {
        var action = GetAction();
        if (action != null)
        {
            action.Execute(interactor);
            return;
        }

        OnInteract(interactor);
    }

    /// <summary>
    /// Override this method for ad-hoc behaviours when no action strategy is assigned.
    /// </summary>
    protected abstract void OnInteract(Interactor interactor);

    /// <summary>
    /// Allows injection of an action strategy at runtime (e.g. from a service locator).
    /// </summary>
    public void SetAction(IInteractionAction action)
    {
        cachedAction = action;
    }

    protected void SetMode(InteractionMode newMode)
    {
        mode = newMode;
    }

    private IInteractionAction GetAction()
    {
        if (cachedAction != null)
            return cachedAction;

        if (interactionActionBehaviour == null)
            return null;

        if (interactionActionBehaviour is not IInteractionAction action)
        {
            Debug.LogError($"{interactionActionBehaviour.name} must implement {nameof(IInteractionAction)} to be used as an interaction action.", this);
            return null;
        }

        cachedAction = action;
        return cachedAction;
    }
}
