using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class Room
{
    public int Width { get; }
    public int Height { get; }

    public CellType[,] Grid { get; }
    public List<DoorData> Doors { get; }
    public List<SpawnData> Spawns { get; }
    
    public GameObject RoomGameObject { get; set; }
    public Vector3 Center => new Vector3(Width / 2f, Height / 2f, 0);
    public Vector2Int SpawnPosition { get; set; } = new Vector2Int(-1, -1);

    public List<EnemySpawnData> EnemySpawns { get; } = new();
    public List<ChestSpawnData> ChestSpawns { get; } = new();
    public List<NpcSpawnData> NpcSpawns { get; } = new();
    


    public Room(int width, int height)
    {
        Width = width;
        Height = height;

        Grid = new CellType[width, height];
        Doors = new();
        Spawns = new();
    }
}

