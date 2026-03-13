using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "PauseEventSO", menuName = "Game/PauseEventSO")]
public class PauseEventSO : ScriptableObject
{
    private readonly List<UnityAction> listeners = new();

    public void Raise()
    {
        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            listeners[i].Invoke();
        }
    }
    
    public void RegisterListener(UnityAction listener)
    {
        if (!listeners.Contains(listener))
            listeners.Add(listener);
    }

    public void UnregisterListener(UnityAction listener)
    {
        if (listeners.Contains(listener))
            listeners.Remove(listener);
    }
}
