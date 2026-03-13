using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(
    fileName = "EnemyDeathEvent",
    menuName = "Events/Enemy Death Event"
)]
public class EnemyDeathEvent : ScriptableObject
{
    private readonly UnityEvent<GameObject> _event = new();

    public void Raise(GameObject enemy)
    {
        _event.Invoke(enemy);
    }

    public void RegisterListener(UnityAction<GameObject> listener)
    {
        _event.AddListener(listener);
    }

    public void UnregisterListener(UnityAction<GameObject> listener)
    {
        _event.RemoveListener(listener);
    }
}