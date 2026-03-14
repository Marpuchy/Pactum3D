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

    private Transform player;
    private NavMeshAgent agent;
    private EnemyCombat combat;
    private EnemyAnimatorDriver animatorDriver;
    private Transform attackOrigin;
    private bool statsSubscribed;
    private float currentAttackRange;
    private float nextNavMeshRebindTime;
    private RoomWorldSpaceSettings worldSpaceSettings;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        combat = GetComponent<EnemyCombat>();
        animatorDriver = GetComponent<EnemyAnimatorDriver>();
        if (statResolver == null)
            statResolver = GetComponent<CharacterStatResolver>();
        worldSpaceSettings = RoomWorldSpaceSettings.Current;

        Room2_5DPresentationUtility.EnsureDepthSorting(gameObject, Room2_5DRenderPreset.Character);

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
            player = p.transform;
    }

    private void OnEnable()
    {
        if (worldSpaceSettings == null)
            worldSpaceSettings = RoomWorldSpaceSettings.Current;

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

        RefreshStats();

        transform.position = ClampToGameplayPlane(transform.position);
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
            agent.isStopped = true;
            FaceTowardsPlayer();
            animatorDriver.SetMoveVelocity(Vector2.zero);
            return;
        }
        if (animatorDriver.IsHitLocked())
        {
            agent.isStopped = true;
            animatorDriver.SetMoveVelocity(Vector2.zero);
            return;
        }

        if (player == null)
        {
            agent.isStopped = true;
            animatorDriver.SetMoveVelocity(Vector2.zero);
            return;
        }

        float distance = GetPlanarDistance(GetAttackOriginPosition(), player.position);

        // =========================
        // CHASE
        // =========================
        if (distance > currentAttackRange)
        {
            agent.isStopped = false;
            agent.SetDestination(player.position);
        }
        else
        {
            // =========================
            // ATTACK
            // =========================
            agent.isStopped = true;
            FaceTowardsPlayer();
            combat.TryAttack();
        }

        // Animación de movimiento
        animatorDriver.SetMoveVelocity(ToPlanar(agent.velocity));
    }

    private Vector3 GetAttackOriginPosition()
    {
        if (TryGetComponent(out EnemyAttackPublisher rangedAttackPublisher))
        {
            Transform rangedOrigin = rangedAttackPublisher.Origin;
            if (rangedOrigin != null)
                return rangedOrigin.position;
        }

        if (attackOrigin == null)
        {
            AttackHitbox meleeHitbox = GetComponentInChildren<AttackHitbox>(true);
            attackOrigin = meleeHitbox != null ? meleeHitbox.transform : transform;
        }

        return attackOrigin.position;
    }

    private void FaceTowardsPlayer()
    {
        if (player == null)
            return;

        Vector2 toPlayer = ToPlanar(player.position - GetAttackOriginPosition());
        animatorDriver.FaceDirection(toPlayer);
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
}
