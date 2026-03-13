using UnityEngine;

[CreateAssetMenu(fileName = "AudioEvent", menuName = "AudioSO/AudioEvent")]
public class AudioEventSO : ScriptableObject
{
    [SerializeField] private AudioClip clip;
    [Range(0f, 1f)] [SerializeField] private float volume = 1f;

    public void Play(AudioSource source)
    {
        Debug.Log($"Clip: {clip}");
        source.clip = clip;
        source.volume = volume;
        source.Play();
    }
}
