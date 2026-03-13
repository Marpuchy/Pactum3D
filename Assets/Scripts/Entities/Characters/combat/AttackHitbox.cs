using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AttackHitbox : MonoBehaviour
{
    [SerializeField] private AttackComponent attackComponent;
    [SerializeField] private LayerMask targetLayer;

    private Collider2D hitbox;

    private void Awake()
    {
        hitbox = GetComponent<Collider2D>();
        hitbox.isTrigger = true;

        if (attackComponent == null)
            attackComponent = GetComponentInParent<AttackComponent>();
    }

    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & targetLayer) == 0)
            return;

        float damage = attackComponent != null ? attackComponent.GetDamage() : 1f;
        GameObject attacker = attackComponent != null ? attackComponent.gameObject : gameObject;

        if (other.TryGetComponent(out IDamageable damageable))
        {
            damageable.TakeDamage(damage, attacker);
        }
        else if (other.TryGetComponent(out HealthComponent health))
        {
            health.TakeDamage(damage);
        }
        
        if (!other.TryGetComponent(out IBreakable breakable))
            return;
        
        breakable.ApplyDamage();

    }

}
