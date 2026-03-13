using System;
using UnityEngine;

public class PlayConsumeSound : MonoBehaviour
{
    [SerializeField] private AudioManager audioManager;
    [SerializeField] private ConsumeAudioEvent consumeEvent;

    private void OnEnable()
    {
        if (consumeEvent != null)
            consumeEvent.RegisterListener(OnConsume);
    }

    public void PlaySound()
    {
        if (consumeEvent != null)
            consumeEvent.UnregisterListener(OnConsume);
    }
    
    private void OnConsume(AudioClip clip)
    {
        if (audioManager != null && clip != null)
            audioManager.PlayConsume(clip);
    }
}
