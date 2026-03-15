using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapPainter
{
    private readonly bool useVerticalWallSpritePresentation;
    private readonly Tilemap collisionTilemap;
    private readonly Tilemap floorTilemap;
    private readonly Tilemap wallBackTilemap;
    private readonly Tilemap wallFrontTilemap;
    private readonly bool useLayeredVisuals;
    private readonly int foregroundWallRows;

    private readonly Dictionary<CellType, TileBase> tileMap;
    private readonly HashSet<CellType> prefabCellTypes;
    private readonly TileBase floorTile;
    private readonly TileBase wallFallback;
    private readonly TileBase wallTop;
    private readonly TileBase wallBottom;
    private readonly TileBase wallLeft;
    private readonly TileBase wallRight;
    private readonly TileBase wallTopLeft;
    private readonly TileBase wallTopRight;
    private readonly TileBase wallBottomLeft;
    private readonly TileBase wallBottomRight;
    private readonly TileBase doorUpTile;
    private readonly TileBase doorDownTile;
    private readonly TileBase doorLeftTile;
    private readonly TileBase doorRightTile;
    private readonly Dictionary<Sprite, TileBase> runtimeSpriteTiles = new Dictionary<Sprite, TileBase>();
    private readonly DoorController doorPrefabController;

    public TilemapPainter(
        Tilemap tilemap,
        RoomTemplate template,
        Room2_5DTilemapLayers layeredTilemaps = null,
        GameObject doorPrefab = null)
    {
        collisionTilemap = layeredTilemaps != null && layeredTilemaps.CollisionTilemap != null
            ? layeredTilemaps.CollisionTilemap
            : tilemap;
        floorTilemap = layeredTilemaps != null && layeredTilemaps.FloorTilemap != null
            ? layeredTilemaps.FloorTilemap
            : collisionTilemap;
        wallBackTilemap = layeredTilemaps != null ? layeredTilemaps.WallBackTilemap : null;
        wallFrontTilemap = layeredTilemaps != null ? layeredTilemaps.WallFrontTilemap : null;
        useLayeredVisuals = layeredTilemaps != null && layeredTilemaps.HasLayeredVisuals && collisionTilemap != null;
        foregroundWallRows = layeredTilemaps != null ? layeredTilemaps.ForegroundWallRows : 1;
        useVerticalWallSpritePresentation = RoomWorldSpaceSettings.Current != null && RoomWorldSpaceSettings.Current.UsesXZPlane;

        floorTile = template.floorTile;
        wallFallback = template.wallTile;
        wallTop = template.wallTop;
        wallBottom = template.wallBottom;
        wallLeft = template.wallLeft;
        wallRight = template.wallRight;
        wallTopLeft = template.wallTopLeft;
        wallTopRight = template.wallTopRight;
        wallBottomLeft = template.wallBottomLeft;
        wallBottomRight = template.wallBottomRight;
        if (doorPrefab != null)
            doorPrefabController = doorPrefab.GetComponent<DoorController>();
        doorUpTile = CreateTileFromSprite(ResolveDoorSprite(template, DoorDirection.Up));
        doorDownTile = CreateTileFromSprite(ResolveDoorSprite(template, DoorDirection.Down));
        doorLeftTile = CreateTileFromSprite(ResolveDoorSprite(template, DoorDirection.Left));
        doorRightTile = CreateTileFromSprite(ResolveDoorSprite(template, DoorDirection.Right));
        prefabCellTypes = new HashSet<CellType>();

        tileMap = new Dictionary<CellType, TileBase>
        {
            { CellType.Floor, template.floorTile },
            { CellType.SpawnPoint, template.spawnPointTile }
        };

        if (template.specialTiles != null)
        {
            foreach (SpecialTileConfig config in template.specialTiles)
            {
                if (config == null)
                    continue;

                TileBase specialTile = ResolveSpecialTileTile(config);
                if (specialTile != null)
                {
                    tileMap[config.type] = specialTile;
                    continue;
                }

                if (config.specialTilePrefab != null)
                    prefabCellTypes.Add(config.type);
            }
        }
    }

    public void Paint(Room room)
    {
        ClearAllConfiguredTilemaps();

        for (int x = 0; x < room.Width; x++)
        {
            for (int y = 0; y < room.Height; y++)
            {
                Vector3Int pos = new Vector3Int(x, y, 0);
                CellType type = room.Grid[x, y];

                if (type == CellType.Wall)
                {
                    PaintWall(room, x, y, pos);
                    continue;
                }

                if (tileMap.TryGetValue(type, out TileBase tile))
                {
                    PaintFloor(pos, tile);
                    continue;
                }

                if (type != CellType.Wall && prefabCellTypes.Contains(type))
                    PaintFloor(pos, floorTile);
            }
        }

        PaintDoorBackgrounds(room);
    }

    private void PaintWall(Room room, int x, int y, Vector3Int pos)
    {
        if (useVerticalWallSpritePresentation)
            return;

        TileBase wallTile = ResolveWallTile(room, x, y);
        if (wallTile == null || collisionTilemap == null)
            return;

        collisionTilemap.SetTile(pos, wallTile);
        collisionTilemap.SetColliderType(pos, Tile.ColliderType.Grid);

        if (!useLayeredVisuals || useVerticalWallSpritePresentation)
            return;

        Tilemap targetVisualTilemap = ShouldPaintWallInForeground(y)
            ? ResolveForegroundWallTilemap()
            : ResolveBackgroundWallTilemap();

        if (targetVisualTilemap == null)
            return;

        targetVisualTilemap.SetTile(pos, wallTile);
        targetVisualTilemap.SetColliderType(pos, Tile.ColliderType.None);
    }

    private void PaintFloor(Vector3Int pos, TileBase tile)
    {
        if (tile == null)
            return;

        Tilemap targetTilemap = useLayeredVisuals ? floorTilemap : collisionTilemap;
        if (targetTilemap == null)
            return;

        targetTilemap.SetTile(pos, tile);
        targetTilemap.SetColliderType(pos, Tile.ColliderType.None);

        if (useLayeredVisuals && collisionTilemap != null && collisionTilemap != targetTilemap)
        {
            collisionTilemap.SetTile(pos, null);
            collisionTilemap.SetColliderType(pos, Tile.ColliderType.None);
        }
    }

    private TileBase ResolveWallTile(Room room, int x, int y)
    {
        bool left = x == 0;
        bool right = x == room.Width - 1;
        bool bottom = y == 0;
        bool top = y == room.Height - 1;

        if (left && bottom)
            return wallBottomLeft ?? wallFallback;
        if (right && bottom)
            return wallBottomRight ?? wallFallback;
        if (left && top)
            return wallTopLeft ?? wallFallback;
        if (right && top)
            return wallTopRight ?? wallFallback;
        if (top)
            return wallTop ?? wallFallback;
        if (bottom)
            return wallBottom ?? wallFallback;
        if (left)
            return wallLeft ?? wallFallback;
        if (right)
            return wallRight ?? wallFallback;

        return wallFallback;
    }

    private void PaintDoorBackgrounds(Room room)
    {
        if (useVerticalWallSpritePresentation)
            return;

        if (room.Doors == null || room.Doors.Count == 0)
            return;

        foreach (DoorData door in room.Doors)
        {
            Vector3Int pos = new Vector3Int(door.Position.x, door.Position.y, 0);
            TileBase doorTile = ResolveDoorTile(door.Direction);
            TileBase wallTile = doorTile ?? ResolveWallTile(room, door.Position.x, door.Position.y);

            if (collisionTilemap != null)
            {
                collisionTilemap.SetTile(pos, null);
                collisionTilemap.SetColliderType(pos, Tile.ColliderType.None);
            }
            Tilemap targetTilemap = useLayeredVisuals && !useVerticalWallSpritePresentation
                ? ResolveDoorVisualTilemap()
                : collisionTilemap;

            if (wallTile == null || targetTilemap == null)
                continue;

            targetTilemap.SetTile(pos, wallTile);
            targetTilemap.SetTileFlags(pos, TileFlags.None);
            targetTilemap.SetColliderType(pos, Tile.ColliderType.None);
        }
    }

    private void ClearAllConfiguredTilemaps()
    {
        HashSet<Tilemap> unique = new HashSet<Tilemap>();

        AddIfValid(unique, collisionTilemap);
        AddIfValid(unique, floorTilemap);
        AddIfValid(unique, wallBackTilemap);
        AddIfValid(unique, wallFrontTilemap);

        foreach (Tilemap tilemap in unique)
            tilemap.ClearAllTiles();
    }

    private Tilemap ResolveBackgroundWallTilemap()
    {
        if (wallBackTilemap != null)
            return wallBackTilemap;

        if (wallFrontTilemap != null)
            return wallFrontTilemap;

        return floorTilemap;
    }

    private Tilemap ResolveForegroundWallTilemap()
    {
        if (wallFrontTilemap != null)
            return wallFrontTilemap;

        return ResolveBackgroundWallTilemap();
    }

    private bool ShouldPaintWallInForeground(int y)
    {
        return y < Mathf.Max(1, foregroundWallRows);
    }

    private TileBase ResolveSpecialTileTile(SpecialTileConfig config)
    {
        if (config == null)
            return null;

        if (config.specialTileTile != null)
            return config.specialTileTile;

        if (config.specialTilePrefab == null)
            return null;

        if (!ShouldCreateFallbackTileFromPrefab(config))
            return null;

        SpriteRenderer renderer = config.specialTilePrefab.GetComponentInChildren<SpriteRenderer>(true);
        return CreateTileFromSprite(renderer != null ? renderer.sprite : null);
    }

    private Sprite ResolveDoorSprite(RoomTemplate template, DoorDirection direction)
    {
        Sprite templateSprite = direction switch
        {
            DoorDirection.Up => template.doorUp != null ? template.doorUp.closed : null,
            DoorDirection.Down => template.doorDown != null ? template.doorDown.closed : null,
            DoorDirection.Left => template.doorLeft != null ? template.doorLeft.closed : null,
            DoorDirection.Right => template.doorRight != null ? template.doorRight.closed : null,
            _ => null
        };

        if (templateSprite != null)
            return templateSprite;

        return doorPrefabController != null ? doorPrefabController.GetClosedSprite(direction) : null;
    }

    private TileBase ResolveDoorTile(DoorDirection direction)
    {
        switch (direction)
        {
            case DoorDirection.Up:
                return doorUpTile;
            case DoorDirection.Down:
                return doorDownTile;
            case DoorDirection.Left:
                return doorLeftTile;
            case DoorDirection.Right:
                return doorRightTile;
            default:
                return null;
        }
    }

    private Tilemap ResolveDoorVisualTilemap()
    {
        if (wallFrontTilemap != null)
            return wallFrontTilemap;

        if (wallBackTilemap != null)
            return wallBackTilemap;

        if (floorTilemap != null)
            return floorTilemap;

        return collisionTilemap;
    }

    private static bool ShouldCreateFallbackTileFromPrefab(SpecialTileConfig config)
    {
        if (config == null || config.specialTilePrefab == null)
            return false;

        if (config.specialTilePrefab.GetComponentInChildren<Animator>(true) != null)
            return false;

        if (config.specialTilePrefab.GetComponentInChildren<BreakableBase>(true) != null)
            return false;

        SpriteRenderer renderer = config.specialTilePrefab.GetComponentInChildren<SpriteRenderer>(true);
        return renderer != null && renderer.sprite != null;
    }

    private TileBase CreateTileFromSprite(Sprite sprite)
    {
        if (sprite == null)
            return null;

        if (runtimeSpriteTiles.TryGetValue(sprite, out TileBase existing))
            return existing;

        Tile runtimeTile = ScriptableObject.CreateInstance<Tile>();
        runtimeTile.sprite = sprite;
        runtimeTile.colliderType = Tile.ColliderType.None;
        runtimeSpriteTiles.Add(sprite, runtimeTile);
        return runtimeTile;
    }

    private static void AddIfValid(ICollection<Tilemap> target, Tilemap tilemap)
    {
        if (tilemap != null)
            target.Add(tilemap);
    }
}
