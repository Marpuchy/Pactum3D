using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CameraWallFadeTarget : MonoBehaviour
{
    private const string OcclusionShaderName = "Pactum/WallSoftOcclusionSpriteLit";
    private const string OcclusionShaderResourcePath = "WallSoftOcclusionSpriteLit";

    private static readonly int OcclusionEnabledId = Shader.PropertyToID("_OcclusionEnabled");
    private static readonly int OcclusionScreenCenterId = Shader.PropertyToID("_OcclusionScreenCenter");
    private static readonly int OcclusionRadiusId = Shader.PropertyToID("_OcclusionRadius");
    private static readonly int OcclusionSoftnessId = Shader.PropertyToID("_OcclusionSoftness");
    private static readonly int OcclusionMinAlphaId = Shader.PropertyToID("_OcclusionMinAlpha");
    private static readonly int OcclusionEllipseId = Shader.PropertyToID("_OcclusionEllipse");

    private static Shader cachedOcclusionShader;

    [SerializeField] private bool includeInactiveChildren = true;
    [SerializeField] private SpriteRenderer[] spriteRenderers = Array.Empty<SpriteRenderer>();
    [SerializeField] private SpriteMaskInteraction[] baseMaskInteractions = Array.Empty<SpriteMaskInteraction>();
    [SerializeField] private Material[] originalMaterials = Array.Empty<Material>();

    private Material[] runtimeOcclusionMaterials = Array.Empty<Material>();
    private bool isMasked;
    private bool softFadeEnabled;
    private Vector2 softFadeScreenCenter = new Vector2(0.5f, 0.5f);
    private float softFadeRadius = 0.18f;
    private float softFadeSoftness = 0.08f;
    private float softFadeMinAlpha = 0.15f;
    private Vector2 softFadeEllipse = Vector2.one;

    public static bool SupportsSoftFade => ResolveOcclusionShader() != null;

    private void Awake()
    {
        RefreshTargets();
        ApplyState();
    }

    private void OnEnable()
    {
        RefreshTargets();
        ApplyState();
    }

    private void OnDisable()
    {
        softFadeEnabled = false;
        isMasked = false;
        RestoreOriginalState();
    }

    private void OnDestroy()
    {
        DisposeRuntimeMaterials();
    }

    public bool TrySetSoftFade(Vector2 screenCenter, float radius, float softness, float minimumAlpha, Vector2 ellipse)
    {
        softFadeEnabled = true;
        isMasked = false;
        softFadeScreenCenter = screenCenter;
        softFadeRadius = Mathf.Max(0.001f, radius);
        softFadeSoftness = Mathf.Max(0.001f, softness);
        softFadeMinAlpha = Mathf.Clamp01(minimumAlpha);
        softFadeEllipse = new Vector2(
            Mathf.Max(0.01f, ellipse.x),
            Mathf.Max(0.01f, ellipse.y));

        RefreshTargets();
        if (!EnsureRuntimeMaterials())
        {
            softFadeEnabled = false;
            ApplyState();
            return false;
        }

        ApplyState();
        return true;
    }

    public void ClearSoftFade()
    {
        if (!softFadeEnabled && !UsesOcclusionMaterial())
            return;

        softFadeEnabled = false;
        RefreshTargets();
        ApplyState();
    }

    public void SetMasked(bool masked)
    {
        softFadeEnabled = false;
        isMasked = masked;
        RefreshTargets();
        ApplyState();
    }

    public void RefreshTargets()
    {
        ResolveRenderers();
        CacheBaseMaskInteractions();
        CacheOriginalMaterials();
    }

    private void ResolveRenderers()
    {
        SpriteRenderer[] resolvedRenderers = GetComponentsInChildren<SpriteRenderer>(includeInactiveChildren);
        if (resolvedRenderers == null)
        {
            spriteRenderers = Array.Empty<SpriteRenderer>();
            baseMaskInteractions = Array.Empty<SpriteMaskInteraction>();
            originalMaterials = Array.Empty<Material>();
            return;
        }

        if (HasSameRendererSet(spriteRenderers, resolvedRenderers))
            return;

        DisposeRuntimeMaterials();
        spriteRenderers = resolvedRenderers;
        baseMaskInteractions = Array.Empty<SpriteMaskInteraction>();
        originalMaterials = Array.Empty<Material>();
    }

    private void CacheBaseMaskInteractions()
    {
        if (spriteRenderers == null)
            return;

        if (baseMaskInteractions != null && baseMaskInteractions.Length == spriteRenderers.Length)
            return;

        baseMaskInteractions = new SpriteMaskInteraction[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer renderer = spriteRenderers[i];
            baseMaskInteractions[i] = renderer != null
                ? renderer.maskInteraction
                : SpriteMaskInteraction.None;
        }
    }

    private void CacheOriginalMaterials()
    {
        if (spriteRenderers == null)
            return;

        if (originalMaterials != null && originalMaterials.Length == spriteRenderers.Length)
            return;

        originalMaterials = new Material[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer renderer = spriteRenderers[i];
            Material currentMaterial = renderer != null ? renderer.sharedMaterial : null;
            originalMaterials[i] = IsRuntimeOcclusionMaterial(currentMaterial) ? null : currentMaterial;
        }
    }

    private void ApplyState()
    {
        bool canUseSoftFade = softFadeEnabled && EnsureRuntimeMaterials();

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer renderer = spriteRenderers[i];
            if (renderer == null)
                continue;

            SpriteMaskInteraction baseInteraction =
                baseMaskInteractions != null && i < baseMaskInteractions.Length
                    ? baseMaskInteractions[i]
                    : SpriteMaskInteraction.None;

            renderer.maskInteraction = !canUseSoftFade && isMasked
                ? SpriteMaskInteraction.VisibleOutsideMask
                : baseInteraction;

            if (canUseSoftFade)
            {
                Material runtimeMaterial = i < runtimeOcclusionMaterials.Length ? runtimeOcclusionMaterials[i] : null;
                if (runtimeMaterial != null)
                {
                    runtimeMaterial.SetFloat(OcclusionEnabledId, 1f);
                    runtimeMaterial.SetVector(OcclusionScreenCenterId, new Vector4(softFadeScreenCenter.x, softFadeScreenCenter.y, 0f, 0f));
                    runtimeMaterial.SetFloat(OcclusionRadiusId, softFadeRadius);
                    runtimeMaterial.SetFloat(OcclusionSoftnessId, softFadeSoftness);
                    runtimeMaterial.SetFloat(OcclusionMinAlphaId, softFadeMinAlpha);
                    runtimeMaterial.SetVector(OcclusionEllipseId, new Vector4(softFadeEllipse.x, softFadeEllipse.y, 0f, 0f));

                    if (renderer.sharedMaterial != runtimeMaterial)
                        renderer.sharedMaterial = runtimeMaterial;

                    renderer.SetPropertyBlock(null);
                    continue;
                }
            }

            renderer.SetPropertyBlock(null);
            Material originalMaterial = i < originalMaterials.Length ? originalMaterials[i] : null;
            if (renderer.sharedMaterial != originalMaterial)
                renderer.sharedMaterial = originalMaterial;
        }
    }

    private void RestoreOriginalState()
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer renderer = spriteRenderers[i];
            if (renderer == null)
                continue;

            SpriteMaskInteraction baseInteraction =
                baseMaskInteractions != null && i < baseMaskInteractions.Length
                    ? baseMaskInteractions[i]
                    : SpriteMaskInteraction.None;
            renderer.maskInteraction = baseInteraction;

            renderer.SetPropertyBlock(null);
            Material originalMaterial = i < originalMaterials.Length ? originalMaterials[i] : null;
            if (renderer.sharedMaterial != originalMaterial)
                renderer.sharedMaterial = originalMaterial;
        }
    }

    private bool UsesOcclusionMaterial()
    {
        if (runtimeOcclusionMaterials == null || runtimeOcclusionMaterials.Length == 0)
            return false;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer renderer = spriteRenderers[i];
            Material runtimeMaterial = i < runtimeOcclusionMaterials.Length ? runtimeOcclusionMaterials[i] : null;
            if (renderer != null && runtimeMaterial != null && renderer.sharedMaterial == runtimeMaterial)
                return true;
        }

        return false;
    }

    private bool EnsureRuntimeMaterials()
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
            return false;

        Shader occlusionShader = ResolveOcclusionShader();
        if (occlusionShader == null)
            return false;

        if (runtimeOcclusionMaterials == null || runtimeOcclusionMaterials.Length != spriteRenderers.Length)
            runtimeOcclusionMaterials = new Material[spriteRenderers.Length];

        bool hasMaterial = false;
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null)
                continue;

            Material runtimeMaterial = runtimeOcclusionMaterials[i];
            if (runtimeMaterial != null && runtimeMaterial.shader != occlusionShader)
            {
                DestroyRuntimeMaterial(runtimeMaterial);
                runtimeOcclusionMaterials[i] = null;
                runtimeMaterial = null;
            }

            if (runtimeMaterial == null)
            {
                runtimeMaterial = new Material(occlusionShader)
                {
                    name = $"{spriteRenderers[i].name}_WallSoftOcclusion (Runtime)",
                    hideFlags = HideFlags.HideAndDontSave
                };
                runtimeMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                runtimeOcclusionMaterials[i] = runtimeMaterial;
            }

            hasMaterial = true;
        }

        return hasMaterial;
    }

    private static Shader ResolveOcclusionShader()
    {
        if (cachedOcclusionShader != null)
            return cachedOcclusionShader;

        Shader occlusionShader = Shader.Find(OcclusionShaderName);
        if (occlusionShader == null)
            occlusionShader = Resources.Load<Shader>(OcclusionShaderResourcePath);
        if (occlusionShader == null || !occlusionShader.isSupported)
            return null;

        cachedOcclusionShader = occlusionShader;
        return cachedOcclusionShader;
    }

    private bool IsRuntimeOcclusionMaterial(Material material)
    {
        if (material == null || runtimeOcclusionMaterials == null)
            return false;

        for (int i = 0; i < runtimeOcclusionMaterials.Length; i++)
        {
            if (runtimeOcclusionMaterials[i] == material)
                return true;
        }

        return false;
    }

    private void DisposeRuntimeMaterials()
    {
        if (runtimeOcclusionMaterials == null)
            return;

        for (int i = 0; i < runtimeOcclusionMaterials.Length; i++)
        {
            DestroyRuntimeMaterial(runtimeOcclusionMaterials[i]);
            runtimeOcclusionMaterials[i] = null;
        }
    }

    private static void DestroyRuntimeMaterial(Material material)
    {
        if (material == null)
            return;

        if (Application.isPlaying)
        {
            Destroy(material);
            return;
        }

        DestroyImmediate(material);
    }

    private static bool HasSameRendererSet(SpriteRenderer[] current, SpriteRenderer[] candidate)
    {
        if (current == null || candidate == null || current.Length != candidate.Length)
            return false;

        for (int i = 0; i < current.Length; i++)
        {
            if (current[i] != candidate[i])
                return false;
        }

        return true;
    }
}
