using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[AddComponentMenu("Room/Door Structure Builder")]
public sealed class RoomDoorStructureBuilder : MonoBehaviour
{
    private const string DoorsRootName = "Doors";
    private const string RuntimePhysicsHostName = "Runtime3DPhysics";

    [SerializeField] private RoomWorldSpaceSettings worldSpaceSettings;
    [SerializeField, Min(0.1f)] private float doorHeight = 1.8f;
    [SerializeField, Min(0.01f)] private float doorThickness = 0.22f;
    [SerializeField, Min(0.1f)] private float doorWidthFactor = 0.92f;
    [SerializeField] private bool useSharedVisualSetInXZ = true;

    public void Rebuild(Room room, Transform roomRoot, RoomTilesetSO tileset)
    {
        if (room == null || roomRoot == null)
            return;

        Transform parent = GetOrCreateDoorsRoot(roomRoot);
        ClearChildren(parent);

        GameObject doorPrefab = tileset != null ? tileset.DoorPrefab : null;
        if (doorPrefab == null)
            return;

        if (worldSpaceSettings == null)
            worldSpaceSettings = GetComponent<RoomWorldSpaceSettings>();
        if (worldSpaceSettings == null)
            worldSpaceSettings = GetComponentInParent<RoomWorldSpaceSettings>();
        if (worldSpaceSettings == null)
            worldSpaceSettings = FindFirstObjectByType<RoomWorldSpaceSettings>();

        if (worldSpaceSettings == null)
            return;

        for (int i = 0; i < room.Doors.Count; i++)
        {
            DoorData door = room.Doors[i];
            Vector3 position = ResolveDoorEdgePosition(room, door);
            GameObject instance = Instantiate(doorPrefab, position, Quaternion.identity, parent);

            DoorController controller = instance.GetComponent<DoorController>();
            if (controller != null)
                ConfigureDoorSprites(controller, tileset);

            DoorView view = instance.GetComponent<DoorView>();
            if (view != null)
                view.Init(door);

            if (worldSpaceSettings.UsesXZPlane)
            {
                ConfigureXZPresentation(instance, door.Direction);
                if (controller != null)
                    ConfigureRuntime3DColliders(instance, controller, door.Direction);
            }
            else
            {
                Ensure2DPresentation(instance);
            }
        }
    }

    private void ConfigureDoorSprites(DoorController controller, RoomTilesetSO tileset)
    {
        if (controller == null)
            return;

        DoorSpriteSet sharedSet = ResolveSharedDoorSpriteSet(tileset);
        if (sharedSet == null)
            sharedSet = controller.ResolvePreferredSharedSprites();

        controller.ConfigureSharedSprites(
            sharedSet,
            worldSpaceSettings != null && worldSpaceSettings.UsesXZPlane && useSharedVisualSetInXZ && sharedSet != null);
        controller.ApplySpriteSets(sharedSet, sharedSet, sharedSet, sharedSet);
    }

    private void ConfigureXZPresentation(GameObject instance, DoorDirection direction)
    {
        if (instance == null)
            return;

        EnableSpritePresentation(instance);

        SpriteRenderer renderer = instance.GetComponentInChildren<SpriteRenderer>(true);
        if (renderer != null && renderer.sprite != null)
        {
            Vector3 position = instance.transform.position;
            position.y += RoomStructurePlacementUtility.ResolveSpriteGroundLift(renderer.sprite);
            instance.transform.position = position;
        }

        bool sideDoor = direction == DoorDirection.Left || direction == DoorDirection.Right;
        instance.transform.rotation = sideDoor ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
        Room2_5DPresentationUtility.EnsureDepthSorting(instance, Room2_5DRenderPreset.Door);
    }

    private void Ensure2DPresentation(GameObject instance)
    {
        EnableSpritePresentation(instance);
    }

    private void ConfigureRuntime3DColliders(GameObject instance, DoorController controller, DoorDirection direction)
    {
        if (instance == null || controller == null || worldSpaceSettings == null || !worldSpaceSettings.UsesXZPlane)
            return;

        GameObject physicsHost = GetOrCreateRuntimePhysicsHost(instance);
        if (physicsHost == null)
            return;

        float cellSize = worldSpaceSettings.CellSize;
        float resolvedThickness = Mathf.Max(0.18f, cellSize * doorThickness);
        float resolvedWidth = Mathf.Max(0.6f, cellSize * doorWidthFactor);

        Vector3 size = direction == DoorDirection.Left || direction == DoorDirection.Right
            ? new Vector3(resolvedThickness, doorHeight, resolvedWidth)
            : new Vector3(resolvedWidth, doorHeight, resolvedThickness);
        Vector3 center = new Vector3(0f, doorHeight * 0.5f, 0f);

        BoxCollider blockCollider = physicsHost.AddComponent<BoxCollider>();
        blockCollider.isTrigger = false;
        blockCollider.size = size;
        blockCollider.center = center;

        BoxCollider triggerCollider = physicsHost.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.size = size;
        triggerCollider.center = center;

        TriggerRelay3D relay = physicsHost.GetComponent<TriggerRelay3D>();
        if (relay == null)
            relay = physicsHost.AddComponent<TriggerRelay3D>();

        relay.Configure(controller);
        controller.ConfigureRuntime3DColliders(blockCollider, triggerCollider);
    }

    private Vector3 ResolveDoorEdgePosition(Room room, DoorData door)
    {
        bool sideDoor = door.Direction == DoorDirection.Left || door.Direction == DoorDirection.Right;
        return RoomStructurePlacementUtility.ResolveWallEdgePosition(worldSpaceSettings, room, door.Position, sideDoor);
    }

    private static DoorSpriteSet ResolveSharedDoorSpriteSet(RoomTilesetSO tileset)
    {
        if (tileset == null)
            return null;

        return HasDoorSprites(tileset.Door) ? tileset.Door : null;
    }

    private static bool HasDoorSprites(DoorSpriteSet set)
    {
        return set != null && (set.closed != null || set.open != null);
    }

    private static void EnableSpritePresentation(GameObject instance)
    {
        SpriteRenderer[] renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = true;

        SortingGroup[] sortingGroups = instance.GetComponentsInChildren<SortingGroup>(true);
        for (int i = 0; i < sortingGroups.Length; i++)
            sortingGroups[i].enabled = true;
    }

    private static GameObject GetOrCreateRuntimePhysicsHost(GameObject instance)
    {
        Transform existing = instance.transform.Find(RuntimePhysicsHostName);
        if (existing != null)
            return existing.gameObject;

        GameObject host = new GameObject(RuntimePhysicsHostName);
        host.transform.SetParent(instance.transform, false);
        host.transform.localPosition = Vector3.zero;
        host.transform.localRotation = Quaternion.Inverse(instance.transform.localRotation);
        host.transform.localScale = Vector3.one;
        return host;
    }

    private static Transform GetOrCreateDoorsRoot(Transform roomRoot)
    {
        Transform existing = roomRoot.Find(DoorsRootName);
        if (existing != null)
            return existing;

        GameObject root = new GameObject(DoorsRootName);
        root.transform.SetParent(roomRoot, false);
        return root.transform;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.Destroy(parent.GetChild(i).gameObject);
    }
}
