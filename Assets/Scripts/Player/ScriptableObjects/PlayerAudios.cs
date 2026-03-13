using UnityEngine;

[CreateAssetMenu(fileName = "PlayerAudios", menuName = "Character/PlayerAudios")]
public class PlayerAudios : ScriptableObject
{
    [SerializeField] private AudioClip stepsAudio;

    [Range(0f, 1f)]
    [SerializeField] private float stepsVolume;

    [SerializeField] private AudioClip attackAudio;

    public AudioClip StepsAudio => stepsAudio;
    public float StepsVolume => stepsVolume;

    public AudioClip AttackAudio => attackAudio;
}
