using UnityEngine;

[DisallowMultipleComponent]
public class AttackComponent : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private CharacterStats stats;
    [SerializeField] private CharacterStatResolver statResolver;

    [Header("Attack Timing")]
    [SerializeField] private float attackAnimSpeedFactor = 0.5f;
    [SerializeField] private float minAttackAnimDuration = 0.05f;

    private const float MinAttackSpeed = 0.01f;
    private const float DefaultAttackSpeed = 1f;
    private const float DefaultAttackAnimDuration = 0.2f;

    private float attackLockEndTime = -Mathf.Infinity;

    public float AttackSpeed =>
        Mathf.Max(ResolveStat(StatType.AttackSpeed, DefaultAttackSpeed), MinAttackSpeed);

    public float AttackInterval => 1f / AttackSpeed;

    public float BaseAttackAnimationDuration =>
        Mathf.Max(ResolveStat(StatType.AttackLockTime, DefaultAttackAnimDuration), minAttackAnimDuration);

    public float AttackAnimationDuration
    {
        get
        {
            float speedFactor = Mathf.Max(attackAnimSpeedFactor, 0.01f);
            float scaledSpeed = Mathf.Max(AttackSpeed * speedFactor, 0.01f);
            float duration = BaseAttackAnimationDuration / scaledSpeed;
            return Mathf.Max(duration, minAttackAnimDuration);
        }
    }

    private void Awake()
    {
        if (statResolver == null)
            statResolver = GetComponent<CharacterStatResolver>();

        if (stats == null && statResolver == null)
        {
            Debug.LogError($"{name} has no stats source assigned!", this);
            enabled = false;
            return;
        }
    }

    public void SetBaseStats(CharacterStats statsAsset)
    {
        if (statsAsset == null)
            return;

        stats = statsAsset;
    }
    public float AttackLockDuration
    {
        get
        {
            return Mathf.Max(AttackInterval, AttackAnimationDuration);
        }
    }
    /// <summary>
    /// True mientras la animación de ataque te mantiene bloqueado
    /// </summary>
    public bool IsAttackLocked =>
        Time.time < attackLockEndTime;

    public bool CanAttack =>
        !IsAttackLocked;

    /// <summary>
    /// Llamar SOLO cuando se inicia la animación de ataque
    /// </summary>
    public void ConsumeAttack()
    {
        attackLockEndTime = Time.time + AttackLockDuration;
    }

    /// <summary>
    /// Llamar desde la hitbox
    /// </summary>
    public float GetDamage()
    {
        float baseDamage = GetBaseDamage();
        return ResolveStat(StatType.AttackDamage, baseDamage);
    }

    // =============================
    // INTERNAL
    // =============================

    private float GetBaseDamage()
    {
        return stats != null ? stats.GetBaseValue(StatType.AttackDamage) : 0f;
    }

    private float ResolveStat(StatType type, float fallbackValue)
    {
        if (statResolver != null)
        {
            float baseValue = stats != null ? stats.GetBaseValue(type) : fallbackValue;
            return statResolver.Get(type, baseValue);
        }

        return stats != null ? stats.GetBaseValue(type) : fallbackValue;
    }
}
