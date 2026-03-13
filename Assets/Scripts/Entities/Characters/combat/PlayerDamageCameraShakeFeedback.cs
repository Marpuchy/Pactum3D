using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerDamageCameraShakeFeedback : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private MonoBehaviour damageableSource;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool useMainCameraFallback = true;

    [Header("Shake")]
    [SerializeField] private float shakeAmplitude = 0.08f;
    [SerializeField] private float shakeDuration = 0.09f;
    [SerializeField] private float shakeFrequency = 45f;

    private IDamageable damageable;
    private CameraShake2D cameraShake;

    private void Awake()
    {
        ResolveDamageable();
        ResolveCameraShake();
    }

    private void OnEnable()
    {
        ResolveDamageable();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void ResolveDamageable()
    {
        damageable = null;

        if (damageableSource != null)
            damageable = damageableSource as IDamageable;

        if (damageable == null)
            damageable = GetComponent<IDamageable>();

        if (damageable == null)
            damageable = GetComponentInParent<IDamageable>();
    }

    private void ResolveCameraShake()
    {
        if (targetCamera == null && useMainCameraFallback)
            targetCamera = Camera.main;

        if (targetCamera == null)
        {
            cameraShake = null;
            return;
        }

        if (!targetCamera.TryGetComponent(out cameraShake))
            cameraShake = targetCamera.gameObject.AddComponent<CameraShake2D>();
    }

    private void Subscribe()
    {
        if (damageable == null)
            return;

        damageable.DamageReceived -= OnDamageReceived;
        damageable.DamageReceived += OnDamageReceived;
    }

    private void Unsubscribe()
    {
        if (damageable == null)
            return;

        damageable.DamageReceived -= OnDamageReceived;
    }

    private void OnDamageReceived(DamageReceivedInfo info)
    {
        if (info.AppliedDamage <= 0f)
            return;

        if (cameraShake == null)
            ResolveCameraShake();

        cameraShake?.Shake(shakeAmplitude, shakeDuration, shakeFrequency);
    }
}
