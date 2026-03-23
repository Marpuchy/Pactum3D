using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(menuName = "Rooms/Room Tileset", fileName = "RoomTileset")]
public sealed class RoomTilesetSO : ScriptableObject
{
    private const string DefaultRoomTilesTagName = "roomtilestag";

    [Header("Base Tiles")]
    [SerializeField] private TileBase wallTile;
    [SerializeField] private TileBase floorTile;
    [SerializeField] private TileBase spawnPointTile;

    [Header("Door")]
    [SerializeField] private DoorSpriteSet door = new();
    [SerializeField] private GameObject doorPrefab;

    [Header("Special Tiles")]
    [SerializeField] private List<SpecialTileConfig> specialTiles = new();

    [Header("Tags")]
    [SerializeField] private List<GameplayTag> tags = new();

    public TileBase WallTile => wallTile;
    public TileBase FloorTile => floorTile;
    public TileBase SpawnPointTile => spawnPointTile;
    public DoorSpriteSet Door => door;
    public GameObject DoorPrefab => doorPrefab;
    public List<SpecialTileConfig> SpecialTiles => specialTiles;
    public IReadOnlyList<GameplayTag> Tags => tags;

#if UNITY_EDITOR
    private void OnValidate()
    {
        specialTiles ??= new List<SpecialTileConfig>();
        tags ??= new List<GameplayTag>();

        CleanupNullTags();
        CleanupAndTagSpecialTiles();
        EnsureDefaultRoomTilesTag();
    }

    private void CleanupNullTags()
    {
        for (int i = tags.Count - 1; i >= 0; i--)
        {
            if (tags[i] == null)
                tags.RemoveAt(i);
        }
    }

    private void CleanupAndTagSpecialTiles()
    {
        for (int i = 0; i < specialTiles.Count; i++)
        {
            SpecialTileConfig config = specialTiles[i];
            if (config == null)
                continue;

            config.tags ??= new List<GameplayTag>();
            for (int tagIndex = config.tags.Count - 1; tagIndex >= 0; tagIndex--)
            {
                if (config.tags[tagIndex] == null)
                    config.tags.RemoveAt(tagIndex);
            }

            string defaultTagName = ResolveDefaultSpecialTileTagName(config.type);
            if (string.IsNullOrWhiteSpace(defaultTagName) || ContainsTag(config.tags, defaultTagName))
                continue;

            GameplayTag defaultTag = ResolveDefaultTag(defaultTagName);
            if (defaultTag == null)
                continue;

            config.tags.Insert(0, defaultTag);
            EditorUtility.SetDirty(this);
        }
    }

    private void EnsureDefaultRoomTilesTag()
    {
        if (ContainsTag(DefaultRoomTilesTagName))
            return;

        GameplayTag defaultTag = ResolveDefaultTag(DefaultRoomTilesTagName);
        if (defaultTag == null)
            return;

        tags.Insert(0, defaultTag);
        EditorUtility.SetDirty(this);
    }

    private bool ContainsTag(string expectedTagName)
    {
        return ContainsTag(tags, expectedTagName);
    }

    private static bool ContainsTag(IReadOnlyList<GameplayTag> source, string expectedTagName)
    {
        if (source == null || string.IsNullOrWhiteSpace(expectedTagName))
            return false;

        string expectedWithSuffix = $"{expectedTagName}Tag";
        for (int i = 0; i < source.Count; i++)
        {
            GameplayTag tag = source[i];
            if (tag == null)
                continue;

            if (string.Equals(tag.TagName, expectedTagName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag.TagName, expectedWithSuffix, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag.name, expectedTagName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag.name, expectedWithSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string ResolveDefaultSpecialTileTagName(CellType type)
    {
        return type switch
        {
            CellType.Lava => "lavatiletag",
            CellType.Rock => "rocktiletag",
            CellType.Spike => "spiketiletag",
            CellType.Ice => "icetiletag",
            CellType.Pit => "pittiletag",
            _ => null
        };
    }

    private static GameplayTag ResolveDefaultTag(string defaultTagName)
    {
        string[] guids = AssetDatabase.FindAssets("t:GameplayTag");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameplayTag tag = AssetDatabase.LoadAssetAtPath<GameplayTag>(path);
            if (tag == null)
                continue;

            if (string.Equals(tag.TagName, defaultTagName, StringComparison.OrdinalIgnoreCase))
                return tag;

            if (string.Equals(tag.name, $"{defaultTagName}Tag", StringComparison.OrdinalIgnoreCase))
                return tag;
        }

        return null;
    }
#endif
}

[Serializable]
public class SpecialTileConfig
{
    public CellType type;
    public TileBase specialTileTile;
    public GameObject specialTilePrefab;
    public List<GameplayTag> tags = new();
}

[Serializable]
public class DoorSpriteSet
{
    public Sprite closed;
    public Sprite open;
}
