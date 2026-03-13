using UnityEngine;

public class PlayMusicOnStart : MonoBehaviour
{
    [SerializeField] private AudioManager audioManager;
    [SerializeField] private MusicTrack music;

    private void Start()
    {
        audioManager.PlayMusic(music);
    }
}