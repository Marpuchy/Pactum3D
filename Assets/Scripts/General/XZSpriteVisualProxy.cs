using UnityEngine;

[DisallowMultipleComponent]
public sealed class XZSpriteVisualProxy : MonoBehaviour
{
    private const string VisualObjectName = "XZVisualSprite";
    private const float DefaultFootPlaneOffset = 0.02f;
    private const float DefaultAdditionalVisualLift = 0.08f;

    [SerializeField] private SpriteRenderer sourceRenderer;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer visualRenderer;
    [SerializeField] private float footPlaneOffset = 0.01f;
    [SerializeField] private float additionalVisualLift;
    [SerializeField] private bool updateEveryFrame = true;
    [SerializeField] private RoomWorldSpaceSettings worldSpaceSettings;

    private void Awake()
    {
        RefreshReferences();
        Sync();
    }

    private void OnEnable()
    {
        RefreshReferences();
        Sync();
    }

    private void LateUpdate()
    {
        if (updateEveryFrame)
            Sync();
    }

    public void ConfigureRuntimeAnchoring(float footOffset, float additionalLift, bool continuousSync = true)
    {
        footPlaneOffset = footOffset;
        additionalVisualLift = additionalLift;
        updateEveryFrame = continuousSync;
    }

    public void Sync()
    {
        RefreshReferences();
        if (sourceRenderer == null)
            return;

        RoomWorldSpaceSettings settings = ResolveWorldSpaceSettings();
        bool useXZ = settings != null && settings.UsesXZPlane;

        if (!useXZ)
        {
            sourceRenderer.enabled = true;
            if (visualRoot != null)
                visualRoot.gameObject.SetActive(false);

            BillboardFacingCamera rootBillboard = GetComponent<BillboardFacingCamera>();
            if (rootBillboard != null)
                rootBillboard.enabled = true;

            return;
        }

        EnsureVisualObjects();
        if (visualRoot == null || visualRenderer == null)
            return;

        BillboardFacingCamera rootFacing = GetComponent<BillboardFacingCamera>();
        if (rootFacing != null)
            rootFacing.enabled = false;

        sourceRenderer.enabled = false;
        visualRoot.gameObject.SetActive(true);
        CopyRendererState();
        UpdateVisualPosition(settings);
    }

    private void RefreshReferences()
    {
        if (sourceRenderer == null)
            sourceRenderer = GetComponent<SpriteRenderer>();

        if (visualRoot == null)
        {
            Transform existing = transform.Find(VisualObjectName);
            if (existing != null)
                visualRoot = existing;
        }

        if (visualRenderer == null && visualRoot != null)
            visualRenderer = visualRoot.GetComponent<SpriteRenderer>();

        if (worldSpaceSettings == null)
            worldSpaceSettings = ResolveWorldSpaceSettings();

        if (footPlaneOffset <= 0f)
            footPlaneOffset = DefaultFootPlaneOffset;
    }

    private RoomWorldSpaceSettings ResolveWorldSpaceSettings()
    {
        if (worldSpaceSettings == null)
        {
            worldSpaceSettings = RoomWorldSpaceSettings.Current != null
                ? RoomWorldSpaceSettings.Current
                : FindFirstObjectByType<RoomWorldSpaceSettings>();
        }

        return worldSpaceSettings;
    }

    private void EnsureVisualObjects()
    {
        if (visualRoot == null)
        {
            GameObject child = new GameObject(VisualObjectName);
            child.transform.SetParent(transform, false);
            visualRoot = child.transform;
        }

        if (visualRenderer == null)
            visualRenderer = visualRoot.GetComponent<SpriteRenderer>();
        if (visualRenderer == null)
            visualRenderer = visualRoot.gameObject.AddComponent<SpriteRenderer>();

        if (visualRoot.GetComponent<BillboardFacingCamera>() == null)
            visualRoot.gameObject.AddComponent<BillboardFacingCamera>();
    }

    private void CopyRendererState()
    {
        if (sourceRenderer == null || visualRenderer == null)
            return;

        visualRenderer.enabled = true;
        visualRenderer.sprite = sourceRenderer.sprite;
        visualRenderer.color = sourceRenderer.color;
        visualRenderer.flipX = sourceRenderer.flipX;
        visualRenderer.flipY = sourceRenderer.flipY;
        visualRenderer.drawMode = sourceRenderer.drawMode;
        visualRenderer.size = sourceRenderer.size;
        visualRenderer.maskInteraction = sourceRenderer.maskInteraction;
        visualRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        visualRenderer.sortingOrder = sourceRenderer.sortingOrder;
        visualRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
        visualRenderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
        visualRenderer.receiveShadows = sourceRenderer.receiveShadows;
        visualRenderer.allowOcclusionWhenDynamic = false;
    }

    private void UpdateVisualPosition(RoomWorldSpaceSettings settings)
    {
        if (visualRoot == null || sourceRenderer == null || sourceRenderer.sprite == null || settings == null)
            return;

        float verticalScale = Mathf.Max(Mathf.Abs(transform.lossyScale.y), 0.0001f);
        float spriteBottomLocalY = sourceRenderer.localBounds.min.y;
        float spriteBottomWorldOffset = spriteBottomLocalY * verticalScale;
        float walkPlaneY = settings.Origin.y + settings.OrthogonalAxisOffset;
        float resolvedAdditionalLift = additionalVisualLift;
        if (resolvedAdditionalLift <= 0f)
            resolvedAdditionalLift = DefaultAdditionalVisualLift;

        float desiredCenterWorldY = walkPlaneY + footPlaneOffset + resolvedAdditionalLift - spriteBottomWorldOffset;
        float localY = (desiredCenterWorldY - transform.position.y) / verticalScale;

        visualRoot.localPosition = new Vector3(0f, localY, 0f);
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;
    }
}
