using UnityEngine;

[DisallowMultipleComponent]
public class AttackHitbox : MonoBehaviour
{
    [SerializeField] private AttackComponent attackComponent;
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private Collider2D hitbox2D;
    [SerializeField] private Collider hitbox3D;

    private void Awake()
    {
        ResolveReferences();
        ConfigureTriggers();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ConfigureTriggers();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryApplyHit(other != null ? other.transform : null);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryApplyHit(other != null ? other.transform : null);
    }

    private void ResolveReferences()
    {
        if (hitbox2D == null)
            hitbox2D = GetComponent<Collider2D>();

        if (hitbox3D == null)
            hitbox3D = GetComponent<Collider>();

        if (attackComponent == null)
            attackComponent = GetComponentInParent<AttackComponent>();
    }

    private void ConfigureTriggers()
    {
        if (hitbox2D != null)
            hitbox2D.isTrigger = true;

        if (hitbox3D != null)
            hitbox3D.isTrigger = true;
    }

    private void TryApplyHit(Transform otherTransform)
    {
        if (otherTransform == null)
            return;

        if (!IsInTargetLayer(otherTransform))
            return;

        GameObject attacker = attackComponent != null ? attackComponent.gameObject : gameObject;
        GameObject targetObject = otherTransform.gameObject;
        if (targetObject == attacker || otherTransform.IsChildOf(attacker.transform))
            return;

        IBreakable breakable = FindInterfaceInParents<IBreakable>(otherTransform);
        if (breakable != null && !IsPlayerAttacker(attacker))
            return;

        float damage = attackComponent != null ? attackComponent.GetDamage() : 1f;

        HealthComponent health = otherTransform.GetComponentInParent<HealthComponent>();
        if (health != null)
        {
            health.TakeDamage(damage, attacker);
            return;
        }

        IDamageable damageable = FindInterfaceInParents<IDamageable>(otherTransform);
        if (damageable != null)
        {
            damageable.TakeDamage(damage, attacker);
            return;
        }

        if (breakable != null)
            breakable.ApplyDamage();
    }

    private static T FindInterfaceInParents<T>(Transform source) where T : class
    {
        Transform current = source;
        while (current != null)
        {
            MonoBehaviour[] behaviours = current.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is T match)
                    return match;
            }

            current = current.parent;
        }

        return null;
    }

    private bool IsInTargetLayer(Transform source)
    {
        Transform current = source;
        while (current != null)
        {
            if (((1 << current.gameObject.layer) & targetLayer) != 0)
                return true;

            current = current.parent;
        }

        return false;
    }

    private static bool IsPlayerAttacker(GameObject attacker)
    {
        if (attacker == null)
            return false;

        if (attacker.CompareTag("Player"))
            return true;

        Transform root = attacker.transform.root;
        return root != null && root.CompareTag("Player");
    }
}
