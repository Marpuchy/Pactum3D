using UnityEngine;

[CreateAssetMenu(fileName = "CharacterStats", menuName = "Character/CharacterStats")]
public class CharacterStats : ScriptableObject
{
    [Header("Save")]
    [SerializeField] private string saveId;

    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float shieldArmor = 1f;

    [Header("Combat")]
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackSpeed = 1f;
    [SerializeField] private float attackLocktime = 0.2f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float detectionRange = 10f;
    [SerializeField] private float hitReactThreshold = 0f;




    [Header("Dash")]
    [SerializeField] private float dashSpeed = 14f;
    [SerializeField] private float dashDuration = 0.12f;
    [SerializeField] private float dashCooldown = 0.6f;

    [Header("Speed")]
    [SerializeField] private float maxSpeed = 6.5f;

    [Header("Acceleration")]
    [SerializeField] private float acceleration = 60f;      // acelera cuando pulsas
    [SerializeField] private float deceleration = 70f;      // frena cuando sueltas
    [SerializeField] private float turnAcceleration = 90f;  // cambios de dirección rápidos

    public string SaveId => string.IsNullOrWhiteSpace(saveId) ? name : saveId;

    public float GetBaseValue(StatType type)
    {
        switch (type)
        {
            case StatType.MaxHealth:
                return maxHealth;
            case StatType.ShieldArmor:
                return shieldArmor;
            case StatType.AttackDamage:
                return attackDamage;
            case StatType.AttackSpeed:
                return attackSpeed;
            case StatType.AttackLockTime:
                return attackLocktime;
            case StatType.AttackRange:
                return attackRange;
            case StatType.DetectionRange:
                return detectionRange;
            case StatType.HitReactThreshold:
                return hitReactThreshold;
            case StatType.DashSpeed:
                return dashSpeed;
            case StatType.DashDuration:
                return dashDuration;
            case StatType.DashCooldown:
                return dashCooldown;
            case StatType.MaxSpeed:
                return maxSpeed;
            case StatType.Acceleration:
                return acceleration;
            case StatType.Deceleration:
                return deceleration;
            case StatType.TurnAcceleration:
                return turnAcceleration;
            default:
                return 0f;
        }
    }
}
