using System;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class SpriteDepthSorter : MonoBehaviour
{
    [SerializeField] private Transform pivot;
    [SerializeField] private float pivotYOffset;
    [SerializeField] private int sortingOrderOffset;
    [SerializeField, Min(1)] private int orderPerWorldUnit = 16;
    [SerializeField] private bool sortEveryFrame = true;
    [SerializeField] private bool includeInactiveChildren = true;
    [SerializeField] private SortingGroup sortingGroup;
    [SerializeField] private SpriteRenderer[] spriteRenderers = Array.Empty<SpriteRenderer>();

    private int[] relativeRendererOrders = Array.Empty<int>();
    private int rootRendererOrder;

    private void Awake()
    {
        RefreshRenderTargets();
        ApplySorting();
    }

    private void OnEnable()
    {
        RefreshRenderTargets();
        ApplySorting();
    }

    private void LateUpdate()
    {
        if (sortEveryFrame)
            ApplySorting();
    }

    private void OnValidate()
    {
        orderPerWorldUnit = Mathf.Max(1, orderPerWorldUnit);

        if (!Application.isPlaying)
            RefreshRenderTargets();

        ApplySorting();
    }

    public void Configure(int orderOffset, int ordersPerUnit = 16, Transform customPivot = null, bool continuous = true)
    {
        sortingOrderOffset = orderOffset;
        orderPerWorldUnit = Mathf.Max(1, ordersPerUnit);
        sortEveryFrame = continuous;

        if (customPivot != null)
            pivot = customPivot;

        RefreshRenderTargets();
        ApplySorting();
    }

    public void RefreshRenderTargets()
    {
        if (pivot == null)
            pivot = transform;

        if (sortingGroup == null)
            sortingGroup = GetComponentInChildren<SortingGroup>(includeInactiveChildren);

        if (sortingGroup != null)
            return;

        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(includeInactiveChildren);
        CaptureRendererOrders();
    }

    public void ApplySorting()
    {
        int targetOrder = sortingOrderOffset - Mathf.RoundToInt(GetPivotSortingAxisValue() * orderPerWorldUnit);

        if (sortingGroup != null)
        {
            sortingGroup.sortingOrder = targetOrder;
            return;
        }

        if (spriteRenderers == null || spriteRenderers.Length == 0)
            return;

        if (relativeRendererOrders == null || relativeRendererOrders.Length != spriteRenderers.Length)
            CaptureRendererOrders();

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer renderer = spriteRenderers[i];
            if (renderer == null)
                continue;

            renderer.sortingOrder = targetOrder + relativeRendererOrders[i];
        }
    }

    private void CaptureRendererOrders()
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            relativeRendererOrders = Array.Empty<int>();
            rootRendererOrder = 0;
            return;
        }

        rootRendererOrder = int.MaxValue;
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer renderer = spriteRenderers[i];
            if (renderer == null)
                continue;

            rootRendererOrder = Mathf.Min(rootRendererOrder, renderer.sortingOrder);
        }

        if (rootRendererOrder == int.MaxValue)
            rootRendererOrder = 0;

        relativeRendererOrders = new int[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer renderer = spriteRenderers[i];
            relativeRendererOrders[i] = renderer != null
                ? renderer.sortingOrder - rootRendererOrder
                : 0;
        }
    }

    private float GetPivotSortingAxisValue()
    {
        Transform targetPivot = pivot != null ? pivot : transform;
        RoomWorldSpaceSettings settings = RoomWorldSpaceSettings.Current;
        if (settings != null && settings.UsesXZPlane)
            return targetPivot.position.z + pivotYOffset;

        return targetPivot.position.y + pivotYOffset;
    }
}
