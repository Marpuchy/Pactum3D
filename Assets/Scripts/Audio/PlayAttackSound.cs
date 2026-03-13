using System;
using UnityEngine;

public class PlayAttackSound : MonoBehaviour
{
    [SerializeField] private AudioManager audioManager;
    [SerializeField] private PlayerAudioAttackingEvent playerAudioAttackingEvent;
    
    private void OnEnable()
    {
        if (playerAudioAttackingEvent != null)
            playerAudioAttackingEvent.RegisterListener(OnAttack);
        
    }

    private void OnDisable()
    {
        if (playerAudioAttackingEvent != null)
            playerAudioAttackingEvent.UnregisterListener(OnAttack);
        
    }

    private void OnAttack(AudioClip clip)
    {
        if (audioManager != null && clip != null)
            audioManager.PlayAttack(clip);
    }
    
}
