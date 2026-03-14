using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Rooms/Room Template")]
public class RoomTemplate : ScriptableObject
{
    [Header("Size")]
    public Vector2Int minSize;
    public Vector2Int maxSize;

    [Header("Doors")]
    public int minDoors;
    public int maxDoors;
    
    [Header("Door Sprites (Per Direction)")]
    public DoorSpriteSet doorUp;
    public DoorSpriteSet doorDown;
    public DoorSpriteSet doorLeft;
    public DoorSpriteSet doorRight;

    [Header("Items")]
    public int itemsToSpawn = 0;
    public List<RoomItemRarityEntry> itemRarities = new();

    [Header("Coins")]
    public int coinsToSpawn = 0;
    public ItemDataSO coinItemData;
    public GameObject coinPrefab;
    public int minCoinsPerSpawn = 1;
    public int maxCoinsPerSpawn = 1;
    
    [Header("Chest")]
    public int chestToSpawn = 0;
    public GameObject chestPrefab;
    public List<RoomChestRarityEntry> chestRarities = new();
    
    [Header("NPCs - Pact")]
    [FormerlySerializedAs("npcSpawns")]
    public List<NpcSpawnEntry> pactNpcSpawns = new();

    [Tooltip("If true, the first pact NPC always spawns and one extra pact NPC can spawn optionally.")]
    [FormerlySerializedAs("alwaysSpawnFirstNpc")]
    public bool alwaysSpawnFirstPactNpc = false;

    [Range(0f, 1f)]
    [Tooltip("Chance to spawn one extra pact NPC from the remaining list when alwaysSpawnFirstPactNpc is enabled.")]
    [FormerlySerializedAs("optionalNpcSpawnChance")]
    public float optionalPactNpcSpawnChance = 0.5f;

    [Header("NPCs - Others")]
    public List<NpcSpawnEntry> otherNpcSpawns = new();

    [Range(0f, 1f)]
    [Tooltip("Chance to spawn one extra non-pact NPC.")]
    public float otherNpcSpawnChance = 0f;


    [Tooltip("Configuración avanzada para spawns con prefab específico")]
    public List<RoomItemSpawnEntry> itemSpawns = new List<RoomItemSpawnEntry>();

    [Header("Enemies")]
    public int minEnemies;
    public int maxEnemies;
    public List<EnemySpawnEntry> enemySpawns = new();


    [Header("Base Tiles")] 
    public TileBase spawnPointTile;
    public TileBase floorTile;
    [Tooltip("Fallback wall tile if a side/corner tile is not set")]
    public TileBase wallTile;

    [Header("Wall Tiles (Per Side/Corner)")]
    public TileBase wallTop;
    public TileBase wallBottom;
    public TileBase wallLeft;
    public TileBase wallRight;
    public TileBase wallTopLeft;
    public TileBase wallTopRight;
    public TileBase wallBottomLeft;
    public TileBase wallBottomRight;

    [Header("Special Tile Spawning")]
    [Range(0f, 1f)]
    public float specialTilePercentage = 0.33f;

    public List<SpecialTileConfig> specialTiles;

    [Header("Pact Tags")]
    public List<GameplayTag> roomTags = new();
    public List<GameplayTag> lootTags = new();
    public GameplayTag itemTag;
    public GameplayTag coinTag;

}

[System.Serializable]
public class SpecialTileConfig
{
    public CellType type;
    public TileBase specialTileTile;
    public GameObject specialTilePrefab;

    [Range(0f, 1f)]
    [Tooltip("Relative probability of this special tile appearing within the overall percentage of special tiles")]
    public float spawnPercentage = 0.5f;

    public List<GameplayTag> tags = new();
}

[System.Serializable]
public class RoomItemSpawnEntry
{
    public ItemDataSO itemData;
    public GameObject prefabOverride;
    public int minAmount = 1;
    public int maxAmount = 1;
}

[System.Serializable]
public class EnemySpawnEntry
{
    [SerializeField] private EnemyDefinitionSO enemyDefinition;
    [SerializeField, HideInInspector, FormerlySerializedAs("enemyPrefab")]
    private GameObject legacyEnemyPrefab;

    [Range(0f, 1f)]
    public float spawnWeight = 0.25f;

    public int minAmount = 1;
    public int maxAmount = 1;

    public EnemyDefinitionSO EnemyDefinition => enemyDefinition;
    public GameObject LegacyEnemyPrefab => legacyEnemyPrefab;
    public GameObject Prefab => enemyDefinition != null ? enemyDefinition.Prefab : legacyEnemyPrefab;
    public IReadOnlyList<GameplayTag> Tags => enemyDefinition != null ? enemyDefinition.Tags : null;
    public string DisplayName => enemyDefinition != null ? enemyDefinition.DisplayName : (legacyEnemyPrefab != null ? legacyEnemyPrefab.name : "None");
}

[System.Serializable]
public class RoomItemRarityEntry
{
    public ItemRaritySO rarity;

    [Range(0f, 1f)]
    public float spawnWeight = 0.25f;

    public List<GameplayTag> tags = new();
}

[System.Serializable]
public class RoomChestRarityEntry
{
    public ItemRaritySO rarity;

    [Range(0f, 1f)]
    public float spawnWeight = 0.25f;

    public GameObject prefab;
    public LootTableSO lootTable;

    public List<GameplayTag> tags = new();
}

[System.Serializable]
public class NpcSpawnEntry
{
    [SerializeField] private NpcDefinitionSO npcDefinition;
    [SerializeField, HideInInspector, FormerlySerializedAs("npcPrefab")]
    private GameObject legacyNpcPrefab;

    [Tooltip("Indica si este NPC puede aparecer dentro de la habitación")]
    public bool isInRoom = true;

    [Range(0f, 1f)]
    [Tooltip("Probabilidad relativa de spawn de este NPC")]
    public float spawnRate = 0.25f;

    public NpcDefinitionSO NpcDefinition => npcDefinition;
    public GameObject LegacyNpcPrefab => legacyNpcPrefab;
    public GameObject Prefab => npcDefinition != null && npcDefinition.SharedPrefab != null
        ? npcDefinition.SharedPrefab
        : legacyNpcPrefab;
    public IReadOnlyList<GameplayTag> Tags => npcDefinition != null ? npcDefinition.Tags : null;
    public string DisplayName => npcDefinition != null
        ? npcDefinition.DisplayName
        : (legacyNpcPrefab != null ? legacyNpcPrefab.name : "None");
}

[System.Serializable]
public class DoorSpriteSet
{
    public Sprite closed;
    public Sprite open;
}
