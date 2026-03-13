using System;
using UnityEngine;

/// <summary>
/// ScriptableObject based event channel to decouple UI prompt handling from the player.
/// </summary>
[CreateAssetMenu(menuName = "Events/Interaction Focus Channel", fileName = "InteractionFocusChannel")]
public sealed class InteractionFocusEventChannelSO : ScriptableObject
{
    public event Action<IInteractable> OnFocusChanged;

    public void Raise(IInteractable interactable)
    {
        OnFocusChanged?.Invoke(interactable);
    }
}
