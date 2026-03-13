using System;
using UnityEngine;

public class PlayStepsSound : MonoBehaviour
{
    [SerializeField] private AudioManager audioManager;
    [SerializeField] private PlayerAudioMovingEvent playerAudioMovingEvent;
    [SerializeField] private PlayerAudioStopEvent playerAudioStopEvent;
    
    private void OnEnable()
    {
        if (playerAudioMovingEvent != null)
            playerAudioMovingEvent.RegisterListener(OnWalk);
        
        if (playerAudioStopEvent != null)
            playerAudioStopEvent.RegisterListener(OnStop);
    }

    private void OnDisable()
    {
        if (playerAudioMovingEvent != null)
            playerAudioMovingEvent.UnregisterListener(OnWalk);
        
        if (playerAudioStopEvent != null)
            playerAudioStopEvent.UnregisterListener(OnStop);
    }

    private void OnWalk(AudioClip clip)
    {
        if (audioManager != null && clip != null)
            audioManager.PlaySteps(clip);
    }
    
    private void OnStop()
    {
        if (audioManager != null)
            audioManager.StopSteps();
    }
}
