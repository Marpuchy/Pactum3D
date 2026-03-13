using UnityEngine;

[DisallowMultipleComponent]
public sealed class DamageFlashFeedback : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private MonoBehaviour damageableSource;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer[] spriteRenderers;
    [SerializeField] private Color flashColor = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField] private float flashDuration = 0.08f;
    [SerializeField] private bool useUnscaledTime = true;

    private IDamageable damageable;
    private Color[] baseColors;
    private float flashTimer;
    private bool isFlashing;

    private void Awake()
    {
        ResolveDamageable();
        ResolveSpriteRenderers();
    }

    private void OnEnable()
    {
        ResolveDamageable();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        StopFlash(restoreColors: true);
    }

    private void Update()
    {
        if (!isFlashing)
            return;

        float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        flashTimer -= delta;
        if (flashTimer <= 0f)
            StopFlash(restoreColors: true);
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

    private void ResolveSpriteRenderers()
    {
        if (spriteRenderers != null && spriteRenderers.Length > 0)
            return;

        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
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

        TriggerFlash();
    }

    private void TriggerFlash()
    {
        ResolveSpriteRenderers();

        if (spriteRenderers == null || spriteRenderers.Length == 0)
            return;

        if (!isFlashing)
            CacheBaseColors();

        ApplyFlashColor();
        flashTimer = Mathf.Max(0.01f, flashDuration);
        isFlashing = true;
    }

    private void CacheBaseColors()
    {
        if (baseColors == null || baseColors.Length != spriteRenderers.Length)
            baseColors = new Color[spriteRenderers.Length];

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer renderer = spriteRenderers[i];
            baseColors[i] = renderer != null ? renderer.color : Color.white;
        }
    }

    private void ApplyFlashColor()
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer renderer = spriteRenderers[i];
            if (renderer == null)
                continue;

            renderer.color = flashColor;
        }
    }

    private void StopFlash(bool restoreColors)
    {
        if (restoreColors)
            RestoreBaseColors();

        flashTimer = 0f;
        isFlashing = false;
    }

    private void RestoreBaseColors()
    {
        if (spriteRenderers == null || baseColors == null)
            return;

        int count = Mathf.Min(spriteRenderers.Length, baseColors.Length);
        for (int i = 0; i < count; i++)
        {
            SpriteRenderer renderer = spriteRenderers[i];
            if (renderer == null)
                continue;

            renderer.color = baseColors[i];
        }
    }
}
