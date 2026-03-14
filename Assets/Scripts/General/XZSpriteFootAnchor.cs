using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class XZSpriteFootAnchor : MonoBehaviour
{
    [SerializeField] private bool autoDetectFootOffset = true;
    [SerializeField, Min(0f)] private float manualFootOffset;
    [SerializeField] private bool updateEveryFrame = true;
    [SerializeField] private bool includeInactiveChildren = true;
    [SerializeField] private bool syncNavMeshAgentBaseOffset = true;
    [SerializeField] private SpriteRenderer[] spriteRenderers = new SpriteRenderer[0];
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private RoomWorldSpaceSettings worldSpaceSettings;

    private void Awake()
    {
        RefreshTargets();
        ApplyFootAnchor();
    }

    private void OnEnable()
    {
        RefreshTargets();
        ApplyFootAnchor();
    }

    private void LateUpdate()
    {
        if (updateEveryFrame)
            ApplyFootAnchor();
    }

    public void RefreshTargets()
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(includeInactiveChildren);

        if (navMeshAgent == null)
            navMeshAgent = GetComponent<NavMeshAgent>();

        if (worldSpaceSettings == null)
        {
            worldSpaceSettings = RoomWorldSpaceSettings.Current != null
                ? RoomWorldSpaceSettings.Current
                : FindFirstObjectByType<RoomWorldSpaceSettings>();
        }
    }

    public void ApplyFootAnchor()
    {
        RefreshTargets();

        if (worldSpaceSettings == null || !worldSpaceSettings.UsesXZPlane)
        {
            SyncNavMeshBaseOffset(0f);
            return;
        }

        float footOffset = ResolveFootOffset();
        transform.position = worldSpaceSettings.ClampToWalkPlane(transform.position, footOffset);
        SyncNavMeshBaseOffset(footOffset);
    }

    private float ResolveFootOffset()
    {
        if (!autoDetectFootOffset)
            return manualFootOffset;

        float footOffset = 0f;
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null)
                continue;

            Bounds localBounds = spriteRenderer.localBounds;
            Vector3 bottomWorld = spriteRenderer.transform.TransformPoint(
                new Vector3(localBounds.center.x, localBounds.min.y, localBounds.center.z));

            footOffset = Mathf.Max(footOffset, transform.position.y - bottomWorld.y);
        }

        return footOffset;
    }

    private void SyncNavMeshBaseOffset(float footOffset)
    {
        if (!syncNavMeshAgentBaseOffset || navMeshAgent == null)
            return;

        navMeshAgent.baseOffset = footOffset;
    }
}
