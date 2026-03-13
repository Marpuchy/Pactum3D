using System;
using System.Collections;
using System.Collections.Generic;
using NavMeshPlus.Components;
using NavMeshPlus.Extensions;
using SaveSystem;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Tilemaps;
using Random = UnityEngine.Random;

public class RoomBuilder : MonoBehaviour
{
    private const string HumanoidAgentTypeName = "Humanoid";
    private const string FlyingAgentTypeName = "Flying";
    private const float AgentRebindMaxDistance = 128f;

    private static readonly FieldInfo NavMeshModifierAffectedAgentsField =
        typeof(NavMeshModifier).GetField("m_AffectedAgents", BindingFlags.Instance | BindingFlags.NonPublic);

    public static RoomBuilder Current { get; private set; }

    [SerializeField] private Tilemap tilemap;
    [SerializeField] private GameObject doorPrefab;
    [SerializeField] private RoomSpawnEvent onRoomSpawnEvent;
    [SerializeField] private EnemyDeathEvent enemyDeathEvent;
    [SerializeField] private RoomClearedEvent roomClearedEvent;
    [SerializeField] private NavMeshSurface navMeshSurface;
    [SerializeField] private List<NavMeshSurface> navMeshSurfaces = new List<NavMeshSurface>();
    [SerializeField] private RoomTemplateSequenceSO roomSequenceConfig;

    [SerializeField] private AudioClip doorClip;

    [Header("Items")]
    [SerializeField] private GameObject worldItemPrefab;

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

    public int CurrentRoomNumber => _templateSelector != null ? _templateSelector.CurrentRoomNumber : 0;
    public int CurrentRunSeed => runSeed;
    public int CurrentRoomSeed => currentRoomSeed;
    public bool IsCurrentNpcRoom => isCurrentNpcRoom;
    public RoomSpawnEvent RoomSpawnEvent => onRoomSpawnEvent;

    private void Awake()
    {
        Current = this;
        EnsureNavMeshAgentSurfaces();
        EnsureNavMeshWallBlockingConfiguration();
        _generator = new IsaacRoomGenerator();
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
        EnsureNavMeshAgentSurfaces();
        EnsureNavMeshWallBlockingConfiguration();

        RoomTemplate template = _templateSelector.GetNextTemplate();
        if (template == null)
        {
            Debug.LogError($"RoomBuilder: selected template is null for room {CurrentRoomNumber}.", this);
            return;
        }

        isCurrentNpcRoom = roomSequenceConfig != null &&
                           roomSequenceConfig.npcRoomTemplate != null &&
                           template == roomSequenceConfig.npcRoomTemplate;
        currentRoomSeed = BuildRoomSeed(runSeed, CurrentRoomNumber);
        
        _tileOccupancy = new TileOccupancy();
        _painter = new TilemapPainter(tilemap, template);
        _spawner = new SpecialTileSpawner(template);

        StartCoroutine(BuildAndBakeRoutine(parent, template, currentRoomSeed));
    }

    private IEnumerator BuildAndBakeRoutine(Transform parent, RoomTemplate template, int roomSeed)
    {
        Room room = null;
        bool buildFailed = false;
        Transform enemiesRoot = null;
        Random.State previousRandomState = Random.state;

        try
        {
            // ===== TU CÓDIGO ORIGINAL (SIN CAMBIOS) =====
            Random.InitState(SanitizeSeed(roomSeed));
            room = _generator.Generate(template);
            _painter.Paint(room);
            _spawner.Spawn(room);

            Transform doorsRoot = GetOrCreateChildContainer(parent, "Doors");
            Transform itemsRoot = GetOrCreateChildContainer(parent, "Items");
            enemiesRoot = GetOrCreateChildContainer(parent, "Enemies");
            Transform chestsRoot = GetOrCreateChildContainer(parent, "Chests");
            Transform npcsRoot = GetOrCreateChildContainer(parent, "NPCs");
            

            var context = parent.GetComponent<RoomContext>();
            if (context == null)
                context = parent.gameObject.AddComponent<RoomContext>();

            context.Initialize(itemsRoot);


            DeleteOldDoors(doorsRoot);
            SpawnDoors(room, doorsRoot, template);
            SpawnItemsInRoom(room, itemsRoot, template);
            SpawnCoinsInRoom(room, itemsRoot, template);
            SpawnChests(room, chestsRoot);
            SpawnNpcs(room, npcsRoot);
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

                AsyncOperation operation = surface.BuildNavMeshAsync();
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

        Vector3 spawnPos = new Vector3(
            room.SpawnPosition.x + 0.5f,
            room.SpawnPosition.y + 0.5f,
            0f
        );

        onRoomSpawnEvent?.Raise(spawnPos);
    }

    private void RaiseSpawnFallback(Room room)
    {
        if (room == null)
            return;

        Vector3 fallbackSpawn = new Vector3(
            room.SpawnPosition.x + 0.5f,
            room.SpawnPosition.y + 0.5f,
            0f);
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

    private void EnsureNavMeshWallBlockingConfiguration()
    {
        if (tilemap == null)
            return;

        TilemapCollider2D tilemapCollider = tilemap.GetComponent<TilemapCollider2D>();
        if (tilemapCollider == null)
            tilemapCollider = tilemap.gameObject.AddComponent<TilemapCollider2D>();

        CompositeCollider2D compositeCollider = tilemap.GetComponent<CompositeCollider2D>();
        if (compositeCollider == null)
            compositeCollider = tilemap.gameObject.AddComponent<CompositeCollider2D>();

        Rigidbody2D rigidbody2D = tilemap.GetComponent<Rigidbody2D>();
        if (rigidbody2D == null)
            rigidbody2D = tilemap.gameObject.AddComponent<Rigidbody2D>();

        rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
        rigidbody2D.simulated = true;

        // Keep per-tile wall colliders so NavMesh can carve only wall cells, not the whole room interior.
        tilemapCollider.compositeOperation = Collider2D.CompositeOperation.None;

        NavMeshModifier tilemapModifier = tilemap.GetComponent<NavMeshModifier>();
        if (tilemapModifier == null)
            tilemapModifier = tilemap.gameObject.AddComponent<NavMeshModifier>();

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

        return null;
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

        CollectSources2d templateCollectSources = template.GetComponent<CollectSources2d>();
        if (templateCollectSources != null)
        {
            CollectSources2d createdCollectSources = clone.AddComponent<CollectSources2d>();
            CopyCollectSourcesSettings(templateCollectSources, createdCollectSources);
        }

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
        destination.hideEditorLogs = source.hideEditorLogs;
    }

    private static void CopyCollectSourcesSettings(CollectSources2d source, CollectSources2d destination)
    {
        if (source == null || destination == null)
            return;

        destination.overrideByGrid = source.overrideByGrid;
        destination.useMeshPrefab = source.useMeshPrefab;
        destination.compressBounds = source.compressBounds;
        destination.overrideVector = source.overrideVector;
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
        NavMeshAgent[] agents = FindObjectsOfType<NavMeshAgent>();
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
        NavMeshAgent[] agents = FindObjectsOfType<NavMeshAgent>();
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

        if (!agent.Warp(hit.position))
            return false;

        agent.nextPosition = hit.position;
        agent.ResetPath();
        return true;
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

    private void SpawnItemsInRoom(Room room, Transform parent, RoomTemplate template)
    {
        if (template.itemsToSpawn <= 0) return;

        List<Vector2Int> freeTiles = GetAllFreeFloorTiles(room);
        Shuffle(freeTiles);

        WorldItemSpawner spawner = new(worldItemPrefab);
        ItemCatalog catalog = GameReferences.Instance.ItemCatalog;
        LootStatsProvider provider = PactManager.Instance != null ? PactManager.Instance.Loot : null;

        int spawned = 0;

        foreach (var tilePos in freeTiles)
        {
            if (spawned >= template.itemsToSpawn)
                break;

        int spawnCount = template.itemsToSpawn;
        if (provider != null)
        {
            float countMultiplier = provider.Get(LootStatType.DropCountMultiplier, 1f, template.lootTags);
            spawnCount = Mathf.Max(0, Mathf.FloorToInt(spawnCount * countMultiplier));
        }

        spawnCount = Mathf.Min(spawnCount, freeTiles.Count);
            if (!_tileOccupancy.TryOccupy(tilePos))
                continue;

            Vector3 worldPos = new(tilePos.x + 0.5f, tilePos.y + 0.5f, 0f);

            ItemRaritySO rarity = RaritySelector.PickRarity(template.itemRarities);
            if (rarity == null) continue;

            if (!catalog.TryGetRandom(rarity, out ItemDataSO item))
            // Primero check de override
            if (template.itemSpawns != null && template.itemSpawns.Count > 0)
            {
                RoomItemSpawnEntry entry = template.itemSpawns[Random.Range(0, template.itemSpawns.Count)];
                int amount = Random.Range(entry.minAmount, entry.maxAmount + 1);
                amount = ResolveLootAmount(entry.itemData, amount, provider, BuildLootTags(template, entry.itemData));
                GameObject prefab = entry.prefabOverride != null ? entry.prefabOverride : worldItemPrefab;
                new WorldItemSpawner(prefab).SpawnItem(entry.itemData, amount, worldPos, parent);
                continue;
            }

            // Selección basada en rareza
            ItemRaritySO chosenRarity = RaritySelector.PickRarity(
                template.itemRarities,
                provider,
                template.lootTags);
            if (chosenRarity == null) continue;

            if (!catalog.TryGetRandom(chosenRarity, out ItemDataSO chosenItem)) continue;

            spawned++;
            int resolvedAmount = ResolveLootAmount(chosenItem, 1, provider, BuildLootTags(template, chosenItem));
            if (resolvedAmount <= 0)
                continue;

            spawner.SpawnItem(chosenItem, resolvedAmount, worldPos, parent);
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

            Vector3 worldPos = new(tilePos.x + 0.5f, tilePos.y + 0.5f, 0f);
            spawner.SpawnItem(template.coinItemData, amount, worldPos, parent);
            spawned++;
        }
    }

    private void SpawnDoors(Room room, Transform parent, RoomTemplate template)
    {
        foreach (var door in room.Doors)
        {
            Vector3 pos = new(door.Position.x + 0.513f, door.Position.y + 0.502f, 0);
            GameObject instance = Instantiate(doorPrefab, pos, Quaternion.identity, parent);

            DoorController controller = instance.GetComponent<DoorController>();
            if (controller != null)
            {
                controller.ApplySpriteSets(
                    template.doorUp,
                    template.doorDown,
                    template.doorLeft,
                    template.doorRight);
            }

            DoorView view = instance.GetComponent<DoorView>();
            if (view != null)
                view.Init(door);
        }
    }

    private void DeleteOldDoors(Transform parent)
    {
        foreach (Transform child in parent)
        {
            if (child.CompareTag("Door"))
                Destroy(child.gameObject);
        }
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

            Vector3 worldPos = new(
                tilePos.x + 0.5f,
                tilePos.y + 0.5f,
                0f);

            IReadOnlyList<GameplayTag> effectiveTags = ResolveEnemyEffectiveTags(enemyData);
            int preferredAgentTypeId = ResolveEnemyPreferredAgentTypeId(enemyData, effectiveTags);
            Vector3 spawnPosition = ResolveEnemySpawnPosition(worldPos, preferredAgentTypeId);

            GameObject instance = UnityEngine.Object.
            Instantiate(
                enemyData.Prefab,
                spawnPosition,
                Quaternion.identity,
                parent);

            EnemyMovementAgentTypeMapper.Apply(instance, effectiveTags);
            if (instance.TryGetComponent(out NavMeshAgent agent))
                TryRebindAgent(agent);

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

            Vector3 worldPos = new(
                tilePos.x + 0.5f,
                tilePos.y + 0.5f,
                0f);

            GameObject chestInstance = Instantiate(
                chestData.Prefab,
                worldPos,
                Quaternion.identity,
                parent);

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

            Vector3 worldPos = new(
                npcData.Position.x + 0.5f,
                npcData.Position.y + 0.5f,
                0f);

            GameObject instance = Instantiate(
                npcData.Prefab,
                worldPos,
                Quaternion.identity,
                parent);

            ApplyNpcDefinition(instance, npcData.Definition);
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

        NavMeshSurface[] discovered = FindObjectsOfType<NavMeshSurface>();
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
