/// <summary>
/// Strategy interface that allows interactables to delegate execution logic.
/// </summary>
public interface IInteractionAction
{
    void Execute(Interactor interactor);
}
