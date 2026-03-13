using UnityEngine;

public class PlayDoorOpenSound : MonoBehaviour
{
    [SerializeField] private AudioManager audioManager;
    [SerializeField] private RoomClearedEvent roomClearedEvent;
    
    private void OnEnable()
    {
        if (roomClearedEvent != null)
            roomClearedEvent.RegisterListener(OnDoorOpened);
        
    }

    private void OnDisable()
    {
        if (roomClearedEvent != null)
            roomClearedEvent.UnregisterListener(OnDoorOpened);
        
    }

    private void OnDoorOpened(AudioClip clip)
    {
        if (audioManager != null && clip != null)
            audioManager.PlayDoorOpen(clip);
    }

}
