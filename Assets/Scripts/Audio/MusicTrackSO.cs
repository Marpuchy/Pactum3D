using UnityEngine;

[CreateAssetMenu(menuName = "AudioSO/Music Track")]
public class MusicTrack : ScriptableObject
{
    [SerializeField] private AudioClip clip;
    [Range(0f, 1f)] [SerializeField] private float volume = 1f;

    public AudioClip Clip => clip;
    public float Volume => volume;
}