using UnityEngine;
using UnityEngine.Tilemaps;
using Zenject;

[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float smoothTime = 0.15f;
    [SerializeField] private Vector3 followOffset;
    [SerializeField] private bool useSceneOffsetWhenFollowOffsetUnset = false;
    [Header("XZ View")]
    [SerializeField] private bool applyXZDiagonalView = true;
    [SerializeField] private bool forcePerspectiveForXZ = true;
    [SerializeField] private Vector3 xzDiagonalEulerAngles = new Vector3(30f, 0f, 0f);
    [SerializeField] private Vector3 defaultXZFollowOffset = new Vector3(0f, 0f, -18f);
    [SerializeField] private float defaultXZCameraHeight = 8f;
    [SerializeField] private bool clampPerspectiveXZToBounds = false;
    [Header("Bounds")]
    [SerializeField] private Tilemap boundsTilemap;
    [SerializeField] private Room2_5DTilemapLayers layeredTilemaps;
    [SerializeField] private RoomWorldSpaceSettings worldSpaceSettings;
    [SerializeField, Range(0f, 0.45f)] private float outsidePercent = 0.2f;
    [SerializeField] private RoomSpawnEvent roomSpawnEvent;

    private Vector3 velocity;
    private Camera cam;
    private CameraShake2D cameraShake;
    private Bounds worldBounds;
    private bool hasBounds;
    private Vector3 lastShakeOffset;
    private float baseCameraZ;
    private float baseCameraY;
    private bool hasCachedSceneFollowOffset;
    private Vector3 cachedSceneFollowOffset;
    private Transform cachedSceneFollowTarget;
    private Coroutine pendingRoomSpawnSnap;
    private IPlayerTransformProvider playerTransformProvider;

    public Transform CurrentTarget => target;
    public RoomWorldSpaceSettings WorldSpaceSettings => ResolveWorldSpaceSettings();

    [Inject]
    private void Construct([InjectOptional] IPlayerTransformProvider injectedPlayerTransformProvider)
    {
        playerTransformProvider = injectedPlayerTransformProvider;
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cameraShake = GetComponent<CameraShake2D>();
        baseCameraZ = transform.position.z;
        baseCameraY = transform.position.y;
        if (layeredTilemaps == null)
            layeredTilemaps = FindFirstObjectByType<Room2_5DTilemapLayers>();

        worldSpaceSettings = ResolveWorldSpaceSettings();
        ResolveTargetIfNeeded();
        CacheSceneFollowOffsetIfNeeded();
        ApplyProjectionModeIfNeeded();
        ApplyXZViewRotationIfNeeded();
        RefreshBounds();
        EnsureWallOcclusionFade();
    }

    private void OnEnable()
    {
        if (roomSpawnEvent != null)
            roomSpawnEvent.OnRoomSpawn += HandleRoomSpawn;
    }

    private void OnDisable()
    {
        if (roomSpawnEvent != null)
            roomSpawnEvent.OnRoomSpawn -= HandleRoomSpawn;

        if (pendingRoomSpawnSnap != null)
        {
            StopCoroutine(pendingRoomSpawnSnap);
            pendingRoomSpawnSnap = null;
        }
    }

    private void LateUpdate()
    {
        ResolveTargetIfNeeded();
        CacheSceneFollowOffsetIfNeeded();
        if (!target)
            return;

        if (cameraShake == null)
            cameraShake = GetComponent<CameraShake2D>();

        worldSpaceSettings = ResolveWorldSpaceSettings();
        ApplyProjectionModeIfNeeded();
        ApplyXZViewRotationIfNeeded();

        Vector3 currentBasePosition = transform.position - lastShakeOffset;
        Vector3 targetPos = ResolveTargetPosition();
        RefreshBounds();
        if (hasBounds && cam != null)
            targetPos = ClampToBounds(targetPos);

        Vector3 basePosition = Vector3.SmoothDamp(
            currentBasePosition,
            targetPos,
            ref velocity,
            smoothTime
        );

        Vector3 shakeOffset = ResolveShakeOffset();
        transform.position = basePosition + shakeOffset;
        lastShakeOffset = shakeOffset;
    }

    private void HandleRoomSpawn(Vector3 spawnPosition)
    {
        if (pendingRoomSpawnSnap != null)
            StopCoroutine(pendingRoomSpawnSnap);

        pendingRoomSpawnSnap = StartCoroutine(HandleRoomSpawnDeferred());
    }

    private System.Collections.IEnumerator HandleRoomSpawnDeferred()
    {
        yield return null;

        RefreshBounds();
        ResolveTargetIfNeeded();
        CacheSceneFollowOffsetIfNeeded();

        if (!target)
        {
            pendingRoomSpawnSnap = null;
            yield break;
        }

        ApplyXZViewRotationIfNeeded();
        velocity = Vector3.zero;

        Vector3 targetPos = ResolveTargetPosition();
        if (hasBounds && cam != null)
            targetPos = ClampToBounds(targetPos);

        Vector3 shakeOffset = ResolveShakeOffset();
        transform.position = targetPos + shakeOffset;
        lastShakeOffset = shakeOffset;
        pendingRoomSpawnSnap = null;
    }

    private void RefreshBounds()
    {
        if (UsesXZBounds() && RoomBuilder.Current != null)
        {
            Bounds roomBounds = RoomBuilder.Current.CurrentRoomWorldBounds;
            if (roomBounds.size.x > 0.01f && roomBounds.size.z > 0.01f)
            {
                worldBounds = roomBounds;
                hasBounds = true;
                return;
            }
        }

        Tilemap effectiveBoundsTilemap = ResolveBoundsTilemap();
        if (effectiveBoundsTilemap == null)
        {
            hasBounds = false;
            return;
        }

        effectiveBoundsTilemap.CompressBounds();
        Bounds local = effectiveBoundsTilemap.localBounds;
        if (local.size.x <= 0.01f || local.size.y <= 0.01f)
        {
            hasBounds = false;
            return;
        }

        Vector3 min = effectiveBoundsTilemap.transform.TransformPoint(local.min);
        Vector3 max = effectiveBoundsTilemap.transform.TransformPoint(local.max);
        worldBounds.SetMinMax(min, max);
        hasBounds = true;
    }

    private Tilemap ResolveBoundsTilemap()
    {
        if (layeredTilemaps != null && layeredTilemaps.BoundsTilemap != null)
            return layeredTilemaps.BoundsTilemap;

        return boundsTilemap;
    }

    private Vector3 ClampToBounds(Vector3 targetPos)
    {
        if (cam == null)
            return targetPos;

        if (UsesXZBounds() && !cam.orthographic)
        {
            if (!clampPerspectiveXZToBounds)
                return targetPos;

            targetPos.x = Mathf.Clamp(targetPos.x, worldBounds.min.x, worldBounds.max.x);
            targetPos.z = Mathf.Clamp(targetPos.z, worldBounds.min.z, worldBounds.max.z);
            return targetPos;
        }

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;

        float allowOutsideX = 2f * outsidePercent * halfWidth;
        float allowOutsideY = 2f * outsidePercent * halfHeight;

        float minX = worldBounds.min.x + halfWidth - allowOutsideX;
        float maxX = worldBounds.max.x - halfWidth + allowOutsideX;

        if (minX > maxX)
            targetPos.x = worldBounds.center.x;
        else
            targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);

        if (UsesXZBounds())
        {
            float minZ = worldBounds.min.z + halfHeight - allowOutsideY;
            float maxZ = worldBounds.max.z - halfHeight + allowOutsideY;

            if (minZ > maxZ)
                targetPos.z = worldBounds.center.z;
            else
                targetPos.z = Mathf.Clamp(targetPos.z, minZ, maxZ);
        }
        else
        {
            float minY = worldBounds.min.y + halfHeight - allowOutsideY;
            float maxY = worldBounds.max.y - halfHeight + allowOutsideY;

            if (minY > maxY)
                targetPos.y = worldBounds.center.y;
            else
                targetPos.y = Mathf.Clamp(targetPos.y, minY, maxY);
        }

        return targetPos;
    }

    private Vector3 ResolveTargetPosition()
    {
        if (UsesXZBounds())
            return target.position + ResolveXZFollowOffset();

        if (TryGetSceneFollowOffset(out Vector3 xySceneFollowOffset))
            return target.position + xySceneFollowOffset;

        Vector3 targetPos = target.position + followOffset;
        targetPos.z = baseCameraZ + followOffset.z;
        return targetPos;
    }

    private bool TryGetSceneFollowOffset(out Vector3 sceneFollowOffset)
    {
        sceneFollowOffset = Vector3.zero;

        if (!ShouldUseSceneFollowOffset())
            return false;

        CacheSceneFollowOffsetIfNeeded();
        if (!hasCachedSceneFollowOffset)
            return false;

        sceneFollowOffset = cachedSceneFollowOffset;
        return true;
    }

    private Vector3 ResolveShakeOffset()
    {
        Vector3 shakeOffset = cameraShake != null ? cameraShake.CurrentOffset : Vector3.zero;
        if (!UsesXZBounds())
            return shakeOffset;

        return new Vector3(shakeOffset.x, 0f, shakeOffset.y);
    }

    private RoomWorldSpaceSettings ResolveWorldSpaceSettings()
    {
        if (worldSpaceSettings == null)
        {
            worldSpaceSettings = RoomWorldSpaceSettings.Current != null
                ? RoomWorldSpaceSettings.Current
                : FindFirstObjectByType<RoomWorldSpaceSettings>();
        }

        return worldSpaceSettings;
    }

    private bool UsesXZBounds()
    {
        RoomWorldSpaceSettings settings = ResolveWorldSpaceSettings();
        return settings != null && settings.UsesXZPlane;
    }

    private bool ShouldUseSceneFollowOffset()
    {
        return useSceneOffsetWhenFollowOffsetUnset &&
               target != null &&
               followOffset.sqrMagnitude <= 0.0001f;
    }

    private void ApplyXZViewRotationIfNeeded()
    {
        if (!applyXZDiagonalView || !UsesXZBounds())
            return;

        transform.rotation = Quaternion.Euler(xzDiagonalEulerAngles);
    }

    private void ApplyProjectionModeIfNeeded()
    {
        if (cam == null || !UsesXZBounds())
            return;

        if (forcePerspectiveForXZ)
            cam.orthographic = false;

    }

    private void ResolveTargetIfNeeded()
    {
        if (target != null)
            return;

        if (playerTransformProvider?.Current != null)
            target = playerTransformProvider.Current;
    }

    private void CacheSceneFollowOffsetIfNeeded()
    {
        if (!useSceneOffsetWhenFollowOffsetUnset || target == null)
            return;

        if (hasCachedSceneFollowOffset && cachedSceneFollowTarget == target)
            return;

        cachedSceneFollowOffset = transform.position - target.position;
        cachedSceneFollowTarget = target;
        hasCachedSceneFollowOffset = true;
    }

    private Vector3 ResolveXZFollowOffset()
    {
        if (followOffset.sqrMagnitude > 0.0001f)
            return followOffset;

        if (TryGetSceneFollowOffset(out Vector3 sceneFollowOffset))
            return sceneFollowOffset;

        return new Vector3(
            defaultXZFollowOffset.x,
            defaultXZCameraHeight,
            defaultXZFollowOffset.z);
    }

    private void EnsureWallOcclusionFade()
    {
        if (GetComponent<CameraWallOcclusionFade>() == null)
            gameObject.AddComponent<CameraWallOcclusionFade>();
    }
}
