using UnityEngine;
using UnityEngine.Rendering;

public enum Room2_5DRenderPreset
{
    Character,
    Door,
    Wall,
    Item,
    Prop,
    GroundProp,
    Shadow
}

public static class Room2_5DPresentationUtility
{
    private const int DefaultOrdersPerUnit = 16;
    private const int CharacterSortingBase = 1000;
    private const int WallSortingBase = 1000;
    private const int DoorSortingBase = 1000;
    private const int ItemSortingBase = 1000;
    private const int PropSortingBase = 1000;
    private const int GroundPropSortingBase = 850;
    private const int ShadowSortingBase = 900;
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
            case Room2_5DRenderPreset.Wall:
                return WallSortingBase;
            case Room2_5DRenderPreset.Item:
                return ItemSortingBase;
            case Room2_5DRenderPreset.Prop:
                return PropSortingBase;
            case Room2_5DRenderPreset.GroundProp:
                return GroundPropSortingBase;
            case Room2_5DRenderPreset.Shadow:
                return ShadowSortingBase;
            default:
                return CharacterSortingBase;
        }
    }

    private static void EnsureBillboardIfNeeded(GameObject instance, Room2_5DRenderPreset preset)
    {
        RoomWorldSpaceSettings worldSpace = RoomWorldSpaceSettings.Current;
        if (instance == null || worldSpace == null || !worldSpace.UsesXZPlane)
            return;

        BillboardFacingCamera[] billboards = instance.GetComponentsInChildren<BillboardFacingCamera>(true);
        bool shouldBillboard = preset == Room2_5DRenderPreset.Character;

        if (!shouldBillboard)
        {
            for (int i = 0; i < billboards.Length; i++)
            {
                if (billboards[i] != null)
                    billboards[i].enabled = false;
            }

            return;
        }

        BillboardFacingCamera billboard = instance.GetComponent<BillboardFacingCamera>();
        if (billboard == null)
            billboard = instance.AddComponent<BillboardFacingCamera>();

        billboard.enabled = true;

        for (int i = 0; i < billboards.Length; i++)
        {
            BillboardFacingCamera childBillboard = billboards[i];
            if (childBillboard != null && childBillboard != billboard)
                childBillboard.enabled = false;
        }
    }

    private static void EnsureXZSpriteRendering(GameObject instance)
    {
        RoomWorldSpaceSettings worldSpace = RoomWorldSpaceSettings.Current;
        if (instance == null || worldSpace == null || !worldSpace.UsesXZPlane)
            return;

        SpriteRenderer[] renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers == null || renderers.Length == 0)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            RestoreSceneCompatibleSpriteMaterial(renderer);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.allowOcclusionWhenDynamic = true;
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

        if (materialName.StartsWith("RuntimeXZSpriteUnlit") ||
            materialName.StartsWith("RuntimeFlatTileSpriteUnlit"))
        {
            renderer.sharedMaterial = null;
        }
    }
}
