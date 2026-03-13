using UnityEngine;
using System.Collections.Generic;

public class SpecialTileSpawner
{
    private const string ContainerName = "SpecialTiles";
    
    private readonly Dictionary<CellType, GameObject> _prefabs;
    private readonly Transform _container;

    public SpecialTileSpawner(RoomTemplate template)
    {
        _prefabs = new Dictionary<CellType, GameObject>();
        _container = GetOrCreateContainer();

        foreach (var config in template.specialTiles)
        {
            if (config.specialTilePrefab == null)
            {
                continue;
            }
            
            if (!_prefabs.ContainsKey(config.type))
                _prefabs.Add(config.type, config.specialTilePrefab);
        }
    }

    public void Spawn(Room room)
    {
        foreach (var pos in GetAllSpecialTiles(room))
        {
            CellType type = room.Grid[pos.x, pos.y];

            if (!_prefabs.TryGetValue(type, out GameObject prefab))
                continue;

            Vector3 worldPos = new(pos.x + 0.5f, pos.y + 0.5f, 0);

            Object.Instantiate(prefab, worldPos, Quaternion.identity,  _container);
        }
    }

    private Transform GetOrCreateContainer()
    {
        GameObject existing = GameObject.Find(ContainerName);
        if (existing != null)
        {
            return existing.transform;
        }
        
        GameObject container = new GameObject(ContainerName);
        return container.transform;
    }

    private List<Vector2Int> GetAllSpecialTiles(Room room)
    {
        List<Vector2Int> result = new();

        for (int x = 0; x < room.Width; x++)
        {
            for (int y = 0; y < room.Height; y++)
            {
                CellType cell = room.Grid[x, y];
                
                if (cell != CellType.Floor &&
                    cell != CellType.Wall &&
                    cell != CellType.SpawnPoint)
                {
                    result.Add(new Vector2Int(x, y));
                }
            }
        }

        return result;
    }
}