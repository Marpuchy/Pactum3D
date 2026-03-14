using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Unity.AI.Navigation;

[DisallowMultipleComponent]
public sealed class Room2_5DTilemapLayers : MonoBehaviour
{
    [SerializeField] private Tilemap collisionTilemap;
    [SerializeField] private Tilemap floorTilemap;
    [SerializeField] private Tilemap wallBackTilemap;
    [SerializeField] private Tilemap wallFrontTilemap;
    [SerializeField] private Tilemap boundsTilemap;
    [SerializeField, Min(1)] private int foregroundWallRows = 1;
    [SerializeField] private bool hideCollisionRenderer = true;

    public Tilemap CollisionTilemap => collisionTilemap;
    public Tilemap FloorTilemap => floorTilemap;
    public Tilemap WallBackTilemap => wallBackTilemap;
    public Tilemap WallFrontTilemap => wallFrontTilemap;
    public Tilemap BoundsTilemap => boundsTilemap != null ? boundsTilemap : collisionTilemap;
    public int ForegroundWallRows => Mathf.Max(1, foregroundWallRows);
    public bool HasLayeredVisuals => floorTilemap != null || wallBackTilemap != null || wallFrontTilemap != null;

    private void Awake()
    {
        ApplyRuntimeSetup();
    }

    private void OnValidate()
    {
        foregroundWallRows = Mathf.Max(1, foregroundWallRows);

        if (!Application.isPlaying)
            ApplyRuntimeSetup();
    }

    public void ApplyRuntimeSetup()
    {
        if (hideCollisionRenderer &&
            collisionTilemap != null &&
            collisionTilemap != floorTilemap &&
            collisionTilemap != wallBackTilemap &&
            collisionTilemap != wallFrontTilemap)
        {
            TilemapRenderer collisionRenderer = collisionTilemap.GetComponent<TilemapRenderer>();
            if (collisionRenderer != null)
                collisionRenderer.enabled = false;
        }

        DisablePresentationPhysics(floorTilemap);
        DisablePresentationPhysics(wallBackTilemap);
        DisablePresentationPhysics(wallFrontTilemap);
    }

    public IEnumerable<Tilemap> EnumerateConfiguredTilemaps()
    {
        HashSet<Tilemap> unique = new HashSet<Tilemap>();

        AddIfValid(unique, collisionTilemap);
        AddIfValid(unique, floorTilemap);
        AddIfValid(unique, wallBackTilemap);
        AddIfValid(unique, wallFrontTilemap);
        AddIfValid(unique, boundsTilemap);

        return unique;
    }

    private void DisablePresentationPhysics(Tilemap tilemap)
    {
        if (tilemap == null || tilemap == collisionTilemap)
            return;

        if (tilemap.TryGetComponent(out TilemapCollider2D tilemapCollider))
            tilemapCollider.enabled = false;

        if (tilemap.TryGetComponent(out CompositeCollider2D compositeCollider))
            compositeCollider.enabled = false;

        if (tilemap.TryGetComponent(out Rigidbody2D rigidbody2D))
            rigidbody2D.simulated = false;

        if (tilemap.TryGetComponent(out NavMeshModifier navMeshModifier))
            navMeshModifier.ignoreFromBuild = true;
    }

    private static void AddIfValid(ICollection<Tilemap> target, Tilemap tilemap)
    {
        if (tilemap != null)
            target.Add(tilemap);
    }
}
