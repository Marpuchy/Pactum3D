using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(menuName = "Events/Door Entered")]
public class DoorEnteredEvent : ScriptableObject
{
    private readonly UnityEvent _event = new();

    public void Raise() => _event.Invoke();

    public void Register(UnityAction action)
        => _event.AddListener(action);

    public void Unregister(UnityAction action)
        => _event.RemoveListener(action);
}