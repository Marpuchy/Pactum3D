using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(menuName = "Events/Room Cleared")]
public class RoomClearedEvent : ScriptableObject
{
    private UnityEvent _event = new();
    private readonly List<UnityAction<AudioClip>> listeners = new();
    

    public void Raise() => _event.Invoke();
    
    public void RaiseClip(AudioClip clip)
    {
        if (clip == null) return;

        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            listeners[i].Invoke(clip);
        }
    }
    public void Register(UnityAction action) => _event.AddListener(action);
    public void Unregister(UnityAction action) => _event.RemoveListener(action);
    
    public void RegisterListener(UnityAction<AudioClip> listener)
    {
        if (!listeners.Contains(listener))
            listeners.Add(listener);
    }

    public void UnregisterListener(UnityAction<AudioClip> listener)
    {
        if (listeners.Contains(listener))
            listeners.Remove(listener);
    }
}