using UnityEngine;

public class PlaySoundButton : MonoBehaviour
{
    [SerializeField] private AudioManager audioManager;
    [SerializeField] private AudioEventSO audioEventSelect;
    [SerializeField] private AudioEventSO audioEventHover;

    public void PlaySound()
    {
        audioManager.Play(audioEventSelect);
    }
    
    public void PlayHoverSound()
    {
        audioManager.Play(audioEventHover);
    }
}