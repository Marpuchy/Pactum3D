using UnityEngine;

public class DoorData
{
    public DoorDirection Direction { get; }
    public Vector2Int Position { get; }

    public DoorData(DoorDirection dir, Vector2Int pos)
    {
        Direction = dir;
        Position = pos;
    }
}