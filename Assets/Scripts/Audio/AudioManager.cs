using UnityEngine;

public sealed class AudioManager : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private MusicController musicController;

    public void Play(AudioEventSO audioEvent)
    {
        audioEvent.Play(audioSource);
    }

    public void PlayPick(AudioClip clip)
    {
        audioSource.PlayOneShot(clip);
    }
    
    public void PlayConsume(AudioClip clip)
    {
        audioSource.PlayOneShot(clip);
    }
    
    public void PlaySteps(AudioClip clip)
    {
        if (clip == null)
        {
            audioSource.Stop();
            return;
        }
        
        if (audioSource.clip == clip && audioSource.isPlaying)
            return;
        
        audioSource.clip = clip;
        audioSource.loop = true;
        audioSource.Play();
    }
    public void PlayAttack(AudioClip clip)
                 {
                     audioSource.PlayOneShot(clip);
                 }
    public void PlayDoorOpen(AudioClip clip)
        {
            audioSource.PlayOneShot(clip);
        }
    public void PlayBreakRock(AudioClip clip)
            {
                audioSource.PlayOneShot(clip);
            }
    
    
    
    public void StopSteps()
    {
        if (!audioSource.isPlaying)
            return;

        audioSource.loop = false;
        audioSource.Stop();
        audioSource.clip = null;
    }
    
    public void PlayMusic(MusicTrack musicTrack)
    {
        musicController.Play(musicTrack);
    }
    
    public void StopMusic()
    {
        musicController.Stop();
    }
}