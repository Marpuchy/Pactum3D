using UnityEngine;
using System;

[CreateAssetMenu(menuName = "Architecture/Game Event")]
public class GameEventSO : ScriptableObject
{
    private event Action OnEventRaised;

    public void Raise()
    {
        OnEventRaised?.Invoke();
    }

    public void RegisterListener(Action listener)
    {
        OnEventRaised += listener;
    }

    public void UnregisterListener(Action listener)
    {
        OnEventRaised -= listener;
    }
}