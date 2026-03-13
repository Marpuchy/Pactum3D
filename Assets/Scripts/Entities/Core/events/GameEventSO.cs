using UnityEngine;
using System.Collections.Generic;

public abstract class GameEvent<T> : ScriptableObject
{
    private readonly List<GameEventListener<T>> listeners = new();

    public void Raise(T value)
    {
        for (int i = listeners.Count - 1; i >= 0; i--)
            listeners[i].OnEventRaised(value);
    }

    public void RegisterListener(GameEventListener<T> listener)
    {
        if (!listeners.Contains(listener))
            listeners.Add(listener);
    }

    public void UnregisterListener(GameEventListener<T> listener)
    {
        listeners.Remove(listener);
    }
}