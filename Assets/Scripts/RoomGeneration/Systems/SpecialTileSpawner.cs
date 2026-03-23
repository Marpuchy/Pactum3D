using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class SpecialTileSpawner
{
    private const string ContainerName = "SpecialTiles";
    private const string PhysicsHostName = "Runtime3DPhysics";
    private const float DefaultSpecialTileHeightOffset = 0.06f;
    private const float DefaultBlockingColliderHeight = 1.15f;
    private const float DefaultTriggerColliderHeight = 1.1f;
    private const int FlatTileSortingOrder = 24;

    private readonly Dictionary<CellType, GameObject> prefabs;
    private readonly HashSet<CellType> tileBackedTypes;
    private readonly Transform container;
    private readonly RoomWorldSpaceSettings worldSpaceSettings;

    public SpecialTileSpawner(RoomTilesetSO tileset, Transform roomRoot, RoomWorldSpaceSettings worldSpaceSettings = null)
    {
        prefabs = new Dictionary<CellType, GameObject>();
        tileBackedTypes = new HashSet<CellType>();
        container = GetOrCreateContainer(roomRoot);
        this.worldSpaceSettings = worldSpaceSettings;

        if (tileset == null || tileset.SpecialTiles == null)
            return;

        foreach (SpecialTileConfig config in tileset.SpecialTiles)
        {
            if (config == null)
                continue;

            if (ShouldUseTileOnlyVisual(config))
                tileBackedTypes.Add(config.type);

            if (config.specialTilePrefab == null || prefabs.ContainsKey(config.type))
                continue;

            prefabs.Add(config.type, config.specialTilePrefab);
        }
    }

    public void Spawn(Room room)
    {
        ClearContainer();

        foreach (Vector2Int pos in GetAllSpecialTiles(room))
        {
            CellType type = room.Grid[pos.x, pos.y];
            if (!prefabs.TryGetValue(type, out GameObject prefab))
                continue;

            Vector3 worldPos = ResolveWorldPosition(pos);
            GameObject instance = Object.Instantiate(prefab, worldPos, Quaternion.identity, container);
            bool usePlanarPresentation = ShouldUsePlanarXZPresentation(type);
            if (usePlanarPresentation)
                AlignToXZPlane(instance);

            ConfigureRuntimePhysics(type, instance, usePlanarPresentation);
            if (tileBackedTypes.Contains(type))
            {
                DisableSpritePresentation(instance);
                continue;
            }

            if (usePlanarPresentation)
            {
                ConfigurePlanarTilePresentation(instance);
                continue;
            }

            Room2_5DPresentationUtility.EnsureDepthSorting(instance, ResolveRenderPreset(type));
        }
    }

    private Vector3 ResolveWorldPosition(Vector2Int tilePosition)
    {
        if (worldSpaceSettings != null)
            return worldSpaceSettings.GridToWorld(tilePosition, orthogonalOffset: DefaultSpecialTileHeightOffset);

        return new Vector3(tilePosition.x + 0.5f, tilePosition.y + 0.5f, 0f);
    }

    private void ClearContainer()
    {
        if (container == null)
            return;

        for (int i = container.childCount - 1; i >= 0; i--)
            Object.Destroy(container.GetChild(i).gameObject);
    }

    private static Transform GetOrCreateContainer(Transform roomRoot)
    {
        if (roomRoot != null)
        {
            Transform existingChild = roomRoot.Find(ContainerName);
            if (existingChild != null)
                return existingChild;

            GameObject childContainer = new GameObject(ContainerName);
            childContainer.transform.SetParent(roomRoot, false);
            return childContainer.transform;
        }

        GameObject existing = GameObject.Find(ContainerName);
        if (existing != null)
            return existing.transform;

        GameObject container = new GameObject(ContainerName);
        return container.transform;
    }

    private static List<Vector2Int> GetAllSpecialTiles(Room room)
    {
        List<Vector2Int> result = new List<Vector2Int>();

        for (int x = 0; x < room.Width; x++)
        {
            for (int y = 0; y < room.Height; y++)
            {
                CellType cell = room.Grid[x, y];
                if (cell == CellType.Floor || cell == CellType.Wall || cell == CellType.SpawnPoint)
                    continue;

                result.Add(new Vector2Int(x, y));
            }
        }

        return result;
    }

    private void ConfigureRuntimePhysics(CellType type, GameObject instance, bool usePlanarPresentation)
    {
        if (instance == null || worldSpaceSettings == null || !worldSpaceSettings.UsesXZPlane)
            return;

        switch (type)
        {
            case CellType.Rock:
                EnsureRuntime3DCollider(instance, isTrigger: false, DefaultBlockingColliderHeight);
                break;
            case CellType.Spike:
            case CellType.Lava:
                EnsureRuntime3DCollider(instance, isTrigger: true, DefaultTriggerColliderHeight);
                break;
        }
    }

    private void EnsureRuntime3DCollider(GameObject instance, bool isTrigger, float height)
    {
        if (instance == null)
            return;

        GameObject physicsHost = GetOrCreate3DPhysicsHost(instance);
        if (physicsHost == null)
            return;

        Collider existingCollider = physicsHost.GetComponent<Collider>();
        BoxCollider collider = existingCollider as BoxCollider;
        if (collider == null)
            collider = physicsHost.AddComponent<BoxCollider>();

        if (collider == null)
            return;

        float cellSize = worldSpaceSettings != null ? worldSpaceSettings.CellSize : 1f;
        float planarSize = Mathf.Max(0.1f, cellSize * 0.88f);
        collider.isTrigger = isTrigger;
        collider.size = new Vector3(planarSize, height, planarSize);
        collider.center = new Vector3(0f, height * 0.5f, 0f);

        TriggerRelay3D relay = physicsHost.GetComponent<TriggerRelay3D>();
        if (isTrigger)
        {
            if (relay == null)
                relay = physicsHost.AddComponent<TriggerRelay3D>();

            relay?.Configure(instance.transform);
        }
        else if (relay != null)
        {
            Object.Destroy(relay);
        }
    }

    private static bool ShouldUsePlanarXZPresentation(CellType type)
    {
        return type == CellType.Spike;
    }

    private static Room2_5DRenderPreset ResolveRenderPreset(CellType type)
    {
        return ShouldUsePlanarXZPresentation(type)
            ? Room2_5DRenderPreset.GroundProp
            : Room2_5DRenderPreset.Prop;
    }

    private static void AlignToXZPlane(GameObject instance)
    {
        if (instance == null)
            return;

        instance.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private static GameObject GetOrCreate3DPhysicsHost(GameObject instance)
    {
        if (instance == null)
            return null;

        Transform existing = instance.transform.Find(PhysicsHostName);
        if (existing != null)
            return existing.gameObject;

        GameObject host = new GameObject(PhysicsHostName);
        host.transform.SetParent(instance.transform, false);
        host.transform.localPosition = Vector3.zero;
        host.transform.localRotation = Quaternion.Inverse(instance.transform.localRotation);
        host.transform.localScale = Vector3.one;
        return host;
    }

    private static void ConfigurePlanarTilePresentation(GameObject instance)
    {
        if (instance == null)
            return;

        SpriteRenderer[] renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            RestoreSceneCompatibleSpriteMaterial(renderer);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.allowOcclusionWhenDynamic = true;
            renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, FlatTileSortingOrder);
        }

        SortingGroup[] sortingGroups = instance.GetComponentsInChildren<SortingGroup>(true);
        for (int i = 0; i < sortingGroups.Length; i++)
        {
            SortingGroup group = sortingGroups[i];
            if (group == null)
                continue;

            group.sortingOrder = Mathf.Max(group.sortingOrder, FlatTileSortingOrder);
        }
    }

    private static void RestoreSceneCompatibleSpriteMaterial(SpriteRenderer renderer)
    {
        if (renderer == null)
            return;

        Material material = renderer.sharedMaterial;
        if (material == null)
            return;

        string materialName = material.name;
        if (string.IsNullOrEmpty(materialName))
            return;

        if (materialName.StartsWith("RuntimeFlatTileSpriteUnlit") ||
            materialName.StartsWith("RuntimeXZSpriteUnlit"))
        {
            renderer.sharedMaterial = null;
        }
    }

    private static bool ShouldUseTileOnlyVisual(SpecialTileConfig config)
    {
        if (config == null)
            return false;

        if (config.specialTileTile != null)
            return true;

        if (config.specialTilePrefab == null)
            return false;

        if (config.specialTilePrefab.GetComponentInChildren<Animator>(true) != null)
            return false;

        if (config.specialTilePrefab.GetComponentInChildren<BreakableBase>(true) != null)
            return false;

        SpriteRenderer renderer = config.specialTilePrefab.GetComponentInChildren<SpriteRenderer>(true);
        return renderer != null && renderer.sprite != null;
    }

    private static void DisableSpritePresentation(GameObject instance)
    {
        if (instance == null)
            return;

        SpriteRenderer[] renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = false;

        SortingGroup[] sortingGroups = instance.GetComponentsInChildren<SortingGroup>(true);
        for (int i = 0; i < sortingGroups.Length; i++)
            sortingGroups[i].enabled = false;
    }
}
