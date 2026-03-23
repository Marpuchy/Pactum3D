using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[AddComponentMenu("Room/Wall Structure Builder")]
public sealed class RoomWallStructureBuilder : MonoBehaviour
{
    private const string StructuresRootName = "WallStructures";
    private const string LegacyStructuresRootName = "WallVisuals";

    [SerializeField] private RoomWorldSpaceSettings worldSpaceSettings;
    [SerializeField, Min(0.1f)] private float wallHeight = 2.5f;
    [SerializeField, Min(0.01f)] private float wallThickness = 0.18f;
    [SerializeField] private int structureLayer;
    [SerializeField] private bool buildOnlyForXZProjection = true;
    [SerializeField] private bool createColliders = true;
    [SerializeField] private bool addNavMeshModifier = true;
    [SerializeField] private int notWalkableArea = 1;

    public float WallHeight => Mathf.Max(0.1f, wallHeight);

    public void Rebuild(Room room, Transform roomRoot, RoomTilesetSO tileset)
    {
        if (roomRoot == null)
            return;

        Transform structuresRoot = GetOrCreateStructuresRoot(roomRoot);
        ClearChildren(structuresRoot);

        if (room == null)
            return;

        if (worldSpaceSettings == null)
            worldSpaceSettings = GetComponent<RoomWorldSpaceSettings>();
        if (worldSpaceSettings == null)
            worldSpaceSettings = GetComponentInParent<RoomWorldSpaceSettings>();
        if (worldSpaceSettings == null)
            worldSpaceSettings = FindFirstObjectByType<RoomWorldSpaceSettings>();

        if (worldSpaceSettings == null)
            return;

        if (buildOnlyForXZProjection && !worldSpaceSettings.UsesXZPlane)
            return;

        Sprite sharedWallSprite = ResolveSharedWallSprite(tileset);
        HashSet<Vector2Int> doorPositions = CollectDoorPositions(room);

        for (int x = 0; x < room.Width; x++)
        {
            for (int y = 0; y < room.Height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (room.Grid[x, y] != CellType.Wall || doorPositions.Contains(cell) || RoomStructurePlacementUtility.IsCornerPerimeterCell(room, x, y))
                    continue;

                BuildWallSegment(structuresRoot, room, cell, sharedWallSprite);
            }
        }
    }

    private void BuildWallSegment(Transform parent, Room room, Vector2Int cell, Sprite sprite)
    {
        GameObject wall = new GameObject($"Wall_{cell.x}_{cell.y}");
        wall.layer = structureLayer;
        wall.transform.SetParent(parent, false);

        bool sideWall = RoomStructurePlacementUtility.IsSidePerimeterCell(room, cell.x);
        wall.transform.SetPositionAndRotation(
            RoomStructurePlacementUtility.ResolveWallEdgePosition(worldSpaceSettings, room, cell, sideWall),
            sideWall ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity);

        if (createColliders)
        {
            BoxCollider collider = wall.AddComponent<BoxCollider>();
            collider.size = new Vector3(
                worldSpaceSettings.CellSize,
                WallHeight,
                wallThickness * worldSpaceSettings.CellSize);
            collider.center = new Vector3(0f, WallHeight * 0.5f, 0f);
        }

        if (addNavMeshModifier)
        {
            NavMeshModifier modifier = wall.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = false;
            modifier.overrideArea = true;
            modifier.area = notWalkableArea;
        }

        if (sprite == null)
            return;

        GameObject visual = new GameObject("Visual");
        visual.layer = structureLayer;
        visual.transform.SetParent(wall.transform, false);
        visual.transform.localPosition = new Vector3(0f, RoomStructurePlacementUtility.ResolveSpriteGroundLift(sprite), 0f);

        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;

        Room2_5DPresentationUtility.EnsureDepthSorting(wall, Room2_5DRenderPreset.Wall);
    }

    private static HashSet<Vector2Int> CollectDoorPositions(Room room)
    {
        HashSet<Vector2Int> positions = new HashSet<Vector2Int>();
        if (room == null || room.Doors == null)
            return positions;

        for (int i = 0; i < room.Doors.Count; i++)
            positions.Add(room.Doors[i].Position);

        return positions;
    }

    private static Sprite ResolveSharedWallSprite(RoomTilesetSO tileset)
    {
        if (tileset == null)
            return null;

        return ExtractSprite(tileset.WallTile);
    }

    private static Sprite ExtractSprite(TileBase tile)
    {
        return tile is Tile concreteTile ? concreteTile.sprite : null;
    }

    private static Transform GetOrCreateStructuresRoot(Transform roomRoot)
    {
        Transform existing = roomRoot.Find(StructuresRootName);
        if (existing != null)
            return existing;

        Transform legacy = roomRoot.Find(LegacyStructuresRootName);
        if (legacy != null)
            return legacy;

        GameObject root = new GameObject(StructuresRootName);
        root.transform.SetParent(roomRoot, false);
        return root.transform;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.Destroy(parent.GetChild(i).gameObject);
    }
}
