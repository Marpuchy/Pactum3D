using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(CameraFollow2D))]
public sealed class CameraWallOcclusionFade : MonoBehaviour
{
    private const int FallbackMaskTextureSize = 128;
    private const float FallbackMaskAlphaCutoff = 0.01f;

    [SerializeField, Min(0.01f)] private float sphereCastRadius = 0.45f;
    [SerializeField, Min(0.1f)] private float playerOcclusionRadius = 2f;
    [SerializeField, Range(0.01f, 0.5f)] private float fadeRadius = 0.18f;
    [SerializeField, Range(0.005f, 0.35f)] private float fadeSoftness = 0.08f;
    [SerializeField, Range(0f, 1f)] private float minimumOpacity = 0.15f;
    [SerializeField] private Vector2 fadeEllipse = new Vector2(1f, 0.9f);
    [SerializeField] private bool followPlayerScreenPosition = true;
    [SerializeField, Min(0f)] private float targetHeightOffset = 0.9f;
    [SerializeField] private LayerMask occlusionMask = Physics.DefaultRaycastLayers;
    [SerializeField] private string fallbackTargetTag = "Player";

    private readonly RaycastHit[] hits = new RaycastHit[32];
    private readonly Collider[] overlapHits = new Collider[48];
    private readonly HashSet<CameraWallFadeTarget> currentOccluders = new HashSet<CameraWallFadeTarget>();
    private readonly List<CameraWallFadeTarget> trackedOccluders = new List<CameraWallFadeTarget>(24);

    private static Texture2D fallbackMaskTexture;
    private static Sprite fallbackMaskSprite;

    private CameraFollow2D cameraFollow;
    private Camera occlusionCamera;
    private Transform target;
    private GameObject maskObject;
    private SpriteMask spriteMask;
    private Vector2 lastViewportCenter = new Vector2(0.5f, 0.5f);
    private bool hasViewportCenter;

    private void Awake()
    {
        cameraFollow = GetComponent<CameraFollow2D>();
        occlusionCamera = GetComponent<Camera>();
        ResolveTarget();
        DisableMask();
    }

    private void OnDisable()
    {
        DisableMask();
        ClearTrackedOccluders();
    }

    private void LateUpdate()
    {
        ResolveTarget();

        if (!ShouldProcessOcclusion())
        {
            ClearOcclusion();
            return;
        }

        Vector3 cameraPosition = transform.position;
        Vector3 targetPosition = ResolveTargetPoint();
        Vector3 direction = targetPosition - cameraPosition;
        float distance = direction.magnitude;
        if (distance <= 0.01f)
        {
            ClearOcclusion();
            return;
        }

        if (!HasOccludingWall(cameraPosition, direction, distance))
        {
            ClearOcclusion();
            return;
        }

        PopulateCurrentOccluders(cameraPosition);
        if (currentOccluders.Count == 0)
        {
            ClearOcclusion();
            return;
        }

        if (ApplySoftFade(ResolveViewportCenter(targetPosition)))
        {
            DisableMask();
            return;
        }

        UpdateFallbackMask(targetPosition);
        ApplyFallbackMasking();
    }

    private bool ShouldProcessOcclusion()
    {
        if (target == null)
            return false;

        RoomWorldSpaceSettings worldSpaceSettings = cameraFollow != null
            ? cameraFollow.WorldSpaceSettings
            : RoomWorldSpaceSettings.Current;
        return worldSpaceSettings != null && worldSpaceSettings.UsesXZPlane;
    }

    private Vector3 ResolveTargetPoint()
    {
        if (target == null)
            return transform.position;

        Vector3 targetPoint = target.position;
        targetPoint.y += targetHeightOffset;
        return targetPoint;
    }

    private bool IsWithinPlayerOcclusionRadius(Vector3 worldPosition)
    {
        if (target == null)
            return false;

        RoomWorldSpaceSettings worldSpaceSettings = cameraFollow != null
            ? cameraFollow.WorldSpaceSettings
            : RoomWorldSpaceSettings.Current;

        if (worldSpaceSettings == null)
        {
            Vector2 planarDelta = new Vector2(
                worldPosition.x - target.position.x,
                worldPosition.z - target.position.z);
            return planarDelta.sqrMagnitude <= playerOcclusionRadius * playerOcclusionRadius;
        }

        return worldSpaceSettings.PlanarDistance(worldPosition, target.position) <= playerOcclusionRadius;
    }

    private void ResolveTarget()
    {
        if (cameraFollow == null)
            cameraFollow = GetComponent<CameraFollow2D>();
        if (occlusionCamera == null)
            occlusionCamera = GetComponent<Camera>();

        if (cameraFollow != null && cameraFollow.CurrentTarget != null)
        {
            target = cameraFollow.CurrentTarget;
            return;
        }

        if (target != null)
            return;

        if (string.IsNullOrWhiteSpace(fallbackTargetTag))
            return;

        GameObject targetObject = GameObject.FindGameObjectWithTag(fallbackTargetTag);
        if (targetObject != null)
            target = targetObject.transform;
    }

    private bool HasOccludingWall(Vector3 cameraPosition, Vector3 direction, float distance)
    {
        int hitCount = Physics.SphereCastNonAlloc(
            cameraPosition,
            sphereCastRadius,
            direction.normalized,
            hits,
            distance,
            occlusionMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null)
                continue;

            CameraWallFadeTarget fadeTarget = hitCollider.GetComponentInParent<CameraWallFadeTarget>();
            if (fadeTarget == null)
                continue;

            Vector3 samplePosition = hitCollider.ClosestPoint(target.position);
            if (!IsWithinPlayerOcclusionRadius(samplePosition))
                continue;

            return true;
        }

        return false;
    }

    private void PopulateCurrentOccluders(Vector3 cameraPosition)
    {
        currentOccluders.Clear();

        int overlapCount = Physics.OverlapSphereNonAlloc(
            target.position,
            playerOcclusionRadius,
            overlapHits,
            occlusionMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < overlapCount; i++)
        {
            Collider overlapCollider = overlapHits[i];
            if (overlapCollider == null)
                continue;

            CameraWallFadeTarget fadeTarget = overlapCollider.GetComponentInParent<CameraWallFadeTarget>();
            if (fadeTarget == null)
                continue;

            Vector3 samplePosition = overlapCollider.ClosestPoint(target.position);
            if (!IsWithinPlayerOcclusionRadius(samplePosition))
                continue;

            if (!IsOnCameraSideOfPlayer(samplePosition, cameraPosition))
                continue;

            currentOccluders.Add(fadeTarget);
            TrackOccluder(fadeTarget);
        }
    }

    private bool IsOnCameraSideOfPlayer(Vector3 worldPosition, Vector3 cameraPosition)
    {
        Vector2 toCamera = new Vector2(
            cameraPosition.x - target.position.x,
            cameraPosition.z - target.position.z);
        Vector2 toWall = new Vector2(
            worldPosition.x - target.position.x,
            worldPosition.z - target.position.z);

        if (toCamera.sqrMagnitude <= 0.0001f || toWall.sqrMagnitude <= 0.0001f)
            return true;

        return Vector2.Dot(toWall, toCamera) >= 0f;
    }

    private Vector2 ResolveViewportCenter(Vector3 targetPosition)
    {
        if (occlusionCamera == null)
            occlusionCamera = GetComponent<Camera>();

        if (followPlayerScreenPosition || !hasViewportCenter)
        {
            if (occlusionCamera != null)
            {
                Vector3 viewportPoint = occlusionCamera.WorldToViewportPoint(targetPosition);
                if (viewportPoint.z > 0f)
                {
                    lastViewportCenter = new Vector2(
                        Mathf.Clamp01(viewportPoint.x),
                        Mathf.Clamp01(viewportPoint.y));
                    hasViewportCenter = true;
                }
            }
        }

        if (!hasViewportCenter)
        {
            lastViewportCenter = new Vector2(0.5f, 0.5f);
            hasViewportCenter = true;
        }

        return lastViewportCenter;
    }

    private void TrackOccluder(CameraWallFadeTarget occluder)
    {
        if (occluder == null || trackedOccluders.Contains(occluder))
            return;

        trackedOccluders.Add(occluder);
    }

    private bool ApplySoftFade(Vector2 viewportCenter)
    {
        Vector2 resolvedEllipse = new Vector2(
            Mathf.Max(0.01f, fadeEllipse.x),
            Mathf.Max(0.01f, fadeEllipse.y));
        bool appliedSoftFade = currentOccluders.Count > 0;

        for (int i = trackedOccluders.Count - 1; i >= 0; i--)
        {
            CameraWallFadeTarget occluder = trackedOccluders[i];
            if (occluder == null)
            {
                trackedOccluders.RemoveAt(i);
                continue;
            }

            if (!currentOccluders.Contains(occluder))
            {
                occluder.ClearSoftFade();
                occluder.SetMasked(false);
                trackedOccluders.RemoveAt(i);
                continue;
            }

            if (!occluder.TrySetSoftFade(
                viewportCenter,
                fadeRadius,
                fadeSoftness,
                minimumOpacity,
                resolvedEllipse))
            {
                appliedSoftFade = false;
            }
        }

        if (appliedSoftFade)
            return true;

        for (int i = trackedOccluders.Count - 1; i >= 0; i--)
        {
            CameraWallFadeTarget occluder = trackedOccluders[i];
            if (occluder == null)
                continue;

            occluder.ClearSoftFade();
            occluder.SetMasked(false);
        }

        return false;
    }

    private void ApplyFallbackMasking()
    {
        for (int i = trackedOccluders.Count - 1; i >= 0; i--)
        {
            CameraWallFadeTarget occluder = trackedOccluders[i];
            if (occluder == null)
            {
                trackedOccluders.RemoveAt(i);
                continue;
            }

            bool shouldMask = currentOccluders.Contains(occluder);
            occluder.ClearSoftFade();
            occluder.SetMasked(shouldMask);
            if (!shouldMask)
                trackedOccluders.RemoveAt(i);
        }
    }

    private void ClearOcclusion()
    {
        DisableMask();

        ClearTrackedOccluders();
    }

    private void ClearTrackedOccluders()
    {
        currentOccluders.Clear();

        for (int i = trackedOccluders.Count - 1; i >= 0; i--)
        {
            CameraWallFadeTarget occluder = trackedOccluders[i];
            if (occluder == null)
                continue;

            occluder.ClearSoftFade();
            occluder.SetMasked(false);
        }

        trackedOccluders.Clear();
    }

    private void UpdateFallbackMask(Vector3 targetPosition)
    {
        EnsureMaskObject();
        if (maskObject == null)
            return;

        if (!maskObject.activeSelf)
            maskObject.SetActive(true);

        maskObject.transform.position = targetPosition;
        maskObject.transform.rotation = transform.rotation;

        float diameter = playerOcclusionRadius * 2f;
        maskObject.transform.localScale = new Vector3(diameter, diameter, 1f);
    }

    private void EnsureMaskObject()
    {
        if (maskObject == null)
        {
            Transform existing = transform.Find("WallOcclusionMask");
            maskObject = existing != null ? existing.gameObject : null;
        }

        if (maskObject == null)
        {
            maskObject = new GameObject("WallOcclusionMask");
            maskObject.transform.SetParent(transform, false);
            maskObject.layer = gameObject.layer;
            maskObject.SetActive(false);
        }

        if (spriteMask == null)
            spriteMask = maskObject.GetComponent<SpriteMask>();
        if (spriteMask == null)
            spriteMask = maskObject.AddComponent<SpriteMask>();

        spriteMask.sprite = ResolveFallbackMaskSprite();
        spriteMask.alphaCutoff = FallbackMaskAlphaCutoff;
        spriteMask.isCustomRangeActive = false;
    }

    private void DisableMask()
    {
        if (maskObject != null && maskObject.activeSelf)
            maskObject.SetActive(false);
    }

    private static Sprite ResolveFallbackMaskSprite()
    {
        if (fallbackMaskSprite != null)
            return fallbackMaskSprite;

        Texture2D texture = ResolveFallbackMaskTexture();
        if (texture == null)
            return null;

        fallbackMaskSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            FallbackMaskTextureSize);
        fallbackMaskSprite.name = "RuntimeWallOcclusionMask";
        return fallbackMaskSprite;
    }

    private static Texture2D ResolveFallbackMaskTexture()
    {
        if (fallbackMaskTexture != null)
            return fallbackMaskTexture;

        fallbackMaskTexture = new Texture2D(FallbackMaskTextureSize, FallbackMaskTextureSize, TextureFormat.RGBA32, false)
        {
            name = "RuntimeWallOcclusionMaskTexture"
        };

        Vector2 center = new Vector2((FallbackMaskTextureSize - 1) * 0.5f, (FallbackMaskTextureSize - 1) * 0.5f);
        float radius = FallbackMaskTextureSize * 0.44f;
        float softRadius = FallbackMaskTextureSize * 0.1f;

        for (int y = 0; y < FallbackMaskTextureSize; y++)
        {
            for (int x = 0; x < FallbackMaskTextureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = 1f - Mathf.InverseLerp(radius - softRadius, radius, distance);
                alpha = Mathf.Clamp01(alpha);
                fallbackMaskTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        fallbackMaskTexture.filterMode = FilterMode.Bilinear;
        fallbackMaskTexture.wrapMode = TextureWrapMode.Clamp;
        fallbackMaskTexture.Apply();
        return fallbackMaskTexture;
    }
}
