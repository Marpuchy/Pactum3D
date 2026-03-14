using UnityEngine;

public enum RoomProjectionMode
{
    XY,
    XZ
}

[DisallowMultipleComponent]
[AddComponentMenu("Room/World Space Settings")]
public sealed class RoomWorldSpaceSettings : MonoBehaviour
{
    public static RoomWorldSpaceSettings Current { get; private set; }

    [SerializeField] private RoomProjectionMode projectionMode = RoomProjectionMode.XZ;
    [SerializeField] private Vector3 origin;
    [SerializeField, Min(0.01f)] private float cellSize = 1f;
    [SerializeField] private float orthogonalAxisOffset;

    public RoomProjectionMode ProjectionMode => projectionMode;
    public float CellSize => Mathf.Max(0.01f, cellSize);
    public Vector3 Origin => origin;
    public float OrthogonalAxisOffset => orthogonalAxisOffset;
    public bool UsesXZPlane => projectionMode == RoomProjectionMode.XZ;

    private void Awake()
    {
        Current = this;
    }

    private void OnEnable()
    {
        Current = this;
    }

    private void OnDestroy()
    {
        if (Current == this)
            Current = null;
    }

    public Vector3 GridToWorld(Vector2Int cell, float cellOffsetX = 0.5f, float cellOffsetY = 0.5f, float orthogonalOffset = 0f)
    {
        return GridToWorld(new Vector2(cell.x + cellOffsetX, cell.y + cellOffsetY), orthogonalOffset);
    }

    public Vector3 GridToWorld(Vector2 cellCoordinates, float orthogonalOffset = 0f)
    {
        Vector2 planar = cellCoordinates * CellSize;
        switch (projectionMode)
        {
            case RoomProjectionMode.XZ:
                return origin + new Vector3(planar.x, orthogonalAxisOffset + orthogonalOffset, planar.y);
            default:
                return origin + new Vector3(planar.x, planar.y, orthogonalAxisOffset + orthogonalOffset);
        }
    }

    public Vector3 GridRectCenterToWorld(int width, int height, float orthogonalOffset = 0f)
    {
        return GridToWorld(new Vector2(width * 0.5f, height * 0.5f), orthogonalOffset);
    }

    public Vector3 ClampToWalkPlane(Vector3 position, float orthogonalOffset = 0f)
    {
        switch (projectionMode)
        {
            case RoomProjectionMode.XZ:
                position.y = origin.y + orthogonalAxisOffset + orthogonalOffset;
                break;
            default:
                position.z = origin.z + orthogonalAxisOffset + orthogonalOffset;
                break;
        }

        return position;
    }

    public Vector2 WorldToPlanar(Vector3 worldPosition)
    {
        Vector3 local = worldPosition - origin;
        switch (projectionMode)
        {
            case RoomProjectionMode.XZ:
                return new Vector2(local.x, local.z);
            default:
                return new Vector2(local.x, local.y);
        }
    }

    public Vector2 WorldVectorToPlanar(Vector3 worldVector)
    {
        switch (projectionMode)
        {
            case RoomProjectionMode.XZ:
                return new Vector2(worldVector.x, worldVector.z);
            default:
                return new Vector2(worldVector.x, worldVector.y);
        }
    }

    public float PlanarDistance(Vector3 a, Vector3 b)
    {
        return Vector2.Distance(WorldToPlanar(a), WorldToPlanar(b));
    }

    public void ConfigureProjection(RoomProjectionMode mode, Vector3 newOrigin, float newOrthogonalAxisOffset = 0f)
    {
        projectionMode = mode;
        origin = newOrigin;
        orthogonalAxisOffset = newOrthogonalAxisOffset;
    }
}
