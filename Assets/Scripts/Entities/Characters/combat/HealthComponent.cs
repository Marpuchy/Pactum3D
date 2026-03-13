using System;
using UnityEngine;
using UnityEngine.AI;

public class HealthComponent : MonoBehaviour, IDamageable
{
    [SerializeField] private CharacterStats stats;
    [SerializeField] private CharacterStatResolver statResolver;

    [Header("Player Only")]
    [SerializeField] private bool isPlayer;
    [SerializeField] private HealthChangedEventSO healthChangedEvent;

    [Header("Invulnerability Frames")]
    [SerializeField] private float iFrameDuration = 0.25f;
    private float iFrameTimer = 0f;

    [Header("Events")]
    [SerializeField] private DamageEventSO damageEvent;
    [SerializeField] private EnemyDeathEvent enemyDeathEvent;
    [SerializeField] private OnDeathEventSO onDeathEvent;
    [SerializeField] private HealthEventSO healthEvent;
    [SerializeField] private bool autoAddDamageFlashFeedback = true;
    [SerializeField] private bool autoAddPlayerDamageCameraShakeFeedback = true;
    [SerializeField] private bool autoAddPlayerLowHealthVignetteFeedback = true;

    private bool statsSubscribed;
    private PactManager subscribedManager;
    private bool isDead;
    private float currentHealth;
    private float currentMaxHealth;

    public event Action<DamageReceivedInfo> DamageReceived;
    public CharacterStats BaseStats => stats;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => currentMaxHealth;
    public bool IsDead => isDead;

    private void Awake()
    {
        if (statResolver == null)
            statResolver = GetComponent<CharacterStatResolver>();

        if (autoAddDamageFlashFeedback &&
            !TryGetComponent<DamageFlashFeedback>(out _))
        {
            gameObject.AddComponent<DamageFlashFeedback>();
        }

        if (isPlayer &&
            autoAddPlayerDamageCameraShakeFeedback &&
            !TryGetComponent<PlayerDamageCameraShakeFeedback>(out _))
        {
            gameObject.AddComponent<PlayerDamageCameraShakeFeedback>();
        }

        if (isPlayer &&
            autoAddPlayerLowHealthVignetteFeedback &&
            !TryGetComponent<PlayerLowHealthVignetteFeedback>(out _))
        {
            gameObject.AddComponent<PlayerLowHealthVignetteFeedback>();
        }
    }

    private void Start()
    {
        RefreshMaxHealth(true);

        if (isPlayer && healthChangedEvent != null)
            healthChangedEvent.Raise(currentHealth, currentMaxHealth);
    }

    private void OnEnable()
    {
        TrySubscribeToStats();
        SubscribeDamageEvent();
    }

    private void OnDisable()
    {
        UnsubscribeFromStats();
        UnsubscribeDamageEvent();
    }

    private void Update()
    {
        // i-frames (NO dependientes del timeScale)
        if (iFrameTimer > 0f)
        {
            iFrameTimer -= Time.unscaledDeltaTime;
            if (iFrameTimer < 0f)
                iFrameTimer = 0f;
        }
        /*if (statResolver == null || subscribedManager != null)
            return;
        TrySubscribe();*/
        if (statResolver == null || statsSubscribed)
            return;

        TrySubscribeToStats();
    }

    public void TakeDamage(float damage)
    {
        TakeDamage(damage, null);
    }

    public void TakeDamage(float damage, GameObject attacker)
    {
        if (isDead) return;

        if (iFrameTimer > 0f)
            return;

        float finalDamage = CalculateDamage(damage);
        if (finalDamage <= 0f)
            return;

        ApplyHealthDelta(-finalDamage);

        DamageReceived?.Invoke(new DamageReceivedInfo(
            damage,
            finalDamage,
            gameObject,
            attacker));

        if (healthEvent != null)
            healthEvent.RaiseHit(finalDamage, gameObject, attacker);

        if (!isDead)
        {
            float baseThreshold = stats != null ? stats.GetBaseValue(StatType.HitReactThreshold) : 0f;
            float hitReactThreshold = ResolveStat(StatType.HitReactThreshold, baseThreshold);
            bool shouldReact = finalDamage > hitReactThreshold;
            if (shouldReact)
            {
                if (TryGetComponent(out EnemyAnimatorDriver enemyAnimator))
                    enemyAnimator.PlayHit();
            }
        }

        iFrameTimer = iFrameDuration;
    }

    public void RestoreHealth(float amount)
    {
        if (isDead) return;
        if (amount <= 0f) return;

        ApplyHealthDelta(amount);
    }

    public void SetBaseStats(CharacterStats statsAsset, bool resetCurrentHealth = false)
    {
        if (statsAsset == null)
            return;

        stats = statsAsset;
        RefreshMaxHealth(resetCurrentHealth);
    }

    public void SetMissingHealthPercent(float missingHealthPercent)
    {
        SetHealthNormalized(1f - Mathf.Clamp01(missingHealthPercent));
    }

    public void SetHealthNormalized(float normalizedHealth)
    {
        normalizedHealth = Mathf.Clamp01(normalizedHealth);

        if (currentMaxHealth <= 0f)
            RefreshMaxHealth(true);

        currentHealth = Mathf.Clamp(normalizedHealth * currentMaxHealth, 0f, currentMaxHealth);
        isDead = currentHealth <= 0f;

        if (isPlayer && healthChangedEvent != null)
            healthChangedEvent.Raise(currentHealth, currentMaxHealth);
    }

    private void HandleModifiersChanged()
    {
        RefreshMaxHealth(false);
    }

    private void RefreshMaxHealth(bool resetCurrentHealth)
    {
        float baseMaxHealth = GetBaseMaxHealth();
        float resolvedMaxHealth = ResolveStat(StatType.MaxHealth, baseMaxHealth);

        if (Mathf.Approximately(resolvedMaxHealth, currentMaxHealth))
            return;

        float ratio = currentMaxHealth > 0f ? currentHealth / currentMaxHealth : 1f;
        currentMaxHealth = resolvedMaxHealth;
        currentHealth = resetCurrentHealth
            ? currentMaxHealth
            : Mathf.Clamp(ratio * currentMaxHealth, 0f, currentMaxHealth);

        if (isPlayer && healthChangedEvent != null)
            healthChangedEvent.Raise(currentHealth, currentMaxHealth);
    }

    private float GetBaseMaxHealth()
    {
        return stats != null ? stats.GetBaseValue(StatType.MaxHealth) : 0f;
    }

    private float ResolveStat(StatType type, float fallbackValue)
    {
        return statResolver != null ? statResolver.Get(type, fallbackValue) : fallbackValue;
    }

    private void TrySubscribeToStats()
    {
        if (statResolver == null)
            return;

        if (statsSubscribed)
            return;

        statResolver.StatsChanged += HandleModifiersChanged;
        statsSubscribed = true;
    }

    private void SubscribeDamageEvent()
    {
        if (damageEvent == null)
            return;

        damageEvent.OnEventRaised += OnDamageEventRaised;
    }

    private void UnsubscribeDamageEvent()
    {
        if (damageEvent == null)
            return;

        damageEvent.OnEventRaised -= OnDamageEventRaised;
    }

    private void OnDamageEventRaised(GameObject target, int amount)
    {
        if (target != gameObject)
            return;

        TakeDamage(amount, null);
    }

    private void ApplyHealthDelta(float delta)
    {
        float previousHealth = currentHealth;
        currentHealth = Mathf.Clamp(currentHealth + delta, 0f, currentMaxHealth);

        if (!Mathf.Approximately(previousHealth, currentHealth) &&
            isPlayer &&
            healthChangedEvent != null)
        {
            healthChangedEvent.Raise(currentHealth, currentMaxHealth);
        }

        if (currentHealth <= 0f)
            Die();
    }

    private float CalculateDamage(float rawDamage)
    {
        float clampedDamage = Mathf.Max(0f, rawDamage);
        if (clampedDamage <= 0f)
            return 0f;

        float shieldArmor = ResolveStat(StatType.ShieldArmor, 0f);
        return Mathf.Max(1f, clampedDamage - shieldArmor);
    }

    private void UnsubscribeFromStats()
    {
        if (!statsSubscribed)
            return;

        if (statResolver != null)
            statResolver.StatsChanged -= HandleModifiersChanged;

        statsSubscribed = false;
    }

    private void Die()
    {
        if(isDead) return;

        isDead = true;
        DisableCollisions();
        DisableNavigation();
        enemyDeathEvent?.Raise(gameObject);
        if (isPlayer && onDeathEvent != null)
        {
            onDeathEvent.Raise();
        }

        if (TryGetComponent(out EnemyAnimatorDriver enemyAnimator))
        {
            enemyAnimator.OnDeath();
        }
        
    }
    
    private void DisableCollisions()
    {
        if (TryGetComponent(out Rigidbody rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        
        var colliders = GetComponentsInChildren<BoxCollider>();

        foreach (var collider in colliders)
        {
            collider.enabled = false;
        }
    }
    
    private void DisableNavigation()
    {
        if (TryGetComponent(out NavMeshAgent agent))
        {
            agent.enabled = false;
        }
    }


}
