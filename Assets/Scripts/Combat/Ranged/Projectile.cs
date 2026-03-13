using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public sealed class Projectile : MonoBehaviour
{
    [SerializeField] private float skinWidth = 0.02f;
    [SerializeField] private LayerMask collisionMask;

    [Header("Visual")]
    [SerializeField] private float spriteAngleOffset = 180f;

    private Rigidbody2D rb;
    private IProjectileSpawner spawner;
    private GameObject attacker;

    private Vector2 velocity;
    private float damage;
    private float lifeRemaining;
    private int remainingBounces;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.isKinematic = true;
        rb.gravityScale = 0f;
    }

    public void Initialize(
        Vector2 direction,
        float speed,
        float damage,
        int maxBounces,
        float lifeSeconds,
        GameObject attacker,
        IProjectileSpawner spawner)
    {
        velocity = direction.normalized * speed;

        this.damage = damage;
        remainingBounces = maxBounces;
        lifeRemaining = lifeSeconds;
        this.attacker = attacker;
        this.spawner = spawner;

        rb.position = transform.position;

        RotateToVelocity();

        if (attacker != null && attacker.CompareTag("Player"))
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null && player.TryGetComponent(out Collider2D playerCollider))
                Physics2D.IgnoreCollision(GetComponent<Collider2D>(), playerCollider);
        }
    }

    private void FixedUpdate()
    {
        float delta = Time.fixedDeltaTime;
        Vector2 displacement = velocity * delta;

        RaycastHit2D hit = Physics2D.Raycast(
            rb.position,
            velocity.normalized,
            displacement.magnitude + skinWidth,
            collisionMask
        );

        bool hasValidHit = hit && CanCollide(hit.collider);

        if (hasValidHit)
        {
            if (TryApplyDamage(hit.collider, out bool consumeProjectile))
            {
                if (consumeProjectile)
                {
                    ReturnToPool();
                    return;
                }
            }

            if (remainingBounces > 0)
            {
                remainingBounces--;

                Vector2 normal = hit.normal;
                velocity = Vector2.Reflect(velocity, normal);

                rb.position = hit.point + normal * skinWidth;

                RotateToVelocity();
            }
            else
            {
                ReturnToPool();
                return;
            }
        }

        rb.MovePosition(rb.position + velocity * delta);

        RotateToVelocity();

        lifeRemaining -= delta;
        if (lifeRemaining <= 0f)
            ReturnToPool();
    }

    private void RotateToVelocity()
    {
        if (velocity.sqrMagnitude <= 0.0001f)
            return;

        float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg + spriteAngleOffset;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private bool TryApplyDamage(Collider2D other, out bool consumeProjectile)
    {
        consumeProjectile = true;

        if (!CanCollide(other))
            return false;

        if (!other.TryGetComponent(out IDamageable damageable))
            return false;

        damageable.TakeDamage(damage, attacker);

        if (other.TryGetComponent<INonConsumableHit>(out _))
            consumeProjectile = false;

        return true;
    }


    private bool CanCollide(Collider2D other)
    {
        if (attacker != null)
        {
            if (other.gameObject == attacker)
                return false;

            if (other.transform.root == attacker.transform)
                return false;

            if (attacker.CompareTag("Player") && other.CompareTag("Player"))
                return false;

            if (attacker.CompareTag("Enemy") && other.CompareTag("Enemy"))
                return false;
        }

        return true;
    }

    private void ReturnToPool()
    {
        velocity = Vector2.zero;

        if (spawner != null)
            spawner.Despawn(this);
        else
            gameObject.SetActive(false);
    }
}
