using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class EnemyGroundPlaneAnchor : MonoBehaviour
{
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private RoomWorldSpaceSettings worldSpaceSettings;
    [SerializeField] private bool updateEveryFrame = true;

    private bool isFlying;
    private bool configured;

    private void Awake()
    {
        ResolveReferences();
        ApplyAnchor();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ApplyAnchor();
    }

    private void LateUpdate()
    {
        if (updateEveryFrame)
            ApplyAnchor();
    }

    public void ConfigureRuntime(bool isFlyingEnemy, RoomWorldSpaceSettings explicitWorldSpaceSettings = null)
    {
        isFlying = isFlyingEnemy;
        configured = true;

        if (explicitWorldSpaceSettings != null)
            worldSpaceSettings = explicitWorldSpaceSettings;

        ResolveReferences();
        ApplyAnchor();
    }

    public void ApplyAnchor()
    {
        ResolveReferences();

        if (!configured || isFlying || worldSpaceSettings == null || !worldSpaceSettings.UsesXZPlane)
            return;

        Vector3 clampedPosition = worldSpaceSettings.ClampToWalkPlane(transform.position);
        transform.position = clampedPosition;

        if (navMeshAgent != null && navMeshAgent.enabled)
        {
            navMeshAgent.baseOffset = 0f;

            if (navMeshAgent.isOnNavMesh)
                navMeshAgent.nextPosition = clampedPosition;
        }

        if (TryGetComponent(out Rigidbody rigidbody3D))
        {
            Vector3 velocity = rigidbody3D.linearVelocity;
            if (!Mathf.Approximately(velocity.y, 0f))
            {
                velocity.y = 0f;
                rigidbody3D.linearVelocity = velocity;
            }
        }
    }

    private void ResolveReferences()
    {
        if (navMeshAgent == null)
            navMeshAgent = GetComponent<NavMeshAgent>();

        if (worldSpaceSettings == null)
        {
            worldSpaceSettings = RoomWorldSpaceSettings.Current != null
                ? RoomWorldSpaceSettings.Current
                : FindFirstObjectByType<RoomWorldSpaceSettings>();
        }
    }
}
