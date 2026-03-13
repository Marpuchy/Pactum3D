using UnityEngine;

public class PlayRockBreakingSound : MonoBehaviour
{
    [SerializeField] private AudioManager audioManager;
    [SerializeField] private RockHitEventSO rockHitEventSo;
    
    private void OnEnable()
    {
        if (rockHitEventSo != null)
            rockHitEventSo.RegisterListener(OnBreak);
        
    }

    private void OnDisable()
    {
        if (rockHitEventSo != null)
            rockHitEventSo.UnregisterListener(OnBreak);
        
    }

    private void OnBreak(AudioClip clip)
    {
        if (audioManager != null && clip != null)
            audioManager.PlayBreakRock(clip);
    }
}
