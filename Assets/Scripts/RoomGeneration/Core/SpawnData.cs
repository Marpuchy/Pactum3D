using UnityEngine;

public class SpawnData
{
    public SpawnType Type { get; }
    public Vector2Int Position { get; }

    public SpawnData(SpawnType type, Vector2Int pos)
    {
        Type = type;
        Position = pos;
    }
}