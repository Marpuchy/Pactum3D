using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyCombat))]
[RequireComponent(typeof(EnemyAnimatorDriver))]
public class EnemyBrain : MonoBehaviour
{
    private const float NavMeshRebindInterval = 0.25f;
    private const float NavMeshRebindMaxDistance = 128f;

    [SerializeField] private CharacterStats stats;
    [SerializeField] private CharacterStatResolver statResolver;
    [SerializeField] private bool chasePlayer = true;
    [SerializeField] private bool autoDisableChaseForRanged = true;
    [SerializeField, Min(0f)] private float attackRangeHysteresis = 0.3f;

    [Header("Shadow")]
    [SerializeField] private float shadowGroundOffset = 0.03f;
    [SerializeField] private float shadowAlpha = 0.62f;
    [SerializeField] private float shadowDiameter = 0.62f;
    [SerializeField] private float shadowTrackedHeight = 1.5f;
    [SerializeField] private float shadowPlanarOffsetTowardsCamera = 0.14f;

    [Header("2.5D Presentation")]
    [SerializeField] private float visualFootOffset = 0.02f;
    [SerializeField] private float visualLift = 0.08f;

    private Transform player;
    private NavMeshAgent agent;
    private EnemyCombat combat;
    private EnemyAnimatorDriver animatorDriver;
    private Transform attackOrigin;
    private Vector3 meleeHitboxBaseLocalPosition;
    private float meleeHitboxPlanarDistance;
    private bool meleeHitboxCached;
    private bool statsSubscribed;
    private float currentAttackRange;
    private float nextNavMeshRebindTime;
    private RoomWorldSpaceSettings worldSpaceSettings;
    private bool isHoldingAttackPosition;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        combat = GetComponent<EnemyCombat>();
        animatorDriver = GetComponent<EnemyAnimatorDriver>();
        if (statResolver == null)
            statResolver = GetComponent<CharacterStatResolver>();
        worldSpaceSettings = RoomWorldSpaceSettings.Current;

        Room2_5DPresentationUtility.EnsureDepthSorting(gameObject, Room2_5DRenderPreset.Prop);
        EnsureXZVisualProxy();
        EnsureShadowProjector();

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
            player = p.transform;
    }

    private void OnEnable()
    {
        if (worldSpaceSettings == null)
            worldSpaceSettings = RoomWorldSpaceSettings.Current;

        EnsureXZVisualProxy();
        EnsureShadowProjector();
        TrySubscribeToStats();
        RefreshStats();
    }

    private void OnDisable()
    {
        UnsubscribeFromStats();
    }

    private void Start()
    {
        bool usesXZPlane = worldSpaceSettings != null && worldSpaceSettings.UsesXZPlane;
        agent.updateRotation = false;
        agent.updateUpAxis = usesXZPlane;
        // Dejamos que el "parar para atacar" lo controle AttackRange, no el stoppingDistance del agente.
        agent.stoppingDistance = 0f;
        ConfigureRuntimeMovementAuthority(usesXZPlane);

        RefreshStats();

        transform.position = ClampToGameplayPlane(transform.position);
        SnapAgentToTransform();
    }

    private void Update()
    {
        if (!IsAgentReady())
        {
            TryRebindToNavMesh();
            animatorDriver.SetMoveVelocity(Vector2.zero);
            return;
        }

        if (combat.IsAttacking)
        {
            StopAgentMovement();
            FaceTowardsPlayer();
            animatorDriver.SetMoveVelocity(Vector2.zero);
            return;
        }
        if (animatorDriver.IsHitLocked())
        {
            StopAgentMovement();
            animatorDriver.SetMoveVelocity(Vector2.zero);
            return;
        }

        if (player == null)
        {
            StopAgentMovement();
            animatorDriver.SetMoveVelocity(Vector2.zero);
            return;
        }

        Vector2 toPlayerFromBody = ToPlanar(player.position - transform.position);
        UpdateMeleeAttackOrigin(ResolveFacingDirection(toPlayerFromBody));
        float distance = GetPlanarDistance(transform.position, player.position);
        UpdateAttackStandoffState(distance);

        // =========================
        // CHASE
        // =========================
        bool shouldChase = ShouldChasePlayer() && !isHoldingAttackPosition;
        if (shouldChase)
        {
            agent.isStopped = false;
            agent.SetDestination(player.position);
            UpdateMeleeAttackOrigin(ResolveFacingDirection(ToPlanar(agent.velocity)));
            animatorDriver.SetMoveVelocity(ToPlanar(agent.velocity));
        }
        else
        {
            StopAgentMovement();
            FaceTowardsPlayer();

            if (distance <= currentAttackRange)
                combat.TryAttack();

            animatorDriver.SetMoveVelocity(Vector2.zero);
            return;
        }
    }

    private void FaceTowardsPlayer()
    {
        if (player == null)
            return;

        Vector2 toPlayer = ToPlanar(player.position - transform.position);
        Vector2 facing = ResolveFacingDirection(toPlayer);
        UpdateMeleeAttackOrigin(facing);
        animatorDriver.FaceDirection(facing);
    }

    private void HandleStatsChanged()
    {
        RefreshStats();
    }

    private void RefreshStats()
    {
        float baseMoveSpeed = stats != null ? stats.GetBaseValue(StatType.MaxSpeed) : agent.speed;
        float baseAcceleration = stats != null ? stats.GetBaseValue(StatType.Acceleration) : agent.acceleration;
        float baseAttackRange = stats != null ? stats.GetBaseValue(StatType.AttackRange) : currentAttackRange;

        agent.speed = ResolveStat(StatType.MaxSpeed, baseMoveSpeed);
        agent.acceleration = ResolveStat(StatType.Acceleration, baseAcceleration);
        currentAttackRange = ResolveStat(StatType.AttackRange, baseAttackRange);
    }

    private float ResolveStat(StatType type, float fallback)
    {
        return statResolver != null ? statResolver.Get(type, fallback) : fallback;
    }

    private void TrySubscribeToStats()
    {
        if (statResolver == null || statsSubscribed)
            return;

        statResolver.StatsChanged += HandleStatsChanged;
        statsSubscribed = true;
    }

    private void UnsubscribeFromStats()
    {
        if (!statsSubscribed)
            return;

        if (statResolver != null)
            statResolver.StatsChanged -= HandleStatsChanged;

        statsSubscribed = false;
    }

    private void TryRebindToNavMesh()
    {
        if (agent == null || !agent.enabled)
            return;

        if (agent.isOnNavMesh)
            return;

        if (Time.time < nextNavMeshRebindTime)
            return;

        nextNavMeshRebindTime = Time.time + NavMeshRebindInterval;

        var filter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = NavMesh.AllAreas
        };

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, NavMeshRebindMaxDistance, filter))
            return;

        if (!agent.Warp(hit.position))
            return;

        agent.nextPosition = hit.position;
        agent.ResetPath();
    }

    private bool IsAgentReady()
    {
        return agent != null && agent.enabled && agent.isOnNavMesh;
    }

    private Vector3 ClampToGameplayPlane(Vector3 position)
    {
        if (worldSpaceSettings != null)
            return worldSpaceSettings.ClampToWalkPlane(position);

        position.z = 0f;
        return position;
    }

    private float GetPlanarDistance(Vector3 a, Vector3 b)
    {
        if (worldSpaceSettings != null)
            return worldSpaceSettings.PlanarDistance(a, b);

        return Vector2.Distance(a, b);
    }

    private Vector2 ToPlanar(Vector3 vector)
    {
        if (worldSpaceSettings != null)
            return worldSpaceSettings.WorldVectorToPlanar(vector);

        return vector;
    }

    private void EnsureShadowProjector()
    {
        BlobShadowProjector shadowProjector = GetComponent<BlobShadowProjector>();
        if (shadowProjector == null)
            shadowProjector = gameObject.AddComponent<BlobShadowProjector>();

        shadowProjector.ConfigureRuntime(
            shadowAlpha,
            shadowDiameter,
            shadowTrackedHeight,
            shadowGroundOffset,
            shadowPlanarOffsetTowardsCamera);
    }

    private void EnsureXZVisualProxy()
    {
        SpriteRenderer rootRenderer = GetComponent<SpriteRenderer>();
        if (rootRenderer == null)
            return;

        bool use3DPresentation = worldSpaceSettings != null && worldSpaceSettings.UsesXZPlane;
        XZSpriteVisualProxy proxy = GetComponent<XZSpriteVisualProxy>();
        BillboardFacingCamera rootBillboard = GetComponent<BillboardFacingCamera>();
        if (rootBillboard != null)
            rootBillboard.enabled = false;

        if (use3DPresentation)
        {
            if (proxy == null)
                proxy = gameObject.AddComponent<XZSpriteVisualProxy>();

            proxy.ConfigureRuntimeAnchoring(visualFootOffset, visualLift, true, false, true);
            proxy.enabled = true;
            proxy.Sync();
            return;
        }

        if (proxy != null)
            proxy.enabled = false;
    }

    private bool ShouldChasePlayer()
    {
        if (!chasePlayer)
            return false;

        if (autoDisableChaseForRanged && TryGetComponent(out EnemyAttackPublisher _))
            return false;

        if (agent != null && agent.speed <= 0.001f)
            return false;

        return true;
    }

    private Vector2 ResolveFacingDirection(Vector2 direction)
    {
        if (animatorDriver == null)
            return direction;

        return animatorDriver.ResolveFacingDirection(direction);
    }

    private void UpdateAttackStandoffState(float distanceToPlayer)
    {
        float enterDistance = Mathf.Max(0f, currentAttackRange);
        float exitDistance = enterDistance + Mathf.Max(0f, attackRangeHysteresis);

        if (distanceToPlayer <= enterDistance)
        {
            isHoldingAttackPosition = true;
            return;
        }

        if (distanceToPlayer >= exitDistance)
            isHoldingAttackPosition = false;
    }

    private void ConfigureRuntimeMovementAuthority(bool usesXZPlane)
    {
        if (!usesXZPlane)
            return;

        if (!TryGetComponent(out Rigidbody rigidbody3D))
            return;

        rigidbody3D.useGravity = false;
        rigidbody3D.isKinematic = true;
        rigidbody3D.linearVelocity = Vector3.zero;
        rigidbody3D.angularVelocity = Vector3.zero;
        rigidbody3D.constraints = RigidbodyConstraints.FreezeRotation;
    }

    private void StopAgentMovement()
    {
        if (agent == null)
            return;

        agent.isStopped = true;

        if (agent.isOnNavMesh)
            agent.ResetPath();

        agent.velocity = Vector3.zero;
        SnapAgentToTransform();
    }

    private void SnapAgentToTransform()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        Vector3 clampedPosition = ClampToGameplayPlane(transform.position);
        transform.position = clampedPosition;
        agent.nextPosition = clampedPosition;
    }

    private void CacheMeleeHitbox(Transform hitboxTransform)
    {
        if (meleeHitboxCached || hitboxTransform == null || hitboxTransform == transform)
            return;

        meleeHitboxBaseLocalPosition = hitboxTransform.localPosition;
        meleeHitboxPlanarDistance = Mathf.Max(
            0.01f,
            Mathf.Abs(meleeHitboxBaseLocalPosition.x),
            Mathf.Abs(meleeHitboxBaseLocalPosition.z));
        meleeHitboxCached = true;
    }

    private void UpdateMeleeAttackOrigin(Vector2 planarDirection)
    {
        if (worldSpaceSettings == null || !worldSpaceSettings.UsesXZPlane)
            return;

        if (animatorDriver != null && !animatorDriver.UsesFourDirectionalFacing)
            return;

        if (attackOrigin == null)
            return;

        if (!meleeHitboxCached)
            CacheMeleeHitbox(attackOrigin);

        if (!meleeHitboxCached || attackOrigin == transform)
            return;

        if (planarDirection.sqrMagnitude <= 0.0001f)
            planarDirection = Vector2.down;

        Vector2 facing = planarDirection.normalized;
        Vector3 localPosition = meleeHitboxBaseLocalPosition;

        if (Mathf.Abs(facing.x) >= Mathf.Abs(facing.y))
        {
            localPosition.x = Mathf.Sign(facing.x) * meleeHitboxPlanarDistance;
            localPosition.z = 0f;
        }
        else
        {
            localPosition.x = 0f;
            localPosition.z = Mathf.Sign(facing.y) * meleeHitboxPlanarDistance;
        }

        attackOrigin.localPosition = localPosition;
    }

}
