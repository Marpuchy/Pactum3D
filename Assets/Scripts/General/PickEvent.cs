using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "PickEvent", menuName = "Game/PickEvent")]
public class PickAudioEvent : ScriptableObject
{
    private readonly List<UnityAction<AudioClip>> listeners = new();

    public void Raise(AudioClip clip)
    {
        if (clip == null) return;

        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            listeners[i].Invoke(clip);
        }
    }
    
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
