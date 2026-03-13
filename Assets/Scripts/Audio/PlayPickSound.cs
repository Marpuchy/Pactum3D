using System;
using UnityEngine;

public class PlayPickSound : MonoBehaviour
{
    [SerializeField] private AudioManager audioManager;
    [SerializeField] private PickAudioEvent pickEvent;

    private void OnEnable()
    {
        if (pickEvent != null)
            pickEvent.RegisterListener(OnPick);
    }

    public void PlaySound()
    {
        if (pickEvent != null)
            pickEvent.UnregisterListener(OnPick);
    }
    
    private void OnPick(AudioClip clip)
    {
        if (audioManager != null && clip != null)
            audioManager.PlayPick(clip);
    }
}
