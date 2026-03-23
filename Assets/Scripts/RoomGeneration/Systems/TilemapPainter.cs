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
    private readonly TileBase floorTile;
    private readonly TileBase wallTile;
    private readonly TileBase sharedDoorTile;
    private readonly Dictionary<CellType, TileBase> tileMap;
    private readonly HashSet<CellType> prefabCellTypes;
    private readonly Dictionary<Sprite, TileBase> runtimeSpriteTiles = new();
    private readonly DoorController doorPrefabController;

    public TilemapPainter(
        Tilemap tilemap,
        RoomTilesetSO tileset,
        Room2_5DTilemapLayers layeredTilemaps = null)
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

        floorTile = tileset != null ? tileset.FloorTile : null;
        wallTile = tileset != null ? tileset.WallTile : null;

        if (tileset != null && tileset.DoorPrefab != null)
            doorPrefabController = tileset.DoorPrefab.GetComponent<DoorController>();

        sharedDoorTile = CreateTileFromSprite(ResolveDoorSprite(tileset));
        prefabCellTypes = new HashSet<CellType>();
        tileMap = new Dictionary<CellType, TileBase>
        {
            { CellType.Floor, floorTile },
            { CellType.SpawnPoint, tileset != null ? tileset.SpawnPointTile : null }
        };

        if (tileset == null || tileset.SpecialTiles == null)
            return;

        foreach (SpecialTileConfig config in tileset.SpecialTiles)
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
                    PaintWall(y, pos);
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

    private void PaintWall(int y, Vector3Int pos)
    {
        if (useVerticalWallSpritePresentation || wallTile == null || collisionTilemap == null)
            return;

        collisionTilemap.SetTile(pos, wallTile);
        collisionTilemap.SetColliderType(pos, Tile.ColliderType.Grid);

        if (!useLayeredVisuals)
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

    private void PaintDoorBackgrounds(Room room)
    {
        if (useVerticalWallSpritePresentation || room.Doors == null || room.Doors.Count == 0)
            return;

        TileBase visibleDoorTile = sharedDoorTile ?? wallTile;
        if (visibleDoorTile == null)
            return;

        foreach (DoorData door in room.Doors)
        {
            Vector3Int pos = new Vector3Int(door.Position.x, door.Position.y, 0);

            if (collisionTilemap != null)
            {
                collisionTilemap.SetTile(pos, null);
                collisionTilemap.SetColliderType(pos, Tile.ColliderType.None);
            }

            Tilemap targetTilemap = useLayeredVisuals
                ? ResolveDoorVisualTilemap()
                : collisionTilemap;

            if (targetTilemap == null)
                continue;

            targetTilemap.SetTile(pos, visibleDoorTile);
            targetTilemap.SetTileFlags(pos, TileFlags.None);
            targetTilemap.SetColliderType(pos, Tile.ColliderType.None);
        }
    }

    private void ClearAllConfiguredTilemaps()
    {
        HashSet<Tilemap> unique = new();

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

        if (config.specialTilePrefab == null || !ShouldCreateFallbackTileFromPrefab(config))
            return null;

        SpriteRenderer renderer = config.specialTilePrefab.GetComponentInChildren<SpriteRenderer>(true);
        return CreateTileFromSprite(renderer != null ? renderer.sprite : null);
    }

    private Sprite ResolveDoorSprite(RoomTilesetSO tileset)
    {
        if (tileset != null && tileset.Door != null && tileset.Door.closed != null)
            return tileset.Door.closed;

        DoorSpriteSet sharedDoorSprites = doorPrefabController != null
            ? doorPrefabController.ResolvePreferredSharedSprites()
            : null;
        return sharedDoorSprites != null ? sharedDoorSprites.closed : null;
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
