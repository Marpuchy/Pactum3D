using UnityEngine;

public static class RoomStructurePlacementUtility
{
    public static bool IsSidePerimeterCell(Room room, int x)
    {
        return room != null && (x == 0 || x == room.Width - 1);
    }

    public static bool IsCornerPerimeterCell(Room room, int x, int y)
    {
        if (room == null)
            return false;

        bool side = x == 0 || x == room.Width - 1;
        bool front = y == 0 || y == room.Height - 1;
        return side && front;
    }

    public static Vector3 ResolveWallEdgePosition(
        RoomWorldSpaceSettings worldSpaceSettings,
        Room room,
        Vector2Int cell,
        bool sideWall)
    {
        if (worldSpaceSettings == null)
            return Vector3.zero;

        Vector3 worldPosition = worldSpaceSettings.GridToWorld(cell, orthogonalOffset: 0f);
        float halfCell = worldSpaceSettings.CellSize * 0.5f;

        if (sideWall)
        {
            if (cell.x == 0)
                worldPosition.x += halfCell;
            else if (room != null && cell.x == room.Width - 1)
                worldPosition.x -= halfCell;
        }
        else
        {
            if (cell.y == 0)
                worldPosition.z += halfCell;
            else if (room != null && cell.y == room.Height - 1)
                worldPosition.z -= halfCell;
        }

        return worldPosition;
    }

    public static float ResolveSpriteGroundLift(Sprite sprite)
    {
        return sprite == null ? 0f : -sprite.bounds.min.y;
    }
}
