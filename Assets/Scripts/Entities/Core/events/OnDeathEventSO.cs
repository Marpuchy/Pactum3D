using UnityEngine;
using System;

[CreateAssetMenu(
    fileName = "OnDeathEvent",
    menuName = "Events/On Death Event"
)]
public class OnDeathEventSO : ScriptableObject
{
    private Action listeners;

    public void RegisterListener(Action listener)
    {
        listeners += listener;
    }

    public void UnregisterListener(Action listener)
    {
        listeners -= listener;
    }

    public void Raise()
    {
        listeners?.Invoke();
    }
}