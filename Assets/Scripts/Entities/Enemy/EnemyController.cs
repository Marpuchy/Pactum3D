using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 2f;
    public float chaseRange = 5f;
    public float attackRange = 1.2f;

    [SerializeField] private CharacterStats stats;
    [SerializeField] private CharacterStatResolver statResolver;

    private Transform player;
    private Rigidbody2D rb;
    private EnemyCombat combat;
    private EnemyAnimatorDriver animatorDriver;
    private bool statsSubscribed;
    private float currentMoveSpeed;
    private float currentChaseRange;
    private float currentAttackRange;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        combat = GetComponent<EnemyCombat>();
        animatorDriver = GetComponent<EnemyAnimatorDriver>();
        if (statResolver == null)
            statResolver = GetComponent<CharacterStatResolver>();
    }

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        RefreshStats();
    }

    private void OnEnable()
    {
        TrySubscribeToStats();
        RefreshStats();
    }

    private void OnDisable()
    {
        UnsubscribeFromStats();
    }

    void FixedUpdate()
    {
        if (animatorDriver != null && animatorDriver.IsHitLocked())
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (player == null)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float distance = Vector2.Distance(transform.position, player.position);

        if (distance > currentChaseRange)
        {
            rb.linearVelocity = Vector2.zero; // idle
        }
        else if (distance > currentAttackRange)
        {
            ChasePlayer();
        }
        else
        {
            AttackPlayer();
        }
    }

    void ChasePlayer()
    {
        Vector2 dir = (player.position - transform.position).normalized;
        rb.linearVelocity = dir * currentMoveSpeed;
    }

    void AttackPlayer()
    {
        rb.linearVelocity = Vector2.zero;
        // el daño real se produce por la hitbox
    }

    private void HandleStatsChanged()
    {
        RefreshStats();
    }

    private void RefreshStats()
    {
        float baseMoveSpeed = stats != null ? stats.GetBaseValue(StatType.MaxSpeed) : moveSpeed;
        float baseChaseRange = stats != null ? stats.GetBaseValue(StatType.DetectionRange) : chaseRange;
        float baseAttackRange = stats != null ? stats.GetBaseValue(StatType.AttackRange) : attackRange;

        currentMoveSpeed = ResolveStat(StatType.MaxSpeed, baseMoveSpeed);
        currentChaseRange = ResolveStat(StatType.DetectionRange, baseChaseRange);
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
}
