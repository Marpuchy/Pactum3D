using UnityEngine;

public sealed class EnemyAttackPublisher : MonoBehaviour
{
    [SerializeField] private AttackEventChannelSO attackEvent;
    [SerializeField] private Transform originOverride;
    [SerializeField] private Transform targetOverride;
    [SerializeField] private string targetTag = "Player";
    [SerializeField] private float baseAttackSpeed = 1f;
    [SerializeField] private float baseAttackRange = 4f;
    [SerializeField] private CharacterStatResolver statResolver;

    private float nextFireTime;
    private float nextTargetSearchTime;
    private Transform target;
    private bool statsSubscribed;
    private float currentAttackRange;
    private float currentAttackSpeed;

    public Transform Origin => originOverride != null ? originOverride : transform;

    private void Awake()
    {
        if (originOverride == null)
            originOverride = transform;

        if (statResolver == null)
            statResolver = GetComponent<CharacterStatResolver>();
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

    private void Start()
    {
        TryResolveTarget(true);
    }

    private void Update()
    {
        if (attackEvent == null)
            return;

        if (target == null)
        {
            TryResolveTarget(false);
            return;
        }

        if (Time.time < nextFireTime)
            return;

        Vector2 origin = originOverride != null ? (Vector2)originOverride.position : (Vector2)transform.position;
        Vector2 toTarget = (Vector2)target.position - origin;
        if (toTarget.sqrMagnitude <= 0.0001f)
            return;

        if (toTarget.sqrMagnitude > currentAttackRange * currentAttackRange)
            return;

        float cooldown = 1f / Mathf.Max(0.01f, currentAttackSpeed);
        nextFireTime = Time.time + cooldown;

        attackEvent.Raise(new AttackRequest(gameObject, origin, toTarget.normalized));
    }

    private void TryResolveTarget(bool immediate)
    {
        if (!immediate && Time.time < nextTargetSearchTime)
            return;

        nextTargetSearchTime = Time.time + 0.5f;

        if (IsValidTarget(targetOverride))
        {
            target = targetOverride;
            return;
        }

        if (string.IsNullOrEmpty(targetTag))
            return;

        GameObject targetObject = GameObject.FindGameObjectWithTag(targetTag);
        if (targetObject != null && IsValidTarget(targetObject.transform))
            target = targetObject.transform;
    }

    private bool IsValidTarget(Transform candidate)
    {
        if (candidate == null)
            return false;

        return candidate.root != transform.root;
    }

    private void HandleStatsChanged()
    {
        RefreshStats();
    }

    private void RefreshStats()
    {
        currentAttackRange = statResolver != null
            ? statResolver.Get(StatType.AttackRange, baseAttackRange)
            : baseAttackRange;

        currentAttackSpeed = statResolver != null
            ? statResolver.Get(StatType.AttackSpeed, baseAttackSpeed)
            : baseAttackSpeed;
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
