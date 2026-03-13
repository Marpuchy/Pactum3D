using UnityEngine;
using System;

[CreateAssetMenu(menuName = "Architecture/Health Changed Event")]
public class HealthChangedEventSO : ScriptableObject
{
    private event Action<float, float> OnEventRaised;

    public void Raise(float current, float max)
    {
        OnEventRaised?.Invoke(current, max);
    }

    public void RegisterListener(Action<float, float> listener)
    {
        OnEventRaised += listener;
    }

    public void UnregisterListener(Action<float, float> listener)
    {
        OnEventRaised -= listener;
    }
}