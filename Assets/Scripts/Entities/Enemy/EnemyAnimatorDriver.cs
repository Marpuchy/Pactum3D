using UnityEngine;

[RequireComponent(typeof(HealthComponent))]
public class EnemyAnimatorDriver : MonoBehaviour
{
    private enum LocomotionMode
    {
        FlipX,
        FourDirections
    }

    // =============================
    // Animator hashes
    // =============================
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int SpeedMultiplierHash = Animator.StringToHash("SpeedMultiplier");
    private static readonly int AttackTriggerHash = Animator.StringToHash("Attack");
    private static readonly int HitTriggerHash = Animator.StringToHash("Hit");
    private static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    private static readonly int AttackStateHash = Animator.StringToHash("Attack");
    private static readonly int HitStateHash = Animator.StringToHash("Hit");
    private static readonly int DieStateHash = Animator.StringToHash("Die");
    private static readonly int LocomotionStateHash = Animator.StringToHash("Locomotion");

    // Top-down (4-dir) hashes
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");

    [Header("Locomotion Mode")]
    [SerializeField] private LocomotionMode locomotionMode = LocomotionMode.FlipX;

    [Header("Top-Down 4 Dir")]
    [SerializeField] private bool snapToFourDirections = true;
    [SerializeField] private float movingSpeedThreshold = 0.05f;
    [SerializeField] private Vector2 defaultFacing = Vector2.down;

    [Header("Hit")]
    [SerializeField] private float baseHitAnimDuration = 0.3f;
    [SerializeField] private float minHitAnimDuration = 0.5f;

    [Header("Attack")]
    [SerializeField] private float maxAttackAnimSpeed = 3.0f;
    [SerializeField] private float baseAttackAnimDuration = 0.4f;

    [Header("Movement Scaling (legacy)")]
    [SerializeField] private float referenceMoveSpeed = 3.5f;
    [SerializeField] private float minMultiplier = 0.8f;
    [SerializeField] private float maxMultiplier = 1.3f;

    [Header("Flip (legacy)")]
    [SerializeField] private bool facingRight = true;
    [SerializeField] private bool invertHorizontalFacing;
    [SerializeField] private Transform flipRoot;
    private Animator animator;
    private bool isDead;
    private float hitLockEndTime = -Mathf.Infinity;
    private bool hitLockActive;
    private float cachedAnimatorSpeed = 1f;

    private Vector2 lastFacing;

    // =============================
    // LIFECYCLE
    // =============================
    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();

        if (animator == null)
            Debug.LogError($"{name}: Animator not found", this);

        lastFacing = defaultFacing == Vector2.zero ? Vector2.down : defaultFacing;

        if (flipRoot == null)
            flipRoot = transform;
    }

    private void Update()
    {
        if (!hitLockActive) return;
        if (Time.time < hitLockEndTime) return;

        hitLockActive = false;
        if (animator != null)
        {
            animator.speed = cachedAnimatorSpeed;
            if (IsInHitState())
                animator.CrossFade(LocomotionStateHash, 0.05f, 0, 0f);
        }
    }
    
    public void OnDeath()
    {
        if (isDead) return;
        isDead = true;
        ClearHitLock();

        // ▶ Animación (forzar entrada inmediata)
        if (animator != null)
        {
            animator.speed = 1f;
            animator.ResetTrigger(AttackTriggerHash);
            animator.ResetTrigger(HitTriggerHash);
            animator.SetBool(IsDeadHash, true);
            animator.Play(DieStateHash, 0, 0f);
        }

        // 🛑 Parar movimiento físico
        if (TryGetComponent(out Rigidbody2D rb))
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        // 🧠 APAGAR CEREBRO
        DisableEnemyBrain();
    }
    public bool IsInAttackState()
    {
        if (animator == null) return false;
        return animator.GetCurrentAnimatorStateInfo(0).IsName("Attack");
    }
    public bool IsInHitState()
    {
        if (animator == null) return false;
        return animator.GetCurrentAnimatorStateInfo(0).IsName("Hit");
    }
    private void DisableEnemyBrain()
    {
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();

        foreach (var b in behaviours)
        {
            if (b == this) continue;
            if (b is EnemyAnimatorDriver) continue;

            b.enabled = false;
        }
    }


    

    // =============================
    // MOVEMENT (Idle / Run)
    // =============================
    public void SetMoveVelocity(Vector2 velocity)
    {
        if (isDead || animator == null) return;
        if (IsHitLocked()) return;

        switch (locomotionMode)
        {
            case LocomotionMode.FlipX:
                ApplyLegacyFlipLocomotion(velocity);
                break;

            case LocomotionMode.FourDirections:
                ApplyTopDownLocomotion(velocity);
                break;
        }
    }

    private void ApplyLegacyFlipLocomotion(Vector2 velocity)
    {
        float speed = velocity.magnitude;
        animator.SetFloat(SpeedHash, speed);

        float normalized = speed / Mathf.Max(referenceMoveSpeed, 0.01f);
        float multiplier = Mathf.Clamp(normalized, minMultiplier, maxMultiplier);
        animator.SetFloat(SpeedMultiplierHash, multiplier);

        if (velocity.x > 0.01f && !facingRight)
            Flip(true);
        else if (velocity.x < -0.01f && facingRight)
            Flip(false);
        ApplyHorizontalFacing(velocity.x, 0.01f);
    }

    public void FaceDirection(Vector2 direction)
    {
        if (isDead || animator == null)
            return;

        ApplyHorizontalFacing(direction.x, 0.001f);
    }

    private void ApplyHorizontalFacing(float horizontal, float threshold)
    {
        if (Mathf.Abs(horizontal) <= threshold)
            return;

        bool shouldFaceRight = horizontal > 0f;
        if (invertHorizontalFacing)
            shouldFaceRight = !shouldFaceRight;

        if (shouldFaceRight != facingRight)
            Flip(shouldFaceRight);
    }

    private void ApplyTopDownLocomotion(Vector2 velocity)
    {
        bool isMoving = velocity.sqrMagnitude > movingSpeedThreshold * movingSpeedThreshold;

        Vector2 facing;
        if (isMoving)
        {
            facing = snapToFourDirections ? ToCardinal(velocity) : velocity.normalized;
            if (facing != Vector2.zero)
                lastFacing = facing;
        }
        else
        {
            facing = lastFacing;
        }

        animator.SetBool(IsMovingHash, isMoving);
        animator.SetFloat(MoveXHash, facing.x);
        animator.SetFloat(MoveYHash, facing.y);

        // Opcional: si tu locomotion top-down también usa Speed para blending adicional,
        // puedes mantenerlo sin romper nada:
        // animator.SetFloat(SpeedHash, isMoving ? 1f : 0f);
    }

    private static Vector2 ToCardinal(Vector2 v)
    {
        if (v == Vector2.zero) return Vector2.zero;

        float ax = Mathf.Abs(v.x);
        float ay = Mathf.Abs(v.y);

        if (ax > ay)
            return new Vector2(Mathf.Sign(v.x), 0f);

        return new Vector2(0f, Mathf.Sign(v.y));
    }

    private void Flip(bool faceRight)
    {
        facingRight = faceRight;

        Vector3 scale = flipRoot.localScale;
        scale.x = Mathf.Abs(scale.x) * (facingRight ? 1 : -1);
        flipRoot.localScale = scale;
    }

    // =============================
    // ATTACK
    // =============================
    public void PlayHit()
    {
        if (isDead || animator == null) return;

        StartOrExtendHitLock();
        if (IsInHitState()) return;

        animator.ResetTrigger(AttackTriggerHash);
        animator.ResetTrigger(HitTriggerHash);
        animator.Play(HitStateHash, 0, 0f);
    }

    public void PlayAttack(float targetAnimDuration)
    {
        if (isDead || animator == null) return;
        if (IsHitLocked()) return;

        float safeBaseDuration = Mathf.Max(baseAttackAnimDuration, 0.01f);
        float safeTargetDuration = Mathf.Max(targetAnimDuration, 0.01f);

        float animSpeed = safeBaseDuration / safeTargetDuration;
        if (maxAttackAnimSpeed > 0f)
            animSpeed = Mathf.Min(animSpeed, maxAttackAnimSpeed);

        animSpeed = Mathf.Max(animSpeed, 0.01f);

        animator.SetFloat(AttackSpeedHash, animSpeed);
        animator.CrossFade(AttackStateHash, 0.02f, 0, 0f);
    }

    private void StartOrExtendHitLock()
    {
        float baseDuration = Mathf.Max(baseHitAnimDuration, 0.01f);
        float targetDuration = Mathf.Max(minHitAnimDuration, baseDuration);
        float hitSpeed = baseDuration / targetDuration;

        if (!hitLockActive)
            cachedAnimatorSpeed = animator.speed;

        animator.speed = hitSpeed;
        float newEndTime = Time.time + targetDuration;
        hitLockEndTime = hitLockActive ? Mathf.Max(hitLockEndTime, newEndTime) : newEndTime;
        hitLockActive = true;
    }

    public bool IsHitLocked()
    {
        return hitLockActive && Time.time < hitLockEndTime;
    }

    private void ClearHitLock()
    {
        if (!hitLockActive) return;

        hitLockActive = false;
        if (animator != null)
            animator.speed = cachedAnimatorSpeed;
    }

    // =============================
    // UTILITY
    // =============================
    public void FreezeAnimator()
    {
        if (animator != null)
            animator.speed = 0f;
    }
}
