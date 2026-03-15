using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class BlobShadowProjector : MonoBehaviour
{
    private const string ShadowObjectName = "BlobShadow";
    private const int ShadowTextureSize = 64;

    [SerializeField] private Transform followTarget;
    [SerializeField] private float groundOffset = 0.05f;
    [SerializeField] private float fixedDiameter = 1f;
    [SerializeField] private float maxTrackedHeight = 1.5f;
    [SerializeField] private float fixedAlpha = 0.5f;
    [SerializeField] private float planarOffsetTowardsCamera = 0.04f;
    [SerializeField] private float airbornePlanarOffsetMultiplier = 1.45f;
    [SerializeField] private Color shadowColor = Color.black;

    private static Texture2D runtimeShadowTexture;
    private static Sprite runtimeShadowSprite;
    private static Material runtimeShadowMaterial;

    private RoomWorldSpaceSettings worldSpaceSettings;
    private NavMeshAgent navMeshAgent;
    private GameObject shadowObject;
    private SpriteRenderer shadowRenderer;
    private SpriteDepthSorter shadowSorter;

    private void Awake()
    {
        followTarget = followTarget != null ? followTarget : transform;
        worldSpaceSettings = RoomWorldSpaceSettings.Current;
        navMeshAgent = GetComponent<NavMeshAgent>();
        EnsureShadowObject();
        UpdateShadowImmediate();
    }

    private void OnEnable()
    {
        worldSpaceSettings = RoomWorldSpaceSettings.Current;
        navMeshAgent = GetComponent<NavMeshAgent>();
        EnsureShadowObject();

        if (shadowObject != null)
            shadowObject.SetActive(true);

        UpdateShadowImmediate();
    }

    private void LateUpdate()
    {
        UpdateShadowImmediate();
    }

    private void OnDisable()
    {
        if (shadowObject != null)
            shadowObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (shadowObject != null)
            Destroy(shadowObject);
    }

    public void ConfigureRuntime(
        float alpha = 0.5f,
        float shadowDiameter = 1f,
        float trackedHeight = 1.5f,
        float projectedGroundOffset = -1f,
        float projectedPlanarOffsetTowardsCamera = -1f)
    {
        fixedAlpha = Mathf.Clamp01(alpha);
        fixedDiameter = Mathf.Max(0.05f, shadowDiameter);
        maxTrackedHeight = Mathf.Max(0.01f, trackedHeight);
        if (projectedGroundOffset >= 0f)
            groundOffset = projectedGroundOffset;
        if (projectedPlanarOffsetTowardsCamera >= 0f)
            planarOffsetTowardsCamera = projectedPlanarOffsetTowardsCamera;

        UpdateShadowImmediate();
    }

    private void EnsureShadowObject()
    {
        if (shadowObject == null)
        {
            Transform parent = followTarget != null ? followTarget.parent : transform.parent;
            shadowObject = new GameObject(ShadowObjectName);
            shadowObject.transform.SetParent(parent, false);
        }

        shadowObject.layer = gameObject.layer;
        RemoveLegacyShadowComponents();

        if (shadowRenderer == null)
            shadowRenderer = shadowObject.GetComponent<SpriteRenderer>();
        if (shadowRenderer == null)
            shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();

        shadowRenderer.sprite = ResolveShadowSprite();
        shadowRenderer.sharedMaterial = ResolveShadowMaterial();
        shadowRenderer.drawMode = SpriteDrawMode.Simple;
        shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
        shadowRenderer.receiveShadows = false;
        shadowRenderer.allowOcclusionWhenDynamic = false;
        shadowRenderer.maskInteraction = SpriteMaskInteraction.None;
        shadowRenderer.sortingLayerID = ResolveSortingLayerId();

        if (shadowSorter == null)
            shadowSorter = shadowObject.GetComponent<SpriteDepthSorter>();
        if (shadowSorter == null)
            shadowSorter = shadowObject.AddComponent<SpriteDepthSorter>();

        shadowSorter.Configure(900, 16, shadowObject.transform, true);
    }

    private void UpdateShadowImmediate()
    {
        if (followTarget == null)
            followTarget = transform;

        if (worldSpaceSettings == null)
            worldSpaceSettings = RoomWorldSpaceSettings.Current;

        if (worldSpaceSettings == null || !worldSpaceSettings.UsesXZPlane)
        {
            if (shadowObject != null)
                shadowObject.SetActive(false);
            return;
        }

        EnsureShadowObject();
        if (shadowObject == null || shadowRenderer == null)
            return;

        if (!shadowObject.activeSelf)
            shadowObject.SetActive(true);

        float groundY = worldSpaceSettings.Origin.y + worldSpaceSettings.OrthogonalAxisOffset;
        float heightAboveGround = ResolveHeightAboveGround(groundY);
        float heightT = Mathf.InverseLerp(0f, maxTrackedHeight, heightAboveGround);

        Vector3 targetPosition = followTarget.position;
        Vector3 planarOffset = ResolvePlanarOffsetTowardsCamera(heightT);
        shadowObject.transform.position = new Vector3(
            targetPosition.x + planarOffset.x,
            groundY + groundOffset,
            targetPosition.z + planarOffset.z);
        shadowObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        float scale = fixedDiameter;
        shadowObject.transform.localScale = new Vector3(scale, scale, 1f);

        Color color = shadowColor;
        color.a = fixedAlpha;
        shadowRenderer.color = color;
        shadowRenderer.sortingLayerID = ResolveSortingLayerId();
        shadowSorter?.ApplySorting();
    }

    private Vector3 ResolvePlanarOffsetTowardsCamera(float heightT)
    {
        if (planarOffsetTowardsCamera <= 0.0001f)
            return Vector3.zero;

        Camera targetCamera = Camera.main;
        if (targetCamera == null || followTarget == null)
            return Vector3.zero;

        Vector3 direction = targetCamera.transform.position - followTarget.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        float resolvedOffset = planarOffsetTowardsCamera * Mathf.Lerp(1f, airbornePlanarOffsetMultiplier, heightT);
        return direction.normalized * resolvedOffset;
    }

    private float ResolveHeightAboveGround(float groundY)
    {
        float transformHeight = Mathf.Max(0f, (followTarget != null ? followTarget.position.y : transform.position.y) - groundY);
        float agentHeight = 0f;
        if (navMeshAgent != null && navMeshAgent.enabled)
            agentHeight = Mathf.Max(0f, navMeshAgent.baseOffset);

        return Mathf.Max(transformHeight, agentHeight);
    }

    private int ResolveSortingLayerId()
    {
        SpriteRenderer referenceRenderer = ResolveReferenceRenderer();
        return referenceRenderer != null ? referenceRenderer.sortingLayerID : 0;
    }

    private SpriteRenderer ResolveReferenceRenderer()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        SpriteRenderer best = null;
        float bestArea = -1f;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer candidate = renderers[i];
            if (candidate == null || candidate == shadowRenderer || candidate.sprite == null)
                continue;

            Bounds bounds = candidate.bounds;
            float area = Mathf.Max(0.001f, bounds.size.x * bounds.size.y);
            bool isBetter = best == null || (candidate.enabled && !best.enabled) || area > bestArea;
            if (!isBetter)
                continue;

            best = candidate;
            bestArea = area;
        }

        return best;
    }

    private void RemoveLegacyShadowComponents()
    {
        MeshFilter meshFilter = shadowObject.GetComponent<MeshFilter>();
        if (meshFilter != null)
            Destroy(meshFilter);

        MeshRenderer meshRenderer = shadowObject.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
            Destroy(meshRenderer);

        BillboardFacingCamera billboard = shadowObject.GetComponent<BillboardFacingCamera>();
        if (billboard != null)
            Destroy(billboard);
    }

    private static Sprite ResolveShadowSprite()
    {
        if (runtimeShadowSprite != null)
            return runtimeShadowSprite;

        Texture2D texture = ResolveShadowTexture();
        if (texture == null)
            return null;

        runtimeShadowSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            ShadowTextureSize);
        runtimeShadowSprite.name = "RuntimeBlobShadowSprite";
        return runtimeShadowSprite;
    }

    private static Texture2D ResolveShadowTexture()
    {
        if (runtimeShadowTexture != null)
            return runtimeShadowTexture;

        runtimeShadowTexture = new Texture2D(ShadowTextureSize, ShadowTextureSize, TextureFormat.RGBA32, false)
        {
            name = "RuntimeBlobShadow"
        };

        Vector2 center = new Vector2((ShadowTextureSize - 1) * 0.5f, (ShadowTextureSize - 1) * 0.5f);
        float radius = ShadowTextureSize * 0.44f;
        float softRadius = ShadowTextureSize * 0.1f;

        for (int y = 0; y < ShadowTextureSize; y++)
        {
            for (int x = 0; x < ShadowTextureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = 1f - Mathf.InverseLerp(radius - softRadius, radius, distance);
                alpha = Mathf.Clamp01(alpha * alpha * 1.15f);
                runtimeShadowTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        runtimeShadowTexture.filterMode = FilterMode.Bilinear;
        runtimeShadowTexture.wrapMode = TextureWrapMode.Clamp;
        runtimeShadowTexture.Apply();
        return runtimeShadowTexture;
    }

    private static Material ResolveShadowMaterial()
    {
        if (runtimeShadowMaterial != null)
            return runtimeShadowMaterial;

        Shader shader =
            Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ??
            Shader.Find("Sprites/Default");

        if (shader == null)
            return null;

        runtimeShadowMaterial = new Material(shader)
        {
            name = "RuntimeBlobShadowMaterial"
        };

        return runtimeShadowMaterial;
    }
}
