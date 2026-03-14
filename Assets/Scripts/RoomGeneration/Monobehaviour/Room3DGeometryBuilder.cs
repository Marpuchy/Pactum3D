using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[AddComponentMenu("Room/3D Geometry Builder")]
public sealed class Room3DGeometryBuilder : MonoBehaviour
{
    private const string GeometryRootName = "Geometry3D";
    private const float DefaultInvisibleBlockerThickness = 1f;

    [SerializeField] private RoomWorldSpaceSettings worldSpaceSettings;
    [SerializeField] private Material floorMaterial;
    [SerializeField] private Material wallMaterial;
    [SerializeField] private bool buildOnlyForXZProjection = true;
    [SerializeField] private bool buildVisibleFloor = false;
    [SerializeField] private bool buildVisibleWalls = false;
    [SerializeField] private bool generateColliders = true;
    [SerializeField, Min(0.01f)] private float floorThickness = 0.2f;
    [SerializeField, Min(0f)] private float visibleFloorSink = 0.08f;
    [SerializeField, Min(0.01f)] private float walkSurfaceThickness = 0.1f;
    [SerializeField, Min(0.01f)] private float wallVisualHeight = 0.08f;
    [SerializeField, Min(0.1f)] private float wallBlockerHeight = 2.5f;
    [SerializeField] private int geometryLayer;
    [SerializeField] private Color fallbackFloorColor = new Color(0.42f, 0.35f, 0.24f, 1f);
    [SerializeField] private Color fallbackWallColor = new Color(0.24f, 0.22f, 0.18f, 1f);

    private Material runtimeFloorMaterial;
    private Material runtimeWallMaterial;

    public float WallHeight => wallBlockerHeight;

    public void Rebuild(Room room, Transform roomRoot)
    {
        if (room == null || roomRoot == null)
            return;

        if (worldSpaceSettings == null)
            worldSpaceSettings = GetComponent<RoomWorldSpaceSettings>();

        Transform geometryRoot = GetOrCreateGeometryRoot(roomRoot);
        ClearChildren(geometryRoot);

        if (worldSpaceSettings == null)
            return;

        if (buildOnlyForXZProjection && !worldSpaceSettings.UsesXZPlane)
            return;

        if (buildVisibleFloor)
            BuildVisibleFloor(room, geometryRoot);

        BuildWalkSurface(room, geometryRoot);
        BuildWalls(room, geometryRoot);
    }

    private void BuildVisibleFloor(Room room, Transform parent)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor3D";
        floor.layer = geometryLayer;
        floor.transform.SetParent(parent, false);
        floor.transform.position = worldSpaceSettings.GridRectCenterToWorld(
            room.Width,
            room.Height,
            -visibleFloorSink - (floorThickness * 0.5f));
        floor.transform.localScale = new Vector3(
            room.Width * worldSpaceSettings.CellSize,
            floorThickness,
            room.Height * worldSpaceSettings.CellSize);

        ApplyMaterial(floor, ResolveFloorMaterial());

        Collider visibleFloorCollider = floor.GetComponent<Collider>();
        if (visibleFloorCollider != null)
            Destroy(visibleFloorCollider);
    }

    private void BuildWalls(Room room, Transform parent)
    {
        Transform wallsRoot = new GameObject("Walls3D").transform;
        wallsRoot.SetParent(parent, false);

        for (int x = 0; x < room.Width; x++)
        {
            for (int y = 0; y < room.Height; y++)
            {
                if (room.Grid[x, y] != CellType.Wall)
                    continue;

                Vector2Int cell = new Vector2Int(x, y);
                if (buildVisibleWalls)
                    BuildVisibleWall(cell, x, y, wallsRoot);

                BuildWallBlocker(cell, x, y, wallsRoot);
            }
        }
    }

    private void BuildWalkSurface(Room room, Transform parent)
    {
        if (!generateColliders || walkSurfaceThickness <= 0.001f)
            return;

        GameObject walkSurface = new GameObject("WalkSurface");
        walkSurface.layer = geometryLayer;
        walkSurface.transform.SetParent(parent, false);
        walkSurface.transform.position = worldSpaceSettings.GridRectCenterToWorld(
            room.Width,
            room.Height,
            -(walkSurfaceThickness * 0.5f));

        BoxCollider boxCollider = walkSurface.AddComponent<BoxCollider>();
        boxCollider.size = new Vector3(
            room.Width * worldSpaceSettings.CellSize,
            walkSurfaceThickness,
            room.Height * worldSpaceSettings.CellSize);
    }

    private void BuildVisibleWall(Vector2Int cell, int x, int y, Transform parent)
    {
        if (wallVisualHeight <= 0.001f)
            return;

        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = $"Wall_{x}_{y}";
        wall.layer = geometryLayer;
        wall.transform.SetParent(parent, false);
        wall.transform.position = worldSpaceSettings.GridToWorld(
            cell,
            orthogonalOffset: wallVisualHeight * 0.5f);
        wall.transform.localScale = new Vector3(
            worldSpaceSettings.CellSize,
            wallVisualHeight,
            worldSpaceSettings.CellSize);

        ApplyMaterial(wall, ResolveWallMaterial());

        Collider wallCollider = wall.GetComponent<Collider>();
        if (wallCollider != null)
            Destroy(wallCollider);
    }

    private void BuildWallBlocker(Vector2Int cell, int x, int y, Transform parent)
    {
        if (!generateColliders || wallBlockerHeight <= 0.01f)
            return;

        GameObject blocker = new GameObject($"WallBlocker_{x}_{y}");
        blocker.layer = geometryLayer;
        blocker.transform.SetParent(parent, false);
        blocker.transform.position = worldSpaceSettings.GridToWorld(
            cell,
            orthogonalOffset: wallBlockerHeight * 0.5f);

        BoxCollider boxCollider = blocker.AddComponent<BoxCollider>();
        boxCollider.size = new Vector3(
            worldSpaceSettings.CellSize,
            wallBlockerHeight,
            DefaultInvisibleBlockerThickness * worldSpaceSettings.CellSize);
    }

    private static Transform GetOrCreateGeometryRoot(Transform roomRoot)
    {
        Transform existing = roomRoot.Find(GeometryRootName);
        if (existing != null)
            return existing;

        GameObject root = new GameObject(GeometryRootName);
        root.transform.SetParent(roomRoot, false);
        return root.transform;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.Destroy(parent.GetChild(i).gameObject);
    }

    private void OnDestroy()
    {
        DestroyRuntimeMaterial(runtimeFloorMaterial);
        DestroyRuntimeMaterial(runtimeWallMaterial);
    }

    private Material ResolveFloorMaterial()
    {
        return ResolveMaterial(floorMaterial, fallbackFloorColor, ref runtimeFloorMaterial, "RoomFloorRuntimeMaterial");
    }

    private Material ResolveWallMaterial()
    {
        return ResolveMaterial(wallMaterial, fallbackWallColor, ref runtimeWallMaterial, "RoomWallRuntimeMaterial");
    }

    private static Material ResolveMaterial(
        Material assignedMaterial,
        Color fallbackColor,
        ref Material runtimeMaterial,
        string runtimeName)
    {
        if (assignedMaterial != null)
            return assignedMaterial;

        if (runtimeMaterial != null)
            return runtimeMaterial;

        Shader shader =
            Shader.Find("Universal Render Pipeline/Lit") ??
            Shader.Find("Standard") ??
            Shader.Find("Diffuse");

        if (shader == null)
            return null;

        runtimeMaterial = new Material(shader)
        {
            name = runtimeName
        };

        if (runtimeMaterial.HasProperty("_BaseColor"))
            runtimeMaterial.SetColor("_BaseColor", fallbackColor);
        if (runtimeMaterial.HasProperty("_Color"))
            runtimeMaterial.SetColor("_Color", fallbackColor);
        if (runtimeMaterial.HasProperty("_Smoothness"))
            runtimeMaterial.SetFloat("_Smoothness", 0f);
        if (runtimeMaterial.HasProperty("_Metallic"))
            runtimeMaterial.SetFloat("_Metallic", 0f);

        return runtimeMaterial;
    }

    private static void ApplyMaterial(GameObject instance, Material material)
    {
        if (material == null)
            return;

        if (instance.TryGetComponent(out MeshRenderer renderer))
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private static void DestroyRuntimeMaterial(Material material)
    {
        if (material == null)
            return;

        if (Application.isPlaying)
            Destroy(material);
        else
            DestroyImmediate(material);
    }
}
