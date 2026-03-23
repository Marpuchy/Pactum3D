using System;
using System.Collections;
using System.Collections.Generic;
using SaveSystem;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Tilemaps;
using Unity.AI.Navigation;
using Random = UnityEngine.Random;

public class RoomBuilder : MonoBehaviour
{
    private const string HumanoidAgentTypeName = "Humanoid";
    private const string FlyingAgentTypeName = "Flying";
    private const float AgentRebindMaxDistance = 128f;
    private const float DefaultSpawnedContentHeightOffset = 0.06f;

    private static readonly FieldInfo NavMeshModifierAffectedAgentsField =
        typeof(NavMeshModifier).GetField("m_AffectedAgents", BindingFlags.Instance | BindingFlags.NonPublic);

    public static RoomBuilder Current { get; private set; }

    [SerializeField] private Tilemap tilemap;
    [SerializeField] private Room2_5DTilemapLayers layeredTilemaps;
    [SerializeField] private RoomWorldSpaceSettings worldSpaceSettings;
    [SerializeField] private Room3DGeometryBuilder room3DGeometryBuilder;
    [SerializeField] private RoomWallStructureBuilder roomWallStructureBuilder;
    [SerializeField] private RoomDoorStructureBuilder roomDoorStructureBuilder;
    [SerializeField] private RoomSpawnEvent onRoomSpawnEvent;
    [SerializeField] private EnemyDeathEvent enemyDeathEvent;
    [SerializeField] private RoomClearedEvent roomClearedEvent;
    [SerializeField] private NavMeshSurface navMeshSurface;
    [SerializeField] private List<NavMeshSurface> navMeshSurfaces = new List<NavMeshSurface>();
    [SerializeField] private RoomTemplateSequenceSO roomSequenceConfig;

    [Header("Runtime Generation")]
    [SerializeField] private bool usePrototypeRoom = false;
    [SerializeField] private Vector2Int prototypeWalkableSize = new Vector2Int(6, 6);
    [SerializeField, Min(1)] private int prototypeWallThickness = 1;

    [SerializeField] private AudioClip doorClip;

    [Header("Items")]
    [SerializeField] private GameObject worldItemPrefab;

    [Header("Enemy Placement")]
    [SerializeField] private float groundedEnemyBaseOffset = 0f;
    [SerializeField] private float flyingEnemyBaseOffset = 0.5f;
    [SerializeField] private float groundedEnemyHurtboxOffset = 0f;
    [SerializeField] private float flyingEnemyHurtboxOffset = -0.25f;

    [Header("Enemy Shadow")]
    [SerializeField] private float enemyShadowGroundOffset = 0.03f;
    [SerializeField] private float enemyShadowAlpha = 0.62f;
    [SerializeField] private float enemyShadowDiameter = 0.62f;
    [SerializeField] private float enemyShadowTrackedHeight = 1.5f;
    [SerializeField] private float enemyShadowPlanarOffsetTowardsCamera = 0.14f;

    private IRoomGenerator _generator;
    private TilemapPainter _painter;
    private SpecialTileSpawner _spawner;
    private int enemyCount;
    private RoomTemplateSelector _templateSelector;
    private TileOccupancy _tileOccupancy;
    private readonly List<GameplayTag> lootTagBuffer = new List<GameplayTag>(8);
    private int runSeed;
    private int currentRoomSeed;
    private bool isCurrentNpcRoom;
    private NavMeshModifier roomWallsNavMeshModifier;
    private Room currentRoom;
    private Bounds currentRoomWorldBounds;
    private int prototypeRoomNumber;

    public int CurrentRoomNumber => usePrototypeRoom
        ? Mathf.Max(1, prototypeRoomNumber)
        : (_templateSelector != null ? _templateSelector.CurrentRoomNumber : 0);
    public int CurrentRunSeed => runSeed;
    public int CurrentRoomSeed => currentRoomSeed;
    public bool IsCurrentNpcRoom => isCurrentNpcRoom;
    public RoomSpawnEvent RoomSpawnEvent => onRoomSpawnEvent;
    public Bounds CurrentRoomWorldBounds => currentRoomWorldBounds;

    private void Awake()
    {
        Current = this;
        if (layeredTilemaps == null)
            layeredTilemaps = GetComponentInChildren<Room2_5DTilemapLayers>(true);
        if (layeredTilemaps == null)
            layeredTilemaps = FindFirstObjectByType<Room2_5DTilemapLayers>();
        if (worldSpaceSettings == null)
            worldSpaceSettings = GetComponentInChildren<RoomWorldSpaceSettings>(true);
        if (worldSpaceSettings == null)
            worldSpaceSettings = FindFirstObjectByType<RoomWorldSpaceSettings>();
        if (worldSpaceSettings == null)
            worldSpaceSettings = gameObject.AddComponent<RoomWorldSpaceSettings>();
        if (!worldSpaceSettings.UsesXZPlane)
        {
            worldSpaceSettings.ConfigureProjection(
                RoomProjectionMode.XZ,
                worldSpaceSettings.Origin,
                worldSpaceSettings.OrthogonalAxisOffset);
        }
        if (room3DGeometryBuilder == null)
            room3DGeometryBuilder = GetComponentInChildren<Room3DGeometryBuilder>(true);
        if (room3DGeometryBuilder == null)
            room3DGeometryBuilder = gameObject.AddComponent<Room3DGeometryBuilder>();
        if (roomWallStructureBuilder == null)
            roomWallStructureBuilder = GetComponentInChildren<RoomWallStructureBuilder>(true);
        if (roomWallStructureBuilder == null)
            roomWallStructureBuilder = gameObject.AddComponent<RoomWallStructureBuilder>();
        if (roomDoorStructureBuilder == null)
            roomDoorStructureBuilder = GetComponentInChildren<RoomDoorStructureBuilder>(true);
        if (roomDoorStructureBuilder == null)
            roomDoorStructureBuilder = gameObject.AddComponent<RoomDoorStructureBuilder>();

        layeredTilemaps?.ApplyRuntimeSetup();
        AlignTilemapGridToWorldSpace();
        EnsureNavMeshAgentSurfaces();
        EnsureNavMeshWallBlockingConfiguration();
        PactRuntimeContext runtimeContext = PactRuntimeContext.Ensure();
        INpcSelector npcSelector = runtimeContext != null ? runtimeContext.NpcSelector : null;
        _generator = new IsaacRoomGenerator(npcSelector);
        _templateSelector = new RoomTemplateSelector(roomSequenceConfig);

        if (PendingGameSaveState.TryGet(out GameSaveData pendingData))
        {
            int targetCurrentRoom = Mathf.Max(0, pendingData.State.RoomNumber - 1);
            if (targetCurrentRoom > 0)
                _templateSelector.SetCurrentRoomNumber(targetCurrentRoom);

            runSeed = SanitizeSeed(pendingData.State.RunSeed);
        }

        if (runSeed == 0)
            runSeed = GenerateNewRunSeed();
    }

    private void OnDestroy()
    {
        if (Current == this)
            Current = null;
    }
    public void Build(Transform parent)
    {
        if (usePrototypeRoom)
        {
            BuildPrototypeRoom(parent);
            return;
        }

        layeredTilemaps?.ApplyRuntimeSetup();
        EnsureNavMeshAgentSurfaces();
        EnsureNavMeshWallBlockingConfiguration();

        RoomTemplate template = _templateSelector.GetNextTemplate();
        if (template == null)
        {
            Debug.LogError($"RoomBuilder: selected template is null for room {CurrentRoomNumber}.", this);
            return;
        }

        RoomTilesetSO tileset = ResolveTileset(template);
        if (tileset == null)
            return;

        isCurrentNpcRoom = roomSequenceConfig != null &&
                           roomSequenceConfig.npcRoomTemplate != null &&
                           template == roomSequenceConfig.npcRoomTemplate;
        currentRoomSeed = BuildRoomSeed(runSeed, CurrentRoomNumber);
        
        _tileOccupancy = new TileOccupancy();
        _painter = new TilemapPainter(ResolveCollisionTilemap(), tileset, layeredTilemaps);
        _spawner = new SpecialTileSpawner(tileset, parent, worldSpaceSettings);

        StartCoroutine(BuildAndBakeRoutine(parent, template, currentRoomSeed));
    }

    private void BuildPrototypeRoom(Transform parent)
    {
        layeredTilemaps?.ApplyRuntimeSetup();

        prototypeRoomNumber++;
        isCurrentNpcRoom = false;
        enemyCount = 0;
        currentRoomSeed = BuildRoomSeed(runSeed, prototypeRoomNumber);
        _tileOccupancy = new TileOccupancy();

        StartCoroutine(BuildPrototypeRoomRoutine(parent, currentRoomSeed));
    }

    private IEnumerator BuildPrototypeRoomRoutine(Transform parent, int roomSeed)
    {
        RoomTemplate template = ResolvePrototypeTemplate();
        if (template == null)
        {
            Debug.LogError("RoomBuilder: no prototype room template is available.", this);
            yield break;
        }

        RoomTilesetSO tileset = ResolveTileset(template);
        if (tileset == null)
            yield break;

        Random.State previousRandomState = Random.state;
        Random.InitState(SanitizeSeed(roomSeed));
        Room room = _generator.Generate(template);
        Random.state = previousRandomState;

        ConfigurePrototypeWorldOrigin(room);
        currentRoom = room;
        currentRoomWorldBounds = CalculateRoomWorldBounds(room);
        _painter = new TilemapPainter(ResolveCollisionTilemap(), tileset, layeredTilemaps);

        ClearLegacyTilemaps();
        AlignTilemapGridToWorldSpace();
        ClearPrototypeRuntimeContainers(parent);
        room3DGeometryBuilder?.Rebuild(room, parent);
        _painter.Paint(room);
        BuildWallStructures(room, parent, tileset);
        RebuildDoorStructures(room, parent, tileset);

        NavMeshSurface prototypeSurface = EnsurePrototypeNavMeshSurface(parent);
        DisableLegacyNavMeshSurfacesForPrototype(parent);

        Physics.SyncTransforms();
        Physics2D.SyncTransforms();
        yield return null;

        List<NavMeshAgent> temporarilyDisabledAgents = DisableAgentsForNavMeshRebuild();
        List<AsyncOperation> buildOperations = new List<AsyncOperation>(1);

        if (prototypeSurface != null)
        {
            AsyncOperation operation = null;
            if (prototypeSurface.navMeshData == null)
            {
                prototypeSurface.BuildNavMesh();
            }
            else
            {
                operation = prototypeSurface.UpdateNavMesh(prototypeSurface.navMeshData);
            }

            if (operation != null)
                buildOperations.Add(operation);
        }

        while (HasPendingNavMeshBuild(buildOperations))
            yield return null;

        RestoreAgentsAfterNavMeshRebuild(temporarilyDisabledAgents);
        yield return null;

        RebindAllAgentsToCurrentNavMesh();
        onRoomSpawnEvent?.Raise(ResolvePrototypeSpawnWorldPosition(room));
    }

    private IEnumerator BuildAndBakeRoutine(Transform parent, RoomTemplate template, int roomSeed)
    {
        RoomTilesetSO tileset = ResolveTileset(template);
        if (tileset == null)
            yield break;

        Room room = null;
        bool buildFailed = false;
        Transform enemiesRoot = null;
        Random.State previousRandomState = Random.state;

        try
        {
            // ===== TU CÓDIGO ORIGINAL (SIN CAMBIOS) =====
            Random.InitState(SanitizeSeed(roomSeed));
            room = _generator.Generate(template);
            currentRoom = room;
            currentRoomWorldBounds = CalculateRoomWorldBounds(room);
            AlignTilemapGridToWorldSpace();
            ClearLegacyTilemaps();
            room3DGeometryBuilder?.Rebuild(room, parent);
            _painter.Paint(room);
            BuildWallStructures(room, parent, tileset);
            _spawner.Spawn(room);

            Transform itemsRoot = GetOrCreateChildContainer(parent, "Items");
            enemiesRoot = GetOrCreateChildContainer(parent, "Enemies");
            Transform chestsRoot = GetOrCreateChildContainer(parent, "Chests");
            Transform npcsRoot = GetOrCreateChildContainer(parent, "NPCs");
            

            var context = parent.GetComponent<RoomContext>();
            if (context == null)
                context = parent.gameObject.AddComponent<RoomContext>();

            context.Initialize(itemsRoot);

            ClearContainerChildren(itemsRoot);
            ClearContainerChildren(enemiesRoot);
            ClearContainerChildren(chestsRoot);
            ClearContainerChildren(npcsRoot);
            RebuildDoorStructures(room, parent, tileset);
            SpawnChests(room, chestsRoot);
            SpawnNpcs(room, npcsRoot);
            SpawnItemsInRoom(room, itemsRoot, template);
            SpawnCoinsInRoom(room, itemsRoot, template);
        }
        catch (Exception ex)
        {
            buildFailed = true;
            Debug.LogError(
                $"RoomBuilder: build failed on room {CurrentRoomNumber} (seed {roomSeed}). {ex.Message}\n{ex.StackTrace}",
                this);
        }
        finally
        {
            Random.state = previousRandomState;
        }

        if (buildFailed)
        {
            RaiseSpawnFallback(room);
            yield break;
        }

        if (room == null)
        {
            Debug.LogError($"RoomBuilder: generated room is null on room {CurrentRoomNumber}.", this);
            yield break;
        }

        RestrictAllNotWalkableModifiersToHumanoid();

        // ===== AQUÍ EMPIEZA LO NUEVO =====
        Physics.SyncTransforms();
        Physics2D.SyncTransforms(); // fuerza actualización de transforms
        yield return null;          // espera a que Unity actualice colliders

        List<NavMeshAgent> temporarilyDisabledAgents = DisableAgentsForNavMeshRebuild();
        List<AsyncOperation> buildOperations = new List<AsyncOperation>(4);
        try
        {
            foreach (NavMeshSurface surface in ResolveNavMeshSurfaces())
            {
                if (surface == null)
                    continue;

                AsyncOperation operation = null;
                if (surface.navMeshData == null)
                {
                    surface.BuildNavMesh();
                }
                else
                {
                    operation = surface.UpdateNavMesh(surface.navMeshData);
                }

                if (operation != null)
                    buildOperations.Add(operation);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"RoomBuilder: navmesh rebuild failed on room {CurrentRoomNumber}. {ex.Message}\n{ex.StackTrace}",
                this);
        }

        while (HasPendingNavMeshBuild(buildOperations))
            yield return null;

        RestoreAgentsAfterNavMeshRebuild(temporarilyDisabledAgents);

        // Let agents query freshly baked data on the next frame.
        yield return null;

        try
        {
            if (enemiesRoot == null)
                enemiesRoot = GetOrCreateChildContainer(parent, "Enemies");

            SpawnEnemies(room, enemiesRoot);

            RebindAllAgentsToCurrentNavMesh();
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"RoomBuilder: enemy spawn failed on room {CurrentRoomNumber}. {ex.Message}\n{ex.StackTrace}",
                this);
            enemyCount = 0;
        }

        Vector3 spawnPos = CreateCellPosition(room.SpawnPosition, orthogonalOffset: 0f);

        onRoomSpawnEvent?.Raise(spawnPos);
    }

    private void RaiseSpawnFallback(Room room)
    {
        if (room == null)
            return;

        Vector3 fallbackSpawn = CreateCellPosition(room.SpawnPosition, orthogonalOffset: 0f);
        onRoomSpawnEvent?.Raise(fallbackSpawn);
    }

    private static int GenerateNewRunSeed()
    {
        int seed = Guid.NewGuid().GetHashCode();
        return seed == 0 ? 1 : seed;
    }

    private static int BuildRoomSeed(int baseRunSeed, int roomNumber)
    {
       unchecked
{
    int hash = 17;
    hash = (hash * 31) + SanitizeSeed(baseRunSeed);
    hash = (hash * 31) + Mathf.Max(1, roomNumber);
    hash ^= unchecked((int)0x9e3779b9);
    return SanitizeSeed(hash);
}

    }

    private static int SanitizeSeed(int seed)
    {
        return seed == 0    ? 1 : seed;
    }

    private Room CreatePrototypeRoom()
    {
        int totalWidth = Mathf.Max(3, prototypeWalkableSize.x);
        int totalHeight = Mathf.Max(3, prototypeWalkableSize.y);
        int wallThickness = Mathf.Max(1, prototypeWallThickness);
        Room room = new Room(totalWidth, totalHeight);

        for (int x = 0; x < totalWidth; x++)
        {
            for (int y = 0; y < totalHeight; y++)
            {
                bool isWall =
                    x < wallThickness ||
                    y < wallThickness ||
                    x >= totalWidth - wallThickness ||
                    y >= totalHeight - wallThickness;

                room.Grid[x, y] = isWall ? CellType.Wall : CellType.Floor;
            }
        }

        room.SpawnPosition = new Vector2Int(totalWidth / 2, totalHeight / 2);
        return room;
    }

    private RoomTemplate ResolvePrototypeTemplate()
    {
        if (roomSequenceConfig != null && roomSequenceConfig.roomSequence != null)
        {
            for (int i = 0; i < roomSequenceConfig.roomSequence.Count; i++)
            {
                RoomTemplate template = roomSequenceConfig.roomSequence[i];
                if (template != null)
                    return template;
            }
        }

        return null;
    }

    private void ConfigurePrototypeWorldOrigin(Room room)
    {
        if (worldSpaceSettings == null || room == null)
            return;

        float halfWidth = room.Width * worldSpaceSettings.CellSize * 0.5f;
        float halfHeight = room.Height * worldSpaceSettings.CellSize * 0.5f;
        Vector3 centeredOrigin = new Vector3(-halfWidth, 0f, -halfHeight);
        worldSpaceSettings.ConfigureProjection(RoomProjectionMode.XZ, centeredOrigin, 0f);
    }

    private void AlignTilemapGridToWorldSpace()
    {
        if (worldSpaceSettings == null)
            return;

        Tilemap referenceTilemap = ResolveCollisionTilemap();
        if (referenceTilemap == null || referenceTilemap.layoutGrid == null)
            return;

        Transform gridTransform = referenceTilemap.layoutGrid.transform;
        if (gridTransform == null)
            return;

        if (worldSpaceSettings.UsesXZPlane)
        {
            Vector3 position = worldSpaceSettings.Origin;
            position.y += 0.01f;
            gridTransform.position = position;
            gridTransform.rotation = Quaternion.Euler(90f, 0f, 0f);
            return;
        }

        gridTransform.position = worldSpaceSettings.Origin;
        gridTransform.rotation = Quaternion.identity;
    }

    private Vector3 ResolvePrototypeSpawnWorldPosition(Room room)
    {
        if (room == null)
            return Vector3.zero;

        if (worldSpaceSettings != null)
            return worldSpaceSettings.GridRectCenterToWorld(room.Width, room.Height);

        return new Vector3(room.Width * 0.5f, 0f, room.Height * 0.5f);
    }

    private void ClearLegacyTilemaps()
    {
        HashSet<Tilemap> unique = new HashSet<Tilemap>();

        if (tilemap != null)
            unique.Add(tilemap);

        if (layeredTilemaps != null)
        {
            foreach (Tilemap configuredTilemap in layeredTilemaps.EnumerateConfiguredTilemaps())
            {
                if (configuredTilemap != null)
                    unique.Add(configuredTilemap);
            }
        }

        foreach (Tilemap configuredTilemap in unique)
            configuredTilemap.ClearAllTiles();
    }

    private void ClearPrototypeRuntimeContainers(Transform parent)
    {
        if (parent == null)
            return;

        ClearContainerChildren(parent.Find("Doors"));
        ClearContainerChildren(parent.Find("Items"));
        ClearContainerChildren(parent.Find("Enemies"));
        ClearContainerChildren(parent.Find("Chests"));
        ClearContainerChildren(parent.Find("NPCs"));
        ClearContainerChildren(parent.Find("SpecialTiles"));
    }

    private NavMeshSurface EnsurePrototypeNavMeshSurface(Transform roomRoot)
    {
        if (roomRoot == null)
            return null;

        NavMeshSurface prototypeSurface = roomRoot.GetComponent<NavMeshSurface>();
        if (prototypeSurface == null)
            prototypeSurface = roomRoot.gameObject.AddComponent<NavMeshSurface>();

        if (TryGetAgentTypeId(HumanoidAgentTypeName, out int humanoidAgentTypeId))
            prototypeSurface.agentTypeID = humanoidAgentTypeId;

        prototypeSurface.collectObjects = CollectObjects.Children;
        prototypeSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        prototypeSurface.layerMask = ~0;
        prototypeSurface.defaultArea = 0;
        prototypeSurface.ignoreNavMeshAgent = true;
        prototypeSurface.ignoreNavMeshObstacle = true;
        prototypeSurface.overrideTileSize = true;
        prototypeSurface.tileSize = 64;
        prototypeSurface.overrideVoxelSize = true;
        prototypeSurface.voxelSize = 0.1f;

        return prototypeSurface;
    }

    private void DisableLegacyNavMeshSurfacesForPrototype(Transform prototypeRoot)
    {
        foreach (NavMeshSurface surface in ResolveNavMeshSurfaces())
        {
            if (surface == null)
                continue;

            bool belongsToPrototypeRoom =
                prototypeRoot != null &&
                (surface.transform == prototypeRoot || surface.transform.IsChildOf(prototypeRoot));

            surface.enabled = belongsToPrototypeRoom;
        }
    }

    private void EnsureNavMeshWallBlockingConfiguration()
    {
        if (Uses3DNavigation())
        {
            Tilemap xzCollisionTilemap = ResolveCollisionTilemap();
            if (xzCollisionTilemap != null)
            {
                if (xzCollisionTilemap.TryGetComponent(out TilemapCollider2D xzTilemapCollider))
                    xzTilemapCollider.enabled = false;
                if (xzCollisionTilemap.TryGetComponent(out CompositeCollider2D xzCompositeCollider))
                    xzCompositeCollider.enabled = false;
                if (xzCollisionTilemap.TryGetComponent(out Rigidbody2D xzRigidbody2D))
                    xzRigidbody2D.simulated = false;
                if (xzCollisionTilemap.TryGetComponent(out NavMeshModifier xzTilemapModifier))
                    xzTilemapModifier.ignoreFromBuild = true;
            }

            roomWallsNavMeshModifier = null;

            foreach (NavMeshSurface surface in ResolveNavMeshSurfaces())
            {
                if (surface == null)
                    continue;

                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.collectObjects = CollectObjects.All;
            }

            return;
        }

        Tilemap navigationTilemap = ResolveCollisionTilemap();
        if (navigationTilemap == null)
            return;

        TilemapCollider2D tilemapCollider = navigationTilemap.GetComponent<TilemapCollider2D>();
        if (tilemapCollider == null)
            tilemapCollider = navigationTilemap.gameObject.AddComponent<TilemapCollider2D>();

        CompositeCollider2D compositeCollider = navigationTilemap.GetComponent<CompositeCollider2D>();
        if (compositeCollider == null)
            compositeCollider = navigationTilemap.gameObject.AddComponent<CompositeCollider2D>();

        Rigidbody2D rigidbody2D = navigationTilemap.GetComponent<Rigidbody2D>();
        if (rigidbody2D == null)
            rigidbody2D = navigationTilemap.gameObject.AddComponent<Rigidbody2D>();

        rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
        rigidbody2D.simulated = true;

        // Keep per-tile wall colliders so NavMesh can carve only wall cells, not the whole room interior.
        tilemapCollider.compositeOperation = Collider2D.CompositeOperation.None;

        NavMeshModifier tilemapModifier = navigationTilemap.GetComponent<NavMeshModifier>();
        if (tilemapModifier == null)
            tilemapModifier = navigationTilemap.gameObject.AddComponent<NavMeshModifier>();

        roomWallsNavMeshModifier = tilemapModifier;
        tilemapModifier.ignoreFromBuild = false;
        tilemapModifier.overrideArea = true;
        tilemapModifier.area = 1; // Not Walkable
        AllowModifierForAllAgents(tilemapModifier);

        foreach (NavMeshSurface surface in ResolveNavMeshSurfaces())
        {
            if (surface == null)
                continue;

            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        }
    }

    private void EnsureNavMeshAgentSurfaces()
    {
        if (!TryGetAgentTypeId(FlyingAgentTypeName, out int flyingAgentTypeId))
            return;

        if (HasSurfaceForAgentType(flyingAgentTypeId))
            return;

        NavMeshSurface templateSurface = ResolveTemplateSurface();
        if (templateSurface == null)
            return;

        NavMeshSurface flyingSurface = CreateSurfaceFromTemplate(templateSurface, flyingAgentTypeId, "FlyingSurface");
        if (flyingSurface == null)
            return;

        if (navMeshSurfaces == null)
            navMeshSurfaces = new List<NavMeshSurface>();

        if (!navMeshSurfaces.Contains(flyingSurface))
            navMeshSurfaces.Add(flyingSurface);
    }

    private bool HasSurfaceForAgentType(int agentTypeId)
    {
        foreach (NavMeshSurface surface in ResolveNavMeshSurfaces())
        {
            if (surface != null && surface.agentTypeID == agentTypeId)
                return true;
        }

        return false;
    }

    private NavMeshSurface ResolveTemplateSurface()
    {
        if (navMeshSurface != null)
            return navMeshSurface;

        if (navMeshSurfaces != null)
        {
            for (int i = 0; i < navMeshSurfaces.Count; i++)
            {
                if (navMeshSurfaces[i] != null)
                    return navMeshSurfaces[i];
            }
        }

        foreach (NavMeshSurface surface in ResolveNavMeshSurfaces())
        {
            if (surface != null)
                return surface;
        }

        return CreateDefaultNavMeshSurface();
    }

    private NavMeshSurface CreateDefaultNavMeshSurface()
    {
        GameObject owner = gameObject;
        NavMeshSurface createdSurface = owner.GetComponent<NavMeshSurface>();
        if (createdSurface == null)
            createdSurface = owner.AddComponent<NavMeshSurface>();

        if (TryGetAgentTypeId(HumanoidAgentTypeName, out int humanoidAgentTypeId))
            createdSurface.agentTypeID = humanoidAgentTypeId;

        createdSurface.collectObjects = CollectObjects.All;
        createdSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        createdSurface.layerMask = ~0;
        createdSurface.defaultArea = 0;
        createdSurface.ignoreNavMeshAgent = true;
        createdSurface.ignoreNavMeshObstacle = true;
        createdSurface.overrideTileSize = true;
        createdSurface.tileSize = 64;
        createdSurface.overrideVoxelSize = true;
        createdSurface.voxelSize = 0.1f;

        if (navMeshSurface == null)
            navMeshSurface = createdSurface;

        return createdSurface;
    }

    private static NavMeshSurface CreateSurfaceFromTemplate(NavMeshSurface template, int agentTypeId, string nameSuffix)
    {
        if (template == null)
            return null;

        Transform templateTransform = template.transform;
        Transform parent = templateTransform.parent;

        string baseName = string.IsNullOrWhiteSpace(template.gameObject.name)
            ? "NavMeshSurface"
            : template.gameObject.name;
        GameObject clone = new GameObject($"{baseName}_{nameSuffix}");

        if (parent != null)
            clone.transform.SetParent(parent, false);

        clone.transform.position = templateTransform.position;
        clone.transform.rotation = templateTransform.rotation;
        clone.transform.localScale = templateTransform.localScale;

        NavMeshSurface createdSurface = clone.AddComponent<NavMeshSurface>();
        CopySurfaceSettings(template, createdSurface);
        createdSurface.agentTypeID = agentTypeId;

        return createdSurface;
    }

    private static void CopySurfaceSettings(NavMeshSurface source, NavMeshSurface destination)
    {
        if (source == null || destination == null)
            return;

        destination.collectObjects = source.collectObjects;
        destination.size = source.size;
        destination.center = source.center;
        destination.layerMask = source.layerMask;
        destination.useGeometry = source.useGeometry;
        destination.defaultArea = source.defaultArea;
        destination.ignoreNavMeshAgent = source.ignoreNavMeshAgent;
        destination.ignoreNavMeshObstacle = source.ignoreNavMeshObstacle;
        destination.overrideTileSize = source.overrideTileSize;
        destination.tileSize = source.tileSize;
        destination.overrideVoxelSize = source.overrideVoxelSize;
        destination.voxelSize = source.voxelSize;
        destination.buildHeightMesh = source.buildHeightMesh;
        destination.minRegionArea = source.minRegionArea;
    }

    private void RestrictWallModifierToHumanoid(NavMeshModifier modifier)
    {
        if (modifier == null || NavMeshModifierAffectedAgentsField == null)
            return;

        if (!TryGetAgentTypeId(HumanoidAgentTypeName, out int humanoidAgentTypeId))
            return;

        List<int> affectedAgents = NavMeshModifierAffectedAgentsField.GetValue(modifier) as List<int>;
        if (affectedAgents == null)
        {
            affectedAgents = new List<int>(1);
            NavMeshModifierAffectedAgentsField.SetValue(modifier, affectedAgents);
        }

        if (affectedAgents.Count == 1 && affectedAgents[0] == humanoidAgentTypeId)
            return;

        affectedAgents.Clear();
        affectedAgents.Add(humanoidAgentTypeId);
    }

    private void AllowModifierForAllAgents(NavMeshModifier modifier)
    {
        if (modifier == null || NavMeshModifierAffectedAgentsField == null)
            return;

        List<int> affectedAgents = NavMeshModifierAffectedAgentsField.GetValue(modifier) as List<int>;
        if (affectedAgents == null)
        {
            affectedAgents = new List<int>(1);
            NavMeshModifierAffectedAgentsField.SetValue(modifier, affectedAgents);
        }

        if (affectedAgents.Count == 1 && affectedAgents[0] == -1)
            return;

        affectedAgents.Clear();
        affectedAgents.Add(-1);
    }

    private void RestrictAllNotWalkableModifiersToHumanoid()
    {
        List<NavMeshModifier> modifiers = NavMeshModifier.activeModifiers;
        if (modifiers == null || modifiers.Count == 0)
            return;

        for (int i = 0; i < modifiers.Count; i++)
        {
            NavMeshModifier modifier = modifiers[i];
            if (modifier == null || !modifier.overrideArea || modifier.area != 1)
                continue;

            if (ReferenceEquals(modifier, roomWallsNavMeshModifier))
                continue;

            RestrictWallModifierToHumanoid(modifier);
        }
    }

    private static bool TryGetAgentTypeId(string agentTypeName, out int agentTypeId)
    {
        agentTypeId = -1;
        if (string.IsNullOrWhiteSpace(agentTypeName))
            return false;

        int settingsCount = NavMesh.GetSettingsCount();
        for (int i = 0; i < settingsCount; i++)
        {
            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(i);
            string currentName = NavMesh.GetSettingsNameFromID(settings.agentTypeID);
            if (!string.Equals(currentName, agentTypeName, StringComparison.OrdinalIgnoreCase))
                continue;

            agentTypeId = settings.agentTypeID;
            return true;
        }

        return false;
    }

    private static bool HasPendingNavMeshBuild(List<AsyncOperation> buildOperations)
    {
        if (buildOperations == null || buildOperations.Count == 0)
            return false;

        for (int i = 0; i < buildOperations.Count; i++)
        {
            AsyncOperation operation = buildOperations[i];
            if (operation != null && !operation.isDone)
                return true;
        }

        return false;
    }

    private static List<NavMeshAgent> DisableAgentsForNavMeshRebuild()
    {
        NavMeshAgent[] agents = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
        var disabledAgents = new List<NavMeshAgent>(agents.Length);

        for (int i = 0; i < agents.Length; i++)
        {
            NavMeshAgent agent = agents[i];
            if (agent == null || !agent.enabled)
                continue;

            agent.enabled = false;
            disabledAgents.Add(agent);
        }

        return disabledAgents;
    }

    private static void RestoreAgentsAfterNavMeshRebuild(List<NavMeshAgent> disabledAgents)
    {
        if (disabledAgents == null || disabledAgents.Count == 0)
            return;

        for (int i = 0; i < disabledAgents.Count; i++)
        {
            NavMeshAgent agent = disabledAgents[i];
            if (agent == null || agent.enabled)
                continue;

            agent.enabled = true;
        }
    }

    private static void RebindAllAgentsToCurrentNavMesh()
    {
        NavMeshAgent[] agents = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
        for (int i = 0; i < agents.Length; i++)
        {
            TryRebindAgent(agents[i]);
        }
    }

    private static bool TryRebindAgent(NavMeshAgent agent)
    {
        if (agent == null || !agent.enabled)
            return false;

        if (agent.isOnNavMesh)
            return true;

        var filter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = NavMesh.AllAreas
        };

        if (!NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit, AgentRebindMaxDistance, filter))
            return false;

        if (!TryRepositionAgent(agent, hit.position))
            return false;

        agent.nextPosition = hit.position;
        agent.ResetPath();
        return true;
    }

    private static bool TryRepositionAgent(NavMeshAgent agent, Vector3 destination)
    {
        if (agent == null || !agent.enabled)
            return false;

        if (agent.Warp(destination))
            return true;

        agent.enabled = false;
        agent.transform.position = destination;
        agent.enabled = true;

        if (agent.isOnNavMesh)
            return true;

        return agent.Warp(destination);
    }

    private Transform GetOrCreateChildContainer(Transform parent, string containerName)
    {
        Transform existing = parent.Find(containerName);
        if (existing != null)
            return existing;

        GameObject go = new GameObject(containerName);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private Tilemap ResolveCollisionTilemap()
    {
        if (layeredTilemaps != null && layeredTilemaps.CollisionTilemap != null)
            return layeredTilemaps.CollisionTilemap;

        return tilemap;
    }

    private Tilemap ResolveWorldPositionTilemap()
    {
        if (layeredTilemaps != null)
        {
            if (layeredTilemaps.FloorTilemap != null)
                return layeredTilemaps.FloorTilemap;

            if (layeredTilemaps.BoundsTilemap != null)
                return layeredTilemaps.BoundsTilemap;
        }

        return ResolveCollisionTilemap();
    }

    private bool Uses3DNavigation()
    {
        return worldSpaceSettings != null && worldSpaceSettings.UsesXZPlane;
    }

    private float ResolveRoomWallHeight()
    {
        if (roomWallStructureBuilder != null)
            return roomWallStructureBuilder.WallHeight;

        if (room3DGeometryBuilder != null)
            return room3DGeometryBuilder.WallHeight;

        return 2f;
    }

    private Bounds CalculateRoomWorldBounds(Room room)
    {
        if (room == null)
            return default;

        if (worldSpaceSettings != null)
        {
            Vector3 center = worldSpaceSettings.GridRectCenterToWorld(
                room.Width,
                room.Height,
                Uses3DNavigation()
                    ? ResolveRoomWallHeight() * 0.5f
                    : 0f);

            Vector3 size = Uses3DNavigation()
                ? new Vector3(
                    room.Width * worldSpaceSettings.CellSize,
                    Mathf.Max(1f, ResolveRoomWallHeight()),
                    room.Height * worldSpaceSettings.CellSize)
                : new Vector3(
                    room.Width * worldSpaceSettings.CellSize,
                    room.Height * worldSpaceSettings.CellSize,
                    1f);

            return new Bounds(center, size);
        }

        return new Bounds(
            new Vector3(room.Width * 0.5f, room.Height * 0.5f, 0f),
            new Vector3(room.Width, room.Height, 1f));
    }

    private Vector3 CreateCellPosition(
        Vector2Int tilePos,
        float offsetX = 0.5f,
        float offsetY = 0.5f,
        float orthogonalOffset = DefaultSpawnedContentHeightOffset)
    {
        Tilemap worldTilemap = ResolveWorldPositionTilemap();
        if (Uses3DNavigation() && worldTilemap != null && worldTilemap.layoutGrid != null)
        {
            Vector3 cellSize = worldTilemap.layoutGrid.cellSize;
            Transform gridTransform = worldTilemap.layoutGrid.transform;
            Vector3 localPoint = new Vector3(
                (tilePos.x + offsetX) * cellSize.x,
                (tilePos.y + offsetY) * cellSize.y,
                0f);
            Vector3 worldPoint = gridTransform.TransformPoint(localPoint);
            if (worldSpaceSettings != null)
                worldPoint = worldSpaceSettings.ClampToWalkPlane(worldPoint, orthogonalOffset);
            return worldPoint;
        }

        if (worldSpaceSettings != null)
            return worldSpaceSettings.GridToWorld(tilePos, offsetX, offsetY, orthogonalOffset);

        return new Vector3(tilePos.x + offsetX, tilePos.y + offsetY, orthogonalOffset);
    }

    private RoomTilesetSO ResolveTileset(RoomTemplate template)
    {
        if (template == null)
            return null;

        if (template.Tileset != null)
            return template.Tileset;

        Debug.LogError($"RoomBuilder: template '{template.name}' has no RoomTilesetSO assigned.", template);
        return null;
    }

    private void BuildWallStructures(Room room, Transform parent, RoomTilesetSO tileset)
    {
        if (roomWallStructureBuilder == null)
            return;

        roomWallStructureBuilder.Rebuild(room, parent, tileset);
    }

    private void ConfigureSpawnedInstance(GameObject instance, Room2_5DRenderPreset preset)
    {
        Room2_5DPresentationUtility.EnsureDepthSorting(instance, preset);
    }

    private void SpawnItemsInRoom(Room room, Transform parent, RoomTemplate template)
    {
        if (template.itemsToSpawn <= 0)
            return;

        List<Vector2Int> freeTiles = GetAllFreeFloorTiles(room);
        if (freeTiles.Count == 0)
            return;

        Shuffle(freeTiles);

        ItemCatalog catalog = GameReferences.Instance.ItemCatalog;
        LootStatsProvider provider = PactManager.Instance != null ? PactManager.Instance.Loot : null;
        WorldItemSpawner defaultSpawner = new WorldItemSpawner(worldItemPrefab);

        int spawnCount = template.itemsToSpawn;
        if (provider != null)
        {
            float countMultiplier = provider.Get(LootStatType.DropCountMultiplier, 1f, template.lootTags);
            spawnCount = Mathf.Max(0, Mathf.FloorToInt(spawnCount * countMultiplier));
        }

        spawnCount = Mathf.Min(spawnCount, freeTiles.Count);
        if (spawnCount <= 0)
            return;

        int spawned = 0;
        for (int i = 0; i < freeTiles.Count && spawned < spawnCount; i++)
        {
            Vector2Int tilePos = freeTiles[i];
            if (!_tileOccupancy.TryOccupy(tilePos))
                continue;

            Vector3 worldPos = CreateCellPosition(tilePos);
            if (template.itemSpawns != null && template.itemSpawns.Count > 0)
            {
                RoomItemSpawnEntry entry = template.itemSpawns[Random.Range(0, template.itemSpawns.Count)];
                if (entry == null || entry.itemData == null)
                    continue;

                int amount = Random.Range(entry.minAmount, entry.maxAmount + 1);
                amount = ResolveLootAmount(entry.itemData, amount, provider, BuildLootTags(template, entry.itemData));
                if (amount <= 0)
                    continue;

                GameObject prefab = entry.prefabOverride != null ? entry.prefabOverride : worldItemPrefab;
                new WorldItemSpawner(prefab).SpawnItem(entry.itemData, amount, worldPos, parent);
                spawned++;
                continue;
            }

            // Selección basada en rareza
            ItemRaritySO chosenRarity = RaritySelector.PickRarity(
                template.itemRarities,
                provider,
                template.lootTags);
            if (chosenRarity == null)
                continue;

            if (!catalog.TryGetRandom(chosenRarity, out ItemDataSO chosenItem))
                continue;

            int resolvedAmount = ResolveLootAmount(chosenItem, 1, provider, BuildLootTags(template, chosenItem));
            if (resolvedAmount <= 0)
                continue;

            defaultSpawner.SpawnItem(chosenItem, resolvedAmount, worldPos, parent);
            spawned++;
        }
    }

    private void SpawnCoinsInRoom(Room room, Transform parent, RoomTemplate template)
    {
        if (template.coinsToSpawn <= 0)
            return;

        if (template.coinItemData == null || template.coinPrefab == null)
            return;

        List<Vector2Int> freeTiles = GetAllFreeFloorTiles(room);
        if (freeTiles.Count == 0)
            return;

        Shuffle(freeTiles);

        LootStatsProvider provider = PactManager.Instance != null ? PactManager.Instance.Loot : null;
        WorldItemSpawner spawner = new(template.coinPrefab);

        int spawned = 0;
        for (int i = 0; i < freeTiles.Count && spawned < template.coinsToSpawn; i++)
        {
            Vector2Int tilePos = freeTiles[i];

            if (!_tileOccupancy.TryOccupy(tilePos))
                continue;

            int amount = Random.Range(template.minCoinsPerSpawn, template.maxCoinsPerSpawn + 1);
            amount = ResolveLootAmount(template.coinItemData, amount, provider, BuildLootTags(template, template.coinItemData));
            if (amount <= 0)
                continue;

            Vector3 worldPos = CreateCellPosition(tilePos);
            spawner.SpawnItem(template.coinItemData, amount, worldPos, parent);
            spawned++;
        }
    }

    private void RebuildDoorStructures(Room room, Transform roomRoot, RoomTilesetSO tileset)
    {
        if (roomDoorStructureBuilder == null)
            return;

        roomDoorStructureBuilder.Rebuild(room, roomRoot, tileset);
    }

    private static void ClearContainerChildren(Transform parent)
    {
        if (parent == null)
            return;

        foreach (Transform child in parent)
            Destroy(child.gameObject);
    }

    private List<Vector2Int> GetAllFreeFloorTiles(Room room)
    {
        List<Vector2Int> result = new();

        for (int x = 1; x < room.Width - 1; x++)
        {
            for (int y = 1; y < room.Height - 1; y++)
            {
                if (room.Grid[x, y] == CellType.Floor)
                    result.Add(new Vector2Int(x, y));
            }
        }

        return result;
    }

    private void Shuffle(List<Vector2Int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = UnityEngine.Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private IReadOnlyList<GameplayTag> BuildLootTags(RoomTemplate template, ItemDataSO itemData)
    {
        bool hasBaseTags = template != null && template.lootTags != null && template.lootTags.Count > 0;
        GameplayTag typeTag = itemData != null && itemData.IsCurrency ? template.coinTag : template.itemTag;

        if (!hasBaseTags && typeTag == null)
            return null;

        lootTagBuffer.Clear();

        if (hasBaseTags)
            lootTagBuffer.AddRange(template.lootTags);

        if (typeTag != null)
            lootTagBuffer.Add(typeTag);

        return lootTagBuffer;
    }

    private static int ResolveLootAmount(
        ItemDataSO itemData,
        int baseAmount,
        LootStatsProvider provider,
        IReadOnlyList<GameplayTag> tags)
    {
        if (provider == null || itemData == null)
            return baseAmount;

        if (itemData.IsCurrency)
        {
            float coinMultiplier = provider.Get(LootStatType.CoinValueMultiplier, 1f, tags);
            return Mathf.Max(0, Mathf.RoundToInt(baseAmount * coinMultiplier));
        }

        float countMultiplier = provider.Get(LootStatType.DropCountMultiplier, 1f, tags);
        return Mathf.Max(0, Mathf.FloorToInt(baseAmount * countMultiplier));
    }

    private void SpawnEnemies(Room room, Transform parent)
    {
        if (room.EnemySpawns == null || room.EnemySpawns.Count == 0)
        {
            enemyCount = 0;
            return;
        }

        enemyCount = 0;

        foreach (var enemyData in room.EnemySpawns)
        {
            Vector2Int tilePos = enemyData.Position;

            if (!_tileOccupancy.TryOccupy(tilePos))
                continue;

            if (enemyData.Prefab == null)
                continue;

            Vector3 worldPos = CreateCellPosition(tilePos);

            IReadOnlyList<GameplayTag> effectiveTags = ResolveEnemyEffectiveTags(enemyData);
            int preferredAgentTypeId = ResolveEnemyPreferredAgentTypeId(enemyData, effectiveTags);
            Vector3 spawnPosition = ResolveEnemySpawnPosition(worldPos, preferredAgentTypeId);

            GameObject instance = UnityEngine.Object.
            Instantiate(
                enemyData.Prefab,
                spawnPosition,
                Quaternion.identity,
                parent);
            CharacterCombat3DUtility.EnsureHurtbox(instance, ResolveEnemyHurtboxOffset(effectiveTags));
            EnsureEnemyShadow(instance);
            ConfigureSpawnedInstance(instance, Room2_5DRenderPreset.Prop);

            EnemyMovementAgentTypeMapper.Apply(instance, effectiveTags);
            if (instance.TryGetComponent(out NavMeshAgent agent))
            {
                agent.baseOffset = ResolveEnemyBaseOffset(effectiveTags);
                TryRebindAgent(agent);
            }
            else
            {
                ApplyEnemyTransformOffset(instance.transform, effectiveTags);
            }

            if (instance.TryGetComponent(out EnemyStatsApplier applier))
            {
                applier.ApplyTags(effectiveTags);
                applier.Apply();
            }

            enemyCount++;
        }
    }

    private Vector3 ResolveEnemySpawnPosition(Vector3 desiredPosition, int agentTypeId)
    {
        if (agentTypeId < 0)
            return desiredPosition;

        var filter = new NavMeshQueryFilter
        {
            agentTypeID = agentTypeId,
            areaMask = NavMesh.AllAreas
        };

        if (!NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, AgentRebindMaxDistance, filter))
            return desiredPosition;

        return hit.position;
    }

    private int ResolveEnemyPreferredAgentTypeId(EnemySpawnData enemyData, IReadOnlyList<GameplayTag> tags)
    {
        if (TryResolveEnemyAgentTypeId(tags, out int resolvedByTag))
            return resolvedByTag;

        if (enemyData.Prefab != null && enemyData.Prefab.TryGetComponent(out NavMeshAgent prefabAgent))
            return prefabAgent.agentTypeID;

        return -1;
    }

    private bool TryResolveEnemyAgentTypeId(IReadOnlyList<GameplayTag> tags, out int agentTypeId)
    {
        bool isFlying = HasTagNamed(tags, "Flying") || HasTagNamed(tags, "Floating");
        string agentTypeName = isFlying ? FlyingAgentTypeName : HumanoidAgentTypeName;
        return TryGetAgentTypeId(agentTypeName, out agentTypeId);
    }

    private float ResolveEnemyBaseOffset(IReadOnlyList<GameplayTag> tags)
    {
        return HasTagNamed(tags, "Flying") || HasTagNamed(tags, "Floating")
            ? flyingEnemyBaseOffset
            : groundedEnemyBaseOffset;
    }

    private void ApplyEnemyTransformOffset(Transform enemyTransform, IReadOnlyList<GameplayTag> tags)
    {
        if (enemyTransform == null)
            return;

        float offset = ResolveEnemyBaseOffset(tags);
        if (Mathf.Approximately(offset, 0f))
            return;

        Vector3 position = enemyTransform.position;
        position.y += offset;
        enemyTransform.position = position;
    }

    private float ResolveEnemyHurtboxOffset(IReadOnlyList<GameplayTag> tags)
    {
        return HasTagNamed(tags, "Flying") || HasTagNamed(tags, "Floating")
            ? flyingEnemyHurtboxOffset
            : groundedEnemyHurtboxOffset;
    }

    private void EnsureEnemyShadow(GameObject instance)
    {
        if (instance == null)
            return;

        BlobShadowProjector shadowProjector = instance.GetComponent<BlobShadowProjector>();
        if (shadowProjector == null)
            shadowProjector = instance.AddComponent<BlobShadowProjector>();

        shadowProjector.ConfigureRuntime(
            enemyShadowAlpha,
            enemyShadowDiameter,
            enemyShadowTrackedHeight,
            enemyShadowGroundOffset,
            enemyShadowPlanarOffsetTowardsCamera);
    }

    private static IReadOnlyList<GameplayTag> ResolveEnemyEffectiveTags(EnemySpawnData enemyData)
    {
        IReadOnlyList<GameplayTag> baseTags = enemyData.Tags;
        PactManager manager = PactManager.Instance;
        if (manager == null || manager.EnemyStats == null)
            return baseTags;

        return manager.EnemyStats.GetEffectiveTags(baseTags);
    }

    private static bool HasTagNamed(IReadOnlyList<GameplayTag> tags, string expectedTag)
    {
        if (tags == null || tags.Count == 0 || string.IsNullOrWhiteSpace(expectedTag))
            return false;

        string expectedWithSuffix = $"{expectedTag}Tag";

        for (int i = 0; i < tags.Count; i++)
        {
            GameplayTag tag = tags[i];
            if (tag == null)
                continue;

            if (string.Equals(tag.TagName, expectedTag, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag.TagName, expectedWithSuffix, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag.name, expectedTag, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag.name, expectedWithSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }


    private void SpawnChests(Room room, Transform parent)
    {
        if (room.ChestSpawns == null || room.ChestSpawns.Count == 0)
            return;

        foreach (var chestData in room.ChestSpawns)
        {
            Vector2Int tilePos = chestData.Position;

            if (!_tileOccupancy.TryOccupy(tilePos))
                continue;

            Vector3 worldPos = CreateCellPosition(tilePos);

            GameObject chestInstance = Instantiate(
                chestData.Prefab,
                worldPos,
                Quaternion.identity,
                parent);
            ConfigureSpawnedInstance(chestInstance, Room2_5DRenderPreset.Prop);

            if (chestData.LootTableOverride != null &&
                chestInstance.TryGetComponent<ChestInteractable>(out var chestInteractable))
            {
                chestInteractable.SetLootTable(chestData.LootTableOverride);
            }
        }
    }


    private void SpawnNpcs(Room room, Transform parent)
    {
        if (room.NpcSpawns == null || room.NpcSpawns.Count == 0)
            return;

        for (int spawnIndex = 0; spawnIndex < room.NpcSpawns.Count; spawnIndex++)
        {
            NpcSpawnData npcData = room.NpcSpawns[spawnIndex];
            if (npcData.Prefab == null)
                continue;

            if (!_tileOccupancy.TryOccupy(npcData.Position))
                continue;

            Vector3 worldPos = CreateCellPosition(npcData.Position);

            GameObject instance = Instantiate(
                npcData.Prefab,
                worldPos,
                Quaternion.identity,
                parent);

            ApplyNpcDefinition(instance, npcData.Definition);
            ConfigureSpawnedInstance(instance, Room2_5DRenderPreset.Character);
        }
    }

    private static void ApplyNpcDefinition(GameObject instance, NpcDefinitionSO definition)
    {
        if (instance == null || definition == null)
            return;

        if (definition.InGameSprite != null)
        {
            SpriteRenderer renderer = instance.GetComponentInChildren<SpriteRenderer>();
            if (renderer != null)
                renderer.sprite = definition.InGameSprite;
        }

        if (instance.TryGetComponent(out PactNpc pactNpc))
            pactNpc.ApplyRuntimeDefinition(definition);
    }



    //Events
    private void OnEnable()
    {
        enemyDeathEvent.RegisterListener(OnEnemyDeath);
    }

    private void OnDisable()
    {
        enemyDeathEvent.UnregisterListener(OnEnemyDeath);
    }

    private void OnEnemyDeath(GameObject enemy)
    {
        OnDeath();
    }

    public void OnDeath()
    {
        enemyCount--;
        if (enemyCount <= 0)
        {
            roomClearedEvent.Raise();
            roomClearedEvent.RaiseClip(doorClip);
        }

    }

    private IEnumerable<NavMeshSurface> ResolveNavMeshSurfaces()
    {
        HashSet<NavMeshSurface> unique = new HashSet<NavMeshSurface>();

        if (navMeshSurface != null)
            unique.Add(navMeshSurface);

        if (navMeshSurfaces != null)
        {
            for (int i = 0; i < navMeshSurfaces.Count; i++)
            {
                NavMeshSurface surface = navMeshSurfaces[i];
                if (surface != null)
                    unique.Add(surface);
            }
        }

        NavMeshSurface[] discovered = FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        for (int i = 0; i < discovered.Length; i++)
        {
            if (discovered[i] != null)
                unique.Add(discovered[i]);
        }

        return unique;
    }
}

public sealed class TileOccupancy
{
    private readonly HashSet<Vector2Int> _occupied = new();

    public bool IsFree(Vector2Int pos) => !_occupied.Contains(pos);

    public bool TryOccupy(Vector2Int pos)
    {
        if (_occupied.Contains(pos))
            return false;

        _occupied.Add(pos);
        return true;
    }
}
