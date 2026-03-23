using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class IsaacRoomGenerator : RoomGeneratorBase
{
    private readonly INpcSelector npcSelector;
    private static readonly Dictionary<GameObject, EnemyDefinitionSO> enemyDefinitionsByPrefab =
        new Dictionary<GameObject, EnemyDefinitionSO>();
    private static bool enemyDefinitionCacheInitialized;

    public IsaacRoomGenerator()
    {
    }

    public IsaacRoomGenerator(INpcSelector npcSelector)
    {
        this.npcSelector = npcSelector;
    }

    protected override Room CreateLayout(RoomTemplate config)
    {
        int w = Random.Range(config.minSize.x, config.maxSize.x + 1);
        int h = Random.Range(config.minSize.y, config.maxSize.y + 1);

        Room room = new(w, h);

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                bool border = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                room.Grid[x, y] = border ? CellType.Wall : CellType.Floor;
            }
        }

        return room;
    }

    protected override void PlaceDoors(Room room, RoomTemplate config)
    {
        int doorCount = Random.Range(config.minDoors, config.maxDoors + 1);

        List<DoorDirection> dirs = new()
        {
            DoorDirection.Up,
            DoorDirection.Down,
            DoorDirection.Left,
            DoorDirection.Right
        };

        for (int i = 0; i < doorCount && dirs.Count > 0; i++)
        {
            DoorDirection dir = dirs[Random.Range(0, dirs.Count)];
            Vector2Int pos = GetDoorPosition(room, dir);

            room.Doors.Add(new DoorData(dir, pos));
            room.Grid[pos.x, pos.y] = CellType.Floor;

            dirs.Remove(dir);
        }
    }

    protected override void PlaceSpawnPoint(Room room, RoomTemplate config)
    {
        Vector2Int center = new(room.Width / 2, room.Height / 2);
        Vector2Int finalPos;

        if (room.Grid[center.x, center.y] == CellType.Floor)
        {
            finalPos = center;
        }
        else
        {
            finalPos = GetRandomFreeFloor(room);
        }

        room.Grid[finalPos.x, finalPos.y] = CellType.SpawnPoint;
        room.SpawnPosition = finalPos;
    }

    protected override void PlaceSpawns(Room room, RoomTemplate config)
    {
        List<RuntimeSpecialTileSpawnEntry> validSpecialTiles = CollectValidSpecialTileEntries(config);
        if (validSpecialTiles.Count == 0)
            return;

        List<Vector2Int> freeTiles = GetAllFreeFloorTiles(room);

        if (freeTiles.Count == 0)
            return;

        float density = config.specialTilePercentage;
        RoomStatsProvider roomProvider = PactManager.Instance != null ? PactManager.Instance.Rooms : null;
        IReadOnlyList<GameplayTag> roomTags = ResolveRoomTags(config);
        if (roomProvider != null)
        {
            float densityMultiplier = roomProvider.Get(
                RoomParamType.SpecialTileDensityMultiplier,
                1f,
                roomTags);
            density = Mathf.Clamp01(density * Mathf.Max(0f, densityMultiplier));
        }

        int targetAmount = Mathf.FloorToInt(freeTiles.Count * density);

        Shuffle(freeTiles);

        int placed = 0;
        int placedLava = 0;

        for (int i = 0; i < freeTiles.Count && placed < targetAmount; i++)
        {
            Vector2Int pos = freeTiles[i];

            if (!CanPlaceSpecialTile(room, pos))
                continue;

            RuntimeSpecialTileSpawnEntry configEntry = GetWeightedSpecial(validSpecialTiles, roomProvider, roomTags);
            if (!configEntry.IsValid)
                continue;

            room.Grid[pos.x, pos.y] = configEntry.Type;
            placed++;

            if (configEntry.Type == CellType.Lava)
                placedLava++;
        }

        TryPlaceAdditionalLavaTiles(room, config, freeTiles, placedLava, roomProvider);
    }
    
    private RuntimeSpecialTileSpawnEntry GetWeightedSpecial(
        IReadOnlyList<RuntimeSpecialTileSpawnEntry> list,
        RoomStatsProvider provider,
        IReadOnlyList<GameplayTag> baseTags)
    {
        float total = 0f;
        float[] weights = new float[list.Count];
        List<GameplayTag> tagBuffer = provider != null ? new List<GameplayTag>(8) : null;

        for (int i = 0; i < list.Count; i++)
        {
            RuntimeSpecialTileSpawnEntry entry = list[i];
            if (!entry.IsValid)
                continue;

            float weight = entry.Weight;
            if (provider != null)
            {
                IReadOnlyList<GameplayTag> tags = BuildTags(baseTags, entry.Tags, tagBuffer);
                if (entry.Type == CellType.Lava)
                {
                    float multiplier = provider.Get(RoomParamType.LavaWeightMultiplier, 1f, tags);
                    if (multiplier < 1f)
                        weight *= Mathf.Max(0f, multiplier);
                }
            }

            weights[i] = weight;
            total += weight;
        }

        if (total <= 0f)
            return default;

        float roll = Random.value * total;
        float current = 0f;

        for (int i = 0; i < list.Count; i++)
        {
            if (weights[i] <= 0f || !list[i].IsValid)
                continue;

            current += weights[i];
            if (roll <= current)
                return list[i];
        }

        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i].IsValid && weights[i] > 0f)
                return list[i];
        }

        return default;
    }

    private void TryPlaceAdditionalLavaTiles(
        Room room,
        RoomTemplate config,
        IReadOnlyList<Vector2Int> freeTiles,
        int placedLava,
        RoomStatsProvider provider)
    {
        if (room == null || config == null || freeTiles == null || freeTiles.Count == 0 || provider == null)
            return;

        IReadOnlyList<GameplayTag> roomTags = ResolveRoomTags(config);
        List<RuntimeSpecialTileSpawnEntry> lavaEntries = CollectSpecialTilesByType(
            CollectValidSpecialTileEntries(config),
            CellType.Lava);
        if (lavaEntries.Count == 0)
            return;

        float lavaMultiplier = ResolveHighestLavaMultiplier(lavaEntries, provider, roomTags);
        if (lavaMultiplier <= 1f)
            return;

        int baseLavaCount = placedLava > 0 ? placedLava : 1;
        int desiredLavaCount = ScaleCountByMultiplier(baseLavaCount, lavaMultiplier);
        int extraLavaToPlace = Mathf.Max(0, desiredLavaCount - placedLava);

        if (extraLavaToPlace <= 0)
            return;

        // First convert already-placed non-lava special tiles so high-density rooms can still reach the target.
        for (int i = 0; i < freeTiles.Count && extraLavaToPlace > 0; i++)
        {
            Vector2Int pos = freeTiles[i];
            CellType current = room.Grid[pos.x, pos.y];

            if (current == CellType.Lava ||
                current == CellType.Floor ||
                current == CellType.Wall ||
                current == CellType.SpawnPoint)
            {
                continue;
            }

            room.Grid[pos.x, pos.y] = CellType.Lava;
            extraLavaToPlace--;
        }

        for (int i = 0; i < freeTiles.Count && extraLavaToPlace > 0; i++)
        {
            Vector2Int pos = freeTiles[i];
            if (room.Grid[pos.x, pos.y] != CellType.Floor)
                continue;

            if (!CanPlaceSpecialTile(room, pos))
                continue;

            RuntimeSpecialTileSpawnEntry lavaConfig = GetWeightedSpecial(lavaEntries, provider, roomTags);
            if (!lavaConfig.IsValid)
                continue;

            room.Grid[pos.x, pos.y] = lavaConfig.Type;
            extraLavaToPlace--;
        }
    }

    private static List<RuntimeSpecialTileSpawnEntry> CollectSpecialTilesByType(
        IReadOnlyList<RuntimeSpecialTileSpawnEntry> list,
        CellType type)
    {
        var result = new List<RuntimeSpecialTileSpawnEntry>();
        if (list == null || list.Count == 0)
            return result;

        for (int i = 0; i < list.Count; i++)
        {
            RuntimeSpecialTileSpawnEntry entry = list[i];
            if (entry.IsValid && entry.Type == type)
                result.Add(entry);
        }

        return result;
    }

    private static float ResolveHighestLavaMultiplier(
        IReadOnlyList<RuntimeSpecialTileSpawnEntry> lavaEntries,
        RoomStatsProvider provider,
        IReadOnlyList<GameplayTag> roomTags)
    {
        if (lavaEntries == null || lavaEntries.Count == 0 || provider == null)
            return 1f;

        float highest = 1f;
        var tagBuffer = new List<GameplayTag>(8);
        for (int i = 0; i < lavaEntries.Count; i++)
        {
            RuntimeSpecialTileSpawnEntry entry = lavaEntries[i];
            if (!entry.IsValid)
                continue;

            IReadOnlyList<GameplayTag> tags = BuildTags(roomTags, entry.Tags, tagBuffer);
            float multiplier = provider.Get(RoomParamType.LavaWeightMultiplier, 1f, tags);
            highest = Mathf.Max(highest, Mathf.Max(0f, multiplier));
        }

        return highest;
    }
    
    private void Shuffle(List<Vector2Int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private bool CanPlaceSpecialTile(Room room, Vector2Int pos)
    {
        Vector2Int[] dirs =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        foreach (var dir in dirs)
        {
            Vector2Int n = pos + dir;

            if (n.x <= 0 || n.y <= 0 ||
                n.x >= room.Width - 1 ||
                n.y >= room.Height - 1)
                continue;

            if (room.Grid[n.x, n.y] == CellType.Floor ||
                room.Grid[n.x, n.y] == CellType.SpawnPoint)
                return true;
        }

        return false;
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

    private Vector2Int GetRandomFreeFloor(Room room)
    {
        var free = GetAllFreeFloorTiles(room);

        if (free.Count == 0)
            throw new Exception("No hay tiles Floor disponibles");

        return free[Random.Range(0, free.Count)];
    }

    private Vector2Int GetDoorPosition(Room r, DoorDirection d)
    {
        return d switch
        {
            DoorDirection.Up => new(r.Width / 2, r.Height - 1),
            DoorDirection.Down => new(r.Width / 2, 0),
            DoorDirection.Left => new(0, r.Height / 2),
            _ => new(r.Width - 1, r.Height / 2)
        };
    }

    protected override void PlaceEnemies(Room room, RoomTemplate config)
    {
        if (config.enemySpawns == null || config.enemySpawns.Count == 0)
            return;

        RoomStatsProvider roomProvider = PactManager.Instance != null ? PactManager.Instance.Rooms : null;
        EnemyStatsProvider enemyProvider = PactManager.Instance != null ? PactManager.Instance.EnemyStats : null;

        List<RuntimeEnemySpawnEntry> validEnemyEntries = CollectValidEnemyEntries(config.enemySpawns, enemyProvider);
        if (validEnemyEntries.Count == 0)
            return;

        int totalEnemies =
            Random.Range(config.minEnemies, config.maxEnemies + 1);
        if (roomProvider != null)
        {
            float multiplier = roomProvider.Get(
                RoomParamType.EnemyCountMultiplier,
                1f,
                ResolveRoomTags(config));
            totalEnemies = ScaleCountByMultiplier(totalEnemies, multiplier);
        }

        if (totalEnemies <= 0)
            return;

        List<Vector2Int> freeTiles = GetAllFreeFloorTiles(room);
        Shuffle(freeTiles);

        int placed = 0;

        for (int i = 0; i < freeTiles.Count && placed < totalEnemies; i++)
        {
            Vector2Int pos = freeTiles[i];

            if (room.Grid[pos.x, pos.y] != CellType.Floor)
                continue;

            RuntimeEnemySpawnEntry enemyConfig = GetWeightedEnemy(validEnemyEntries);
            if (!enemyConfig.IsValid)
                continue;

            room.EnemySpawns.Add(new EnemySpawnData(
                enemyConfig.Definition,
                pos,
                enemyConfig.Prefab,
                enemyConfig.Tags,
                enemyConfig.MovementTags));

            placed++;
        }
    }
    
    private RuntimeEnemySpawnEntry GetWeightedEnemy(IReadOnlyList<RuntimeEnemySpawnEntry> list)
    {
        if (list == null || list.Count == 0)
            return default;

        float total = 0f;

        for (int i = 0; i < list.Count; i++)
            total += list[i].Weight;

        if (total <= 0f)
            return default;

        float roll = Random.value * total;
        float current = 0f;

        for (int i = 0; i < list.Count; i++)
        {
            RuntimeEnemySpawnEntry candidate = list[i];
            current += candidate.Weight;
            if (roll <= current)
                return candidate;
        }

        return list[list.Count - 1];
    }

    private static List<RuntimeEnemySpawnEntry> CollectValidEnemyEntries(
        List<EnemySpawnEntry> list,
        EnemyStatsProvider enemyProvider)
    {
        var valid = new List<RuntimeEnemySpawnEntry>();
        if (list == null || list.Count == 0)
            return valid;

        for (int i = 0; i < list.Count; i++)
        {
            EnemySpawnEntry entry = list[i];
            if (entry == null || entry.Prefab == null)
                continue;

            EnemyDefinitionSO definition = ResolveEnemyDefinition(entry);
            IReadOnlyList<GameplayTag> resolvedTags = ResolveEnemySpawnTags(entry, definition);
            IReadOnlyList<GameplayTag> resolvedMovementTags = ResolveEnemyMovementTags(definition);

            float weight = Mathf.Max(0f, entry.spawnWeight);
            if (weight <= 0f)
                continue;

            if (enemyProvider != null)
            {
                IReadOnlyList<GameplayTag> effectiveTags = enemyProvider.GetEffectiveTags(resolvedTags);
                float multiplier = enemyProvider.Get(
                    EnemyStatType.SpawnWeightMultiplier,
                    1f,
                    effectiveTags,
                    definition,
                    entry.Prefab);
                weight *= Mathf.Max(0f, multiplier);
            }

            if (weight <= 0f)
                continue;

            valid.Add(new RuntimeEnemySpawnEntry(
                definition,
                entry.Prefab,
                weight,
                resolvedTags,
                resolvedMovementTags));
        }

        return valid;
    }

    private static int ScaleCountByMultiplier(int baseCount, float multiplier)
    {
        float clampedMultiplier = Mathf.Max(0f, multiplier);
        float scaled = Mathf.Max(0f, baseCount * clampedMultiplier);
        int roundedDown = Mathf.FloorToInt(scaled);
        float fractionalPart = scaled - roundedDown;

        if (fractionalPart > 0f && Random.value < fractionalPart)
            roundedDown++;

        return Mathf.Max(0, roundedDown);
    }

    private static EnemyDefinitionSO ResolveEnemyDefinition(EnemySpawnEntry entry)
    {
        if (entry == null)
            return null;

        if (entry.EnemyDefinition != null)
            return entry.EnemyDefinition;

        if (entry.Prefab == null)
            return null;

        EnsureEnemyDefinitionCache();
        if (enemyDefinitionsByPrefab.TryGetValue(entry.Prefab, out EnemyDefinitionSO definition))
            return definition;

        return null;
    }

    private static IReadOnlyList<GameplayTag> ResolveEnemySpawnTags(
        EnemySpawnEntry entry,
        EnemyDefinitionSO definition)
    {
        if (definition != null && definition.Tags != null && definition.Tags.Count > 0)
            return definition.Tags;

        if (entry != null && entry.Tags != null && entry.Tags.Count > 0)
            return entry.Tags;

        if (entry != null &&
            entry.Prefab != null &&
            entry.Prefab.TryGetComponent(out CharacterStatResolver resolver) &&
            resolver.Tags != null &&
            resolver.Tags.Count > 0)
        {
            return resolver.Tags;
        }

        return null;
    }

    private static IReadOnlyList<GameplayTag> ResolveEnemyMovementTags(EnemyDefinitionSO definition)
    {
        if (definition != null && definition.MovementTags != null && definition.MovementTags.Count > 0)
            return definition.MovementTags;

        return null;
    }

    private static void EnsureEnemyDefinitionCache()
    {
        if (enemyDefinitionCacheInitialized)
            return;

        enemyDefinitionCacheInitialized = true;
        enemyDefinitionsByPrefab.Clear();

        EnemyDefinitionSO[] definitions = Resources.FindObjectsOfTypeAll<EnemyDefinitionSO>();
        for (int i = 0; i < definitions.Length; i++)
        {
            EnemyDefinitionSO definition = definitions[i];
            if (definition == null || definition.Prefab == null)
                continue;

            enemyDefinitionsByPrefab[definition.Prefab] = definition;
        }
    }

    private readonly struct RuntimeEnemySpawnEntry
    {
        public RuntimeEnemySpawnEntry(
            EnemyDefinitionSO definition,
            GameObject prefab,
            float weight,
            IReadOnlyList<GameplayTag> tags,
            IReadOnlyList<GameplayTag> movementTags)
        {
            Definition = definition;
            Prefab = prefab;
            Weight = weight;
            Tags = tags;
            MovementTags = movementTags;
        }

        public EnemyDefinitionSO Definition { get; }
        public GameObject Prefab { get; }
        public float Weight { get; }
        public IReadOnlyList<GameplayTag> Tags { get; }
        public IReadOnlyList<GameplayTag> MovementTags { get; }
        public bool IsValid => Prefab != null;
    }

    private readonly struct RuntimeSpecialTileSpawnEntry
    {
        public RuntimeSpecialTileSpawnEntry(SpecialTileConfig definition, float weight)
        {
            Definition = definition;
            Weight = weight;
        }

        public SpecialTileConfig Definition { get; }
        public float Weight { get; }
        public CellType Type => Definition != null ? Definition.type : CellType.Floor;
        public IReadOnlyList<GameplayTag> Tags => Definition != null ? Definition.tags : null;
        public bool IsValid => Definition != null && Weight > 0f;
    }
    
    protected override void PlaceChests(Room room, RoomTemplate config)
    {
        if (config.chestToSpawn <= 0)
            return;

        bool hasRarities = config.chestRarities != null && config.chestRarities.Count > 0;

        if (!hasRarities && config.chestPrefab == null)
            return;

        List<Vector2Int> freeTiles = GetAllFreeFloorTiles(room);
        if (freeTiles.Count == 0)
            return;

        Shuffle(freeTiles);

        int placed = 0;
        LootStatsProvider lootProvider = PactManager.Instance != null ? PactManager.Instance.Loot : null;

        for (int i = 0; i < freeTiles.Count && placed < config.chestToSpawn; i++)
        {
            Vector2Int pos = freeTiles[i];

            if (room.Grid[pos.x, pos.y] != CellType.Floor)
                continue;

            GameObject prefab = config.chestPrefab;
            LootTableSO lootTable = null;

            if (hasRarities)
            {
                var entry = RaritySelector.PickChestEntry(
                    config.chestRarities,
                    lootProvider,
                    config.lootTags);
                if (entry == null)
                    continue;

                if (entry.prefab != null)
                    prefab = entry.prefab;

                lootTable = entry.lootTable;
            }

            if (prefab == null)
                continue;

            room.ChestSpawns.Add(new ChestSpawnData(
                prefab,
                pos,
                lootTable
            ));

            placed++;
        }
    }

    protected override void PlaceNpcs(Room room, RoomTemplate config)
    {
        List<Vector2Int> freeTiles = GetAllFreeFloorTiles(room);
        if (freeTiles.Count == 0)
            return;

        Shuffle(freeTiles);

        int nextFreeTileIndex = 0;
        nextFreeTileIndex = SpawnPactNpcs(room, config, freeTiles, nextFreeTileIndex);
        SpawnOtherNpc(room, config, freeTiles, nextFreeTileIndex);
    }

    private int SpawnPactNpcs(
        Room room,
        RoomTemplate config,
        IReadOnlyList<Vector2Int> freeTiles,
        int startTileIndex)
    {
        if (config.pactNpcSpawns == null || config.pactNpcSpawns.Count == 0)
            return startTileIndex;

        if (startTileIndex >= freeTiles.Count)
            return startTileIndex;

        int tileIndex = startTileIndex;

        if (config.alwaysSpawnFirstPactNpc)
        {
            bool spawnedPrimary = false;
            NpcSpawnEntry firstEntry = config.pactNpcSpawns[0];

            if (IsNpcEntryValid(firstEntry) && IsNpcEligible(firstEntry))
            {
                room.NpcSpawns.Add(new NpcSpawnData(
                    firstEntry.NpcDefinition,
                    freeTiles[tileIndex],
                    firstEntry.LegacyNpcPrefab
                ));
                spawnedPrimary = true;
                tileIndex++;
            }

            List<NpcSpawnEntry> optionalEntries = new();
            for (int i = 1; i < config.pactNpcSpawns.Count; i++)
            {
                NpcSpawnEntry entry = config.pactNpcSpawns[i];
                if (IsNpcEntryValid(entry) && IsNpcEligible(entry))
                    optionalEntries.Add(entry);
            }

            if (spawnedPrimary)
            {
                if (tileIndex < freeTiles.Count && optionalEntries.Count > 0)
                {
                    float optionalChance = Mathf.Clamp01(config.optionalPactNpcSpawnChance);
                    if (Random.value <= optionalChance)
                    {
                        NpcSpawnEntry optionalNpc = SelectNpc(optionalEntries);
                        if (optionalNpc != null)
                        {
                            room.NpcSpawns.Add(new NpcSpawnData(
                                optionalNpc.NpcDefinition,
                                freeTiles[tileIndex],
                                optionalNpc.LegacyNpcPrefab
                            ));
                            tileIndex++;
                        }
                    }
                }

                return tileIndex;
            }

            List<NpcSpawnEntry> fallbackEntries = config.pactNpcSpawns
                .FindAll(entry => IsNpcEntryValid(entry) && IsNpcEligible(entry));

            NpcSpawnEntry guaranteedNpc = SelectNpc(fallbackEntries);
            if (guaranteedNpc != null)
            {
                room.NpcSpawns.Add(new NpcSpawnData(
                    guaranteedNpc.NpcDefinition,
                    freeTiles[tileIndex],
                    guaranteedNpc.LegacyNpcPrefab
                ));
                tileIndex++;
            }

            return tileIndex;
        }

        List<NpcSpawnEntry> validPactNpcs = config.pactNpcSpawns
            .FindAll(entry => IsNpcEntryValid(entry) && IsNpcEligible(entry));

        NpcSpawnEntry selectedPactNpc = SelectNpc(validPactNpcs);
        if (selectedPactNpc == null)
            return tileIndex;

        room.NpcSpawns.Add(new NpcSpawnData(
            selectedPactNpc.NpcDefinition,
            freeTiles[tileIndex],
            selectedPactNpc.LegacyNpcPrefab
        ));

        return tileIndex + 1;
    }

    private void SpawnOtherNpc(
        Room room,
        RoomTemplate config,
        IReadOnlyList<Vector2Int> freeTiles,
        int startTileIndex)
    {
        if (startTileIndex >= freeTiles.Count)
            return;

        if (config.otherNpcSpawns == null || config.otherNpcSpawns.Count == 0)
            return;

        float spawnChance = Mathf.Clamp01(config.otherNpcSpawnChance);
        if (Random.value > spawnChance)
            return;

        List<NpcSpawnEntry> validOtherNpcs = config.otherNpcSpawns
            .FindAll(IsNpcEntryValid);

        // "Other" NPCs (e.g. merchant) should not be filtered by pact run locks.
        // They use their own weighted selection independent of pact NPC eligibility.
        NpcSpawnEntry selectedOtherNpc = GetWeightedNpcFallback(validOtherNpcs);
        if (selectedOtherNpc == null)
            return;

        room.NpcSpawns.Add(new NpcSpawnData(
            selectedOtherNpc.NpcDefinition,
            freeTiles[startTileIndex],
            selectedOtherNpc.LegacyNpcPrefab
        ));
    }

    
    private static bool IsNpcEntryValid(NpcSpawnEntry entry)
    {
        return entry != null && entry.isInRoom && entry.Prefab != null;
    }

    private bool IsNpcEligible(NpcSpawnEntry entry)
    {
        return npcSelector == null || npcSelector.IsEntryEligible(entry);
    }

    private NpcSpawnEntry SelectNpc(IReadOnlyList<NpcSpawnEntry> list)
    {
        if (list == null || list.Count == 0)
            return null;

        if (npcSelector != null)
            return npcSelector.SelectNpc(list);

        return GetWeightedNpcFallback(list);
    }

    private NpcSpawnEntry GetWeightedNpcFallback(IReadOnlyList<NpcSpawnEntry> list)
    {
        float total = 0f;

        for (int i = 0; i < list.Count; i++)
        {
            NpcSpawnEntry entry = list[i];
            if (entry == null)
                continue;

            total += entry.spawnRate;
        }

        if (total <= 0f)
        {
            int randomIndex = Random.Range(0, list.Count);
            return list[randomIndex];
        }

        float roll = Random.value * total;
        float current = 0f;

        for (int i = 0; i < list.Count; i++)
        {
            NpcSpawnEntry entry = list[i];
            if (entry == null)
                continue;

            current += entry.spawnRate;
            if (roll <= current)
                return entry;
        }

        return list[0];
    }

    private static IReadOnlyList<GameplayTag> BuildTags(
        IReadOnlyList<GameplayTag> baseTags,
        IReadOnlyList<GameplayTag> entryTags,
        List<GameplayTag> buffer)
    {
        bool hasBase = baseTags != null && baseTags.Count > 0;
        bool hasEntry = entryTags != null && entryTags.Count > 0;

        if (!hasBase && !hasEntry)
            return null;

        buffer.Clear();

        if (hasBase)
            buffer.AddRange(baseTags);

        if (hasEntry)
            buffer.AddRange(entryTags);

        return buffer;
    }

    private static RoomTilesetSO ResolveTileset(RoomTemplate config)
    {
        return config != null ? config.Tileset : null;
    }

    private static IReadOnlyList<GameplayTag> ResolveRoomTags(RoomTemplate config)
    {
        RoomTilesetSO tileset = ResolveTileset(config);
        return tileset != null ? tileset.Tags : null;
    }

    private static List<RuntimeSpecialTileSpawnEntry> CollectValidSpecialTileEntries(RoomTemplate config)
    {
        var valid = new List<RuntimeSpecialTileSpawnEntry>();
        RoomTilesetSO tileset = ResolveTileset(config);
        if (config == null || tileset == null || config.specialTileSpawns == null || config.specialTileSpawns.Count == 0)
            return valid;

        for (int i = 0; i < config.specialTileSpawns.Count; i++)
        {
            RoomSpecialTileSpawnEntry spawnEntry = config.specialTileSpawns[i];
            if (spawnEntry == null || spawnEntry.spawnWeight <= 0f || spawnEntry.TileTag == null)
                continue;

            List<SpecialTileConfig> matchingDefinitions = CollectSpecialTileDefinitionsByTag(tileset.SpecialTiles, spawnEntry.TileTag);
            if (matchingDefinitions.Count == 0)
                continue;

            float weightPerDefinition = spawnEntry.spawnWeight / matchingDefinitions.Count;
            for (int matchIndex = 0; matchIndex < matchingDefinitions.Count; matchIndex++)
                valid.Add(new RuntimeSpecialTileSpawnEntry(matchingDefinitions[matchIndex], weightPerDefinition));
        }

        return valid;
    }

    private static List<SpecialTileConfig> CollectSpecialTileDefinitionsByTag(
        IReadOnlyList<SpecialTileConfig> definitions,
        GameplayTag expectedTag)
    {
        var result = new List<SpecialTileConfig>();
        if (definitions == null || expectedTag == null)
            return result;

        for (int i = 0; i < definitions.Count; i++)
        {
            SpecialTileConfig definition = definitions[i];
            if (definition == null || !ContainsGameplayTag(definition.tags, expectedTag))
                continue;

            result.Add(definition);
        }

        return result;
    }

    private static bool ContainsGameplayTag(IReadOnlyList<GameplayTag> tags, GameplayTag expectedTag)
    {
        if (tags == null || expectedTag == null)
            return false;

        string expectedName = expectedTag.TagName;
        for (int i = 0; i < tags.Count; i++)
        {
            GameplayTag candidate = tags[i];
            if (candidate == null)
                continue;

            if (candidate == expectedTag ||
                string.Equals(candidate.TagName, expectedName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }





}
