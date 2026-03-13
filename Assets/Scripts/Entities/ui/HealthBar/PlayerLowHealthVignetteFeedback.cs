using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PlayerLowHealthVignetteFeedback : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private HealthComponent healthComponent;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private bool findCanvasAutomatically = true;

    [Header("Vignette")]
    [SerializeField] private Color vignetteColor = new Color(0.85f, 0f, 0f, 1f);
    [SerializeField, Range(0f, 1f)] private float startEffectHealthPercent = 0.5f;
    [SerializeField, Range(0f, 1f)] private float fullEffectHealthPercent = 0.12f;
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.35f;
    [SerializeField] private float alphaSmoothing = 8f;
    [SerializeField] private bool drawOverUi = false;

    [Header("Pulse")]
    [SerializeField] private bool pulseWhenCritical = true;
    [SerializeField, Range(0f, 1f)] private float criticalHealthPercent = 0.22f;
    [SerializeField, Range(0f, 1f)] private float pulseAlphaAmplitude = 0.03f;
    [SerializeField] private float pulseFrequency = 2f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Texture")]
    [SerializeField, Range(64, 1024)] private int textureSize = 512;
    [SerializeField, Range(0f, 1f)] private float innerClearRadius = 0.65f;
    [SerializeField, Range(0.5f, 6f)] private float edgeExponent = 2.5f;

    private Image vignetteImage;
    private Texture2D runtimeTexture;
    private Sprite runtimeSprite;
    private float currentAlpha;

    private void Awake()
    {
        if (healthComponent == null)
            healthComponent = GetComponent<HealthComponent>();

        TryEnsureOverlay();
    }

    private void OnEnable()
    {
        TryEnsureOverlay();
    }

    private void OnDisable()
    {
        ApplyAlpha(0f);
    }

    private void OnDestroy()
    {
        if (runtimeSprite != null)
            Destroy(runtimeSprite);

        if (runtimeTexture != null)
            Destroy(runtimeTexture);
    }

    private void Update()
    {
        TryEnsureOverlay();
        if (vignetteImage == null)
            return;

        float targetAlpha = ComputeTargetAlpha();
        float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float lerpFactor = 1f - Mathf.Exp(-Mathf.Max(0.01f, alphaSmoothing) * delta);
        currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, lerpFactor);
        ApplyAlpha(currentAlpha);
    }

    private float ComputeTargetAlpha()
    {
        if (healthComponent == null || healthComponent.MaxHealth <= 0f)
            return 0f;

        float healthPercent = Mathf.Clamp01(healthComponent.CurrentHealth / healthComponent.MaxHealth);
        float effect = Mathf.InverseLerp(startEffectHealthPercent, fullEffectHealthPercent, healthPercent);
        effect = Mathf.Clamp01(effect);

        float alpha = effect * maxAlpha;

        if (pulseWhenCritical && healthPercent <= criticalHealthPercent && pulseAlphaAmplitude > 0f)
        {
            float criticalWeight = Mathf.InverseLerp(criticalHealthPercent, 0f, healthPercent);
            float time = useUnscaledTime ? Time.unscaledTime : Time.time;
            float pulse01 = (Mathf.Sin(time * Mathf.PI * 2f * pulseFrequency) + 1f) * 0.5f;
            alpha += pulse01 * pulseAlphaAmplitude * criticalWeight;
        }

        return Mathf.Clamp01(alpha);
    }

    private void ApplyAlpha(float alpha)
    {
        if (vignetteImage == null)
            return;

        Color color = vignetteColor;
        color.a = alpha;
        vignetteImage.color = color;
    }

    private void TryEnsureOverlay()
    {
        if (vignetteImage != null)
            return;

        Canvas canvas = ResolveCanvas();
        if (canvas == null)
            return;

        GameObject overlayObject = new GameObject(
            "PlayerLowHealthVignette",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        overlayObject.transform.SetParent(canvas.transform, false);

        RectTransform rect = overlayObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;

        vignetteImage = overlayObject.GetComponent<Image>();
        vignetteImage.raycastTarget = false;
        vignetteImage.sprite = GetOrCreateSprite();
        vignetteImage.type = Image.Type.Simple;
        vignetteImage.preserveAspect = false;

        if (drawOverUi)
            overlayObject.transform.SetAsLastSibling();
        else
            overlayObject.transform.SetAsFirstSibling();

        ApplyAlpha(0f);
    }

    private Canvas ResolveCanvas()
    {
        if (targetCanvas != null)
            return targetCanvas;

        if (!findCanvasAutomatically)
            return null;

        ProHealthBar bar = FindObjectOfType<ProHealthBar>();
        if (bar != null)
        {
            Canvas barCanvas = bar.GetComponentInParent<Canvas>();
            if (barCanvas != null)
            {
                targetCanvas = barCanvas;
                return targetCanvas;
            }
        }

        Canvas[] canvases = FindObjectsOfType<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || !canvas.isRootCanvas)
                continue;

            if (canvas.renderMode == RenderMode.WorldSpace)
                continue;

            targetCanvas = canvas;
            return targetCanvas;
        }

        return null;
    }

    private Sprite GetOrCreateSprite()
    {
        if (runtimeSprite != null)
            return runtimeSprite;

        runtimeTexture = BuildVignetteTexture();
        runtimeSprite = Sprite.Create(
            runtimeTexture,
            new Rect(0f, 0f, runtimeTexture.width, runtimeTexture.height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);

        return runtimeSprite;
    }

    private Texture2D BuildVignetteTexture()
    {
        int size = Mathf.Max(64, textureSize);
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        float maxRadius = Mathf.Sqrt(2f);
        float clampedInner = Mathf.Clamp01(innerClearRadius);
        float clampedExponent = Mathf.Max(0.5f, edgeExponent);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = ((x + 0.5f) / size) * 2f - 1f;
                float v = ((y + 0.5f) / size) * 2f - 1f;
                float radius01 = Mathf.Sqrt(u * u + v * v) / maxRadius;
                float edge = Mathf.InverseLerp(clampedInner, 1f, radius01);
                float alpha = Mathf.Pow(Mathf.Clamp01(edge), clampedExponent);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return texture;
    }
}
