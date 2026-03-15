using UnityEngine;
using UnityEngine.Rendering;

public enum Room2_5DRenderPreset
{
    Character,
    Door,
    Item,
    Prop,
    GroundProp
}

public static class Room2_5DPresentationUtility
{
    private const int DefaultOrdersPerUnit = 16;
    private const int CharacterSortingBase = 1000;
    private const int DoorSortingBase = 1100;
    private const int ItemSortingBase = 900;
    private const int PropSortingBase = 950;
    private const int GroundPropSortingBase = 850;
    private static Material runtimeXZSpriteMaterial;

    public static SpriteDepthSorter EnsureDepthSorting(
        GameObject instance,
        Room2_5DRenderPreset preset,
        bool overrideExisting = false)
    {
        if (instance == null || !HasSpritePresentation(instance))
            return null;

        SpriteDepthSorter sorter = instance.GetComponent<SpriteDepthSorter>();
        bool created = false;

        if (sorter == null)
        {
            sorter = instance.AddComponent<SpriteDepthSorter>();
            created = true;
        }

        if (created || overrideExisting)
        {
            sorter.Configure(
                ResolveSortingOffset(preset),
                DefaultOrdersPerUnit);
        }
        else
        {
            sorter.RefreshRenderTargets();
            sorter.ApplySorting();
        }

        EnsureXZSpriteRendering(instance);
        EnsureBillboardIfNeeded(instance, preset);
        return sorter;
    }

    private static bool HasSpritePresentation(GameObject instance)
    {
        return instance.GetComponentInChildren<SpriteRenderer>(true) != null ||
               instance.GetComponentInChildren<SortingGroup>(true) != null;
    }

    private static int ResolveSortingOffset(Room2_5DRenderPreset preset)
    {
        switch (preset)
        {
            case Room2_5DRenderPreset.Door:
                return DoorSortingBase;
            case Room2_5DRenderPreset.Item:
                return ItemSortingBase;
            case Room2_5DRenderPreset.Prop:
                return PropSortingBase;
            case Room2_5DRenderPreset.GroundProp:
                return GroundPropSortingBase;
            default:
                return CharacterSortingBase;
        }
    }

    private static void EnsureBillboardIfNeeded(GameObject instance, Room2_5DRenderPreset preset)
    {
        RoomWorldSpaceSettings worldSpace = RoomWorldSpaceSettings.Current;
        if (instance == null || worldSpace == null || !worldSpace.UsesXZPlane)
            return;

        BillboardFacingCamera billboard = instance.GetComponent<BillboardFacingCamera>();
        bool shouldBillboard = preset == Room2_5DRenderPreset.Character;

        if (!shouldBillboard)
        {
            if (billboard != null)
                billboard.enabled = false;

            return;
        }

        if (billboard == null)
            billboard = instance.AddComponent<BillboardFacingCamera>();

        billboard.enabled = true;
    }

    private static void EnsureXZSpriteRendering(GameObject instance)
    {
        RoomWorldSpaceSettings worldSpace = RoomWorldSpaceSettings.Current;
        if (instance == null || worldSpace == null || !worldSpace.UsesXZPlane)
            return;

        SpriteRenderer[] renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers == null || renderers.Length == 0)
            return;

        Material spriteMaterial = ResolveXZSpriteMaterial();
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (spriteMaterial != null)
                renderer.sharedMaterial = spriteMaterial;

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.allowOcclusionWhenDynamic = false;
        }
    }

    private static Material ResolveXZSpriteMaterial()
    {
        if (runtimeXZSpriteMaterial != null)
            return runtimeXZSpriteMaterial;

        Shader shader =
            Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ??
            Shader.Find("Sprites/Default");

        if (shader == null)
            return null;

        runtimeXZSpriteMaterial = new Material(shader)
        {
            name = "RuntimeXZSpriteUnlit"
        };

        return runtimeXZSpriteMaterial;
    }
}
