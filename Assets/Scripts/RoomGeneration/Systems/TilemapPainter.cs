using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class TilemapPainter
{
    private readonly Tilemap _tilemap;
    private readonly Dictionary<CellType, TileBase> _tileMap;
    private readonly HashSet<CellType> _prefabCellTypes;
    private readonly TileBase _floorTile;
    private readonly TileBase _wallFallback;
    private readonly TileBase _wallTop;
    private readonly TileBase _wallBottom;
    private readonly TileBase _wallLeft;
    private readonly TileBase _wallRight;
    private readonly TileBase _wallTopLeft;
    private readonly TileBase _wallTopRight;
    private readonly TileBase _wallBottomLeft;
    private readonly TileBase _wallBottomRight;

    public TilemapPainter(Tilemap tilemap, RoomTemplate template)
    {
        _tilemap = tilemap;

        _floorTile = template.floorTile;
        _wallFallback = template.wallTile;
        _wallTop = template.wallTop;
        _wallBottom = template.wallBottom;
        _wallLeft = template.wallLeft;
        _wallRight = template.wallRight;
        _wallTopLeft = template.wallTopLeft;
        _wallTopRight = template.wallTopRight;
        _wallBottomLeft = template.wallBottomLeft;
        _wallBottomRight = template.wallBottomRight;
        _prefabCellTypes = new HashSet<CellType>();

        if (template.specialTiles != null)
        {
            foreach (var config in template.specialTiles)
            {
                if (config != null && config.specialTilePrefab != null)
                    _prefabCellTypes.Add(config.type);
            }
        }

        _tileMap = new Dictionary<CellType, TileBase>
        {
            { CellType.Floor, template.floorTile },
            { CellType.SpawnPoint, template.spawnPointTile}
        };

    }

    public void Paint(Room room)
    {
        _tilemap.ClearAllTiles();

        for (int x = 0; x < room.Width; x++)
        {
            for (int y = 0; y < room.Height; y++)
            {
                Vector3Int pos = new(x, y, 0);

                CellType type = room.Grid[x, y];

                if (type == CellType.Wall)
                {
                    TileBase wallTile = ResolveWallTile(room, x, y);
                    if (wallTile != null)
                    {
                        _tilemap.SetTile(pos, wallTile);
                        _tilemap.SetColliderType(pos, Tile.ColliderType.Grid);
                    }
                }
                else if (_tileMap.TryGetValue(type, out TileBase tile))
                {
                    _tilemap.SetTile(pos, tile);
                    _tilemap.SetColliderType(pos, Tile.ColliderType.None);
                }
                else if (type != CellType.Wall && _prefabCellTypes.Contains(type))
                {
                    _tilemap.SetTile(pos, _floorTile);
                    _tilemap.SetColliderType(pos, Tile.ColliderType.None);
                }
            }
        }

        PaintDoorBackgrounds(room);
    }

    private TileBase ResolveWallTile(Room room, int x, int y)
    {
        bool left = x == 0;
        bool right = x == room.Width - 1;
        bool bottom = y == 0;
        bool top = y == room.Height - 1;

        if (left && bottom)
            return _wallBottomLeft ?? _wallFallback;
        if (right && bottom)
            return _wallBottomRight ?? _wallFallback;
        if (left && top)
            return _wallTopLeft ?? _wallFallback;
        if (right && top)
            return _wallTopRight ?? _wallFallback;
        if (top)
            return _wallTop ?? _wallFallback;
        if (bottom)
            return _wallBottom ?? _wallFallback;
        if (left)
            return _wallLeft ?? _wallFallback;
        if (right)
            return _wallRight ?? _wallFallback;

        return _wallFallback;
    }

    private void PaintDoorBackgrounds(Room room)
    {
        if (room.Doors == null || room.Doors.Count == 0)
            return;

        foreach (var door in room.Doors)
        {
            Vector3Int pos = new(door.Position.x, door.Position.y, 0);
            TileBase wallTile = ResolveWallTile(room, door.Position.x, door.Position.y);

            if (wallTile == null)
                continue;

            _tilemap.SetTile(pos, wallTile);
            _tilemap.SetTileFlags(pos, TileFlags.None);
            _tilemap.SetColliderType(pos, Tile.ColliderType.None);
        }
    }
}
