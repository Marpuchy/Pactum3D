using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class CameraShake2D : MonoBehaviour
{
    [Header("Defaults")]
    [SerializeField] private float defaultAmplitude = 0.08f;
    [SerializeField] private float defaultDuration = 0.09f;
    [SerializeField] private float defaultFrequency = 45f;
    [SerializeField] private bool useUnscaledTime = true;

    private float activeAmplitude;
    private float activeDuration;
    private float activeFrequency;
    private float timeLeft;
    private float noiseSeedX;
    private float noiseSeedY;

    public Vector3 CurrentOffset { get; private set; }

    private void Awake()
    {
        noiseSeedX = Random.value * 100f;
        noiseSeedY = Random.value * 100f;
    }

    private void Update()
    {
        float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        if (timeLeft <= 0f)
        {
            CurrentOffset = Vector3.zero;
            return;
        }

        timeLeft -= delta;
        if (timeLeft <= 0f)
        {
            CurrentOffset = Vector3.zero;
            timeLeft = 0f;
            return;
        }

        float normalized = activeDuration > 0f ? timeLeft / activeDuration : 0f;
        float amplitude = activeAmplitude * Mathf.Clamp01(normalized);
        float t = (useUnscaledTime ? Time.unscaledTime : Time.time) * activeFrequency;

        float x = Mathf.PerlinNoise(noiseSeedX, t) * 2f - 1f;
        float y = Mathf.PerlinNoise(noiseSeedY, t) * 2f - 1f;

        CurrentOffset = new Vector3(x, y, 0f) * amplitude;
    }

    public void Shake()
    {
        Shake(defaultAmplitude, defaultDuration, defaultFrequency);
    }

    public void Shake(float amplitude, float duration, float frequency)
    {
        if (amplitude <= 0f || duration <= 0f)
            return;

        activeAmplitude = Mathf.Max(activeAmplitude, amplitude);
        activeDuration = Mathf.Max(activeDuration, duration);
        activeFrequency = frequency > 0f ? frequency : defaultFrequency;
        timeLeft = Mathf.Max(timeLeft, duration);
    }
}
