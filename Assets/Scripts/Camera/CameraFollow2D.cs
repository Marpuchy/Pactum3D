using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float smoothTime = 0.15f;
    [Header("Bounds")]
    [SerializeField] private Tilemap boundsTilemap;
    [SerializeField, Range(0f, 0.45f)] private float outsidePercent = 0.2f;
    [SerializeField] private RoomSpawnEvent roomSpawnEvent;

    private Vector3 velocity;
    private Camera cam;
    private CameraShake2D cameraShake;
    private Bounds worldBounds;
    private bool hasBounds;
    private Vector3 lastShakeOffset;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cameraShake = GetComponent<CameraShake2D>();
        RefreshBounds();
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
    }

    private void LateUpdate()
    {
        if (!target) return;

        if (cameraShake == null)
            cameraShake = GetComponent<CameraShake2D>();

        Vector3 currentBasePosition = transform.position - lastShakeOffset;

        Vector3 targetPos = target.position;
        targetPos.z = currentBasePosition.z;
        if (boundsTilemap != null)
            RefreshBounds();
        if (hasBounds && cam != null)
            targetPos = ClampToBounds(targetPos);

        Vector3 basePosition = Vector3.SmoothDamp(
            currentBasePosition,
            targetPos,
            ref velocity,
            smoothTime
        );

        Vector3 shakeOffset = cameraShake != null ? cameraShake.CurrentOffset : Vector3.zero;
        transform.position = basePosition + shakeOffset;
        lastShakeOffset = shakeOffset;
    }

    private void HandleRoomSpawn(Vector3 spawnPosition)
    {
        RefreshBounds();
    }

    private void RefreshBounds()
    {
        if (boundsTilemap == null)
        {
            hasBounds = false;
            return;
        }

        boundsTilemap.CompressBounds();
        Bounds local = boundsTilemap.localBounds;
        if (local.size.x <= 0.01f || local.size.y <= 0.01f)
        {
            hasBounds = false;
            return;
        }
        Vector3 min = boundsTilemap.transform.TransformPoint(local.min);
        Vector3 max = boundsTilemap.transform.TransformPoint(local.max);
        worldBounds.SetMinMax(min, max);
        hasBounds = true;
    }

    private Vector3 ClampToBounds(Vector3 targetPos)
    {
        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;

        float allowOutsideX = 2f * outsidePercent * halfWidth;
        float allowOutsideY = 2f * outsidePercent * halfHeight;

        float minX = worldBounds.min.x + halfWidth - allowOutsideX;
        float maxX = worldBounds.max.x - halfWidth + allowOutsideX;
        float minY = worldBounds.min.y + halfHeight - allowOutsideY;
        float maxY = worldBounds.max.y - halfHeight + allowOutsideY;

        if (minX > maxX)
            targetPos.x = worldBounds.center.x;
        else
            targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);

        if (minY > maxY)
            targetPos.y = worldBounds.center.y;
        else
            targetPos.y = Mathf.Clamp(targetPos.y, minY, maxY);

        return targetPos;
    }
}
