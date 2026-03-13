using Injections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Combat")]
    [SerializeField] private bool useAttackLockTime = true;

    private float attackCooldownTimer;
    private bool isAttacking;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 14f;
    [SerializeField] private float dashDuration = 0.12f;
    [SerializeField] private float dashCooldown = 0.6f;
    [SerializeField] private bool dashUsesFacingIfNoInput = true;

    private bool isDashing;
    private float dashTimeLeft;
    private float dashCooldownLeft;
    private Vector2 dashDir;
    private bool wasMoving;

    private const float MovementEpsilon = 0.05f;

    [Header("Movement")]
    [SerializeField] private float minMoveSpeed = 2.5f;
    [SerializeField] private float maxMoveSpeed = 8.5f;
    [SerializeField] private float acceleration = 60f;
    [SerializeField] private float deceleration = 70f;
    [SerializeField] private float turnAcceleration = 90f;
    [SerializeField] private bool lockTo4Directions = true;

    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Header("Stats")]
    [SerializeField] private CharacterStatResolver statResolver;
    [SerializeField] private PlayerAudios playerAudios;

    [Header("Flying Collision Rules")]
    [SerializeField] private float flyingCollisionRefreshInterval = 0.25f;

    [Header("Events")]
    [SerializeField] private PlayerAudioMovingEvent playerAudioMovingEvent;
    [SerializeField] private PlayerAudioStopEvent playerAudioStopEvent;
    [SerializeField] private PlayerAudioAttackingEvent playerAudioAttackingEvent;

    // Runtime stats
    private float currentMaxSpeed;
    private float currentAttackSpeed;
    private float currentDashSpeed;
    private float currentDashDuration;
    private float currentDashCooldown;
    private float currentAcceleration;
    private float currentDeceleration;
    private float currentTurnAcceleration;

    private Rigidbody2D rb;
    private IPlayerDataService playerDataService;
    private Vector2 rawInput;
    private Vector2 input;
    private Vector2 desiredVelocity;
    private Vector2 currentVelocity;
    private Vector2 lastDir = Vector2.down;
    private bool flyingCollisionBypassActive;
    private float flyingCollisionRefreshTimer;
    private readonly List<Collider2D> playerSolidColliders = new List<Collider2D>(4);
    private readonly HashSet<Collider2D> ignoredFlyingColliders = new HashSet<Collider2D>();

    public float CurrentHealth => ResolveStat(StatType.MaxHealth, 100f);

    public float CurrentDamage
    {
        get => ResolveStat(StatType.AttackDamage, 10f);
        set => ResolveStat(StatType.AttackDamage, value);
    }

    public float CurrentArmor => ResolveStat(StatType.ShieldArmor, 0f);
    public float CurrentAttackSpeed => currentAttackSpeed;

    public float CurrentSpeed
    {
        get => currentMaxSpeed;
        set
        {
            currentMaxSpeed = Mathf.Max(0f, value);
        }
    }

    private void Awake()
    {
        playerDataService = RuntimeServiceFallback.PlayerDataService;
        DisableNavMeshAgentIfPresent();

        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.useFullKinematicContacts = false;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        rb.linearVelocity = Vector2.zero;

        EnsureCriticalWallLayerCollisionsEnabled();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (statResolver == null)
            statResolver = GetComponent<CharacterStatResolver>();

        RefreshRuntimeStats();
        RefreshMovementTagState();
    }

    private void Start()
    {
        // Ensure tags modified during other Awake calls are reflected.
        RefreshMovementTagState();
    }

    private void OnEnable()
    {
        DisableNavMeshAgentIfPresent();
        EnsureCriticalWallLayerCollisionsEnabled();

        if (statResolver == null)
            statResolver = GetComponent<CharacterStatResolver>();

        if (statResolver != null)
            statResolver.StatsChanged += HandleStatsChanged;

        RefreshRuntimeStats();
        RefreshMovementTagState();
    }

    private void OnDisable()
    {
        if (statResolver != null)
            statResolver.StatsChanged -= HandleStatsChanged;

        if (flyingCollisionBypassActive)
            ApplyFlyingCollisionBypass(false);
    }

    private void Update()
    {
        if (GameplayUIState.IsGameplayInputBlocked)
            rawInput = Vector2.zero;

        input = lockTo4Directions ? ToCardinal(rawInput) : rawInput;

        if (input != Vector2.zero)
            lastDir = input.normalized;

        desiredVelocity = input * currentMaxSpeed;

        if (useAttackLockTime && attackCooldownTimer > 0f)
        {
            attackCooldownTimer -= Time.deltaTime;
            if (attackCooldownTimer <= 0f)
                isAttacking = false;
        }

        if (dashCooldownLeft > 0f)
            dashCooldownLeft -= Time.deltaTime;

        if (isDashing)
        {
            dashTimeLeft -= Time.deltaTime;
            if (dashTimeLeft <= 0f)
                isDashing = false;
        }

        UpdateAnimator(input);
        RefreshRuntimeTagAndFlyingCollisions(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        UpdateMovement(Time.fixedDeltaTime);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null)
            return;

        HandleFlyingCollisionCandidate(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision == null)
            return;

        HandleFlyingCollisionCandidate(collision.collider);
    }

    private void OnMove(InputValue value)
    {
        if (GameplayUIState.IsGameplayInputBlocked)
            return;

        rawInput = value.Get<Vector2>();
    }

    private void OnAttack(InputValue value)
    {
        if (GameplayUIState.IsGameplayInputBlocked)
            return;

        if (!IsMeleeAttackSelected())
            return;

        TryAttack();
    }

    private void OnSprint(InputValue value)
    {
        if (GameplayUIState.IsGameplayInputBlocked)
            return;

        if (value.isPressed)
            TryDash();
    }

    private void TryAttack()
    {
        if (attackCooldownTimer > 0f)
            return;

        float attackSpeed = Mathf.Max(0.01f, currentAttackSpeed);

        if (useAttackLockTime)
        {
            float baseLockTime = statResolver != null
                ? statResolver.Get(StatType.AttackLockTime, 0.2f)
                : 0.2f;

            attackCooldownTimer = baseLockTime / attackSpeed;
            isAttacking = true;
            playerAudioAttackingEvent?.Raise(playerAudios.AttackAudio);
        }

        if (animator != null)
        {
            animator.SetFloat("AttackSpeed", attackSpeed);
            animator.SetTrigger("Attack");
        }
    }

    private void TryDash()
    {
        if (!ResolveRule(PlayerRuleType.CanDash, true))
            return;

        if (isDashing || dashCooldownLeft > 0f)
            return;

        Vector2 dir = input == Vector2.zero && dashUsesFacingIfNoInput
            ? lastDir
            : input;

        if (dir == Vector2.zero)
            return;

        dashDir = dir.normalized;
        lastDir = dashDir;

        isDashing = true;
        dashTimeLeft = currentDashDuration;
        dashCooldownLeft = currentDashCooldown;

        if (animator != null)
            animator.SetTrigger("Dash");
    }

    private void UpdateMovement(float deltaTime)
    {
        if (useAttackLockTime && isAttacking)
        {
            currentVelocity = Vector2.zero;
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 targetVelocity;
        float accelRate;

        if (isDashing)
        {
            targetVelocity = dashDir * currentDashSpeed;
            accelRate = currentTurnAcceleration;
        }
        else if (desiredVelocity.sqrMagnitude > 0.01f)
        {
            bool turning = currentVelocity.sqrMagnitude > 0.01f &&
                           Vector2.Dot(currentVelocity.normalized, desiredVelocity.normalized) < 0.7f;

            accelRate = turning ? currentTurnAcceleration : currentAcceleration;
            targetVelocity = desiredVelocity;
        }
        else
        {
            accelRate = currentDeceleration;
            targetVelocity = Vector2.zero;
        }

        currentVelocity = Vector2.MoveTowards(
            currentVelocity,
            targetVelocity,
            accelRate * deltaTime);

        bool hasMovement = currentVelocity.sqrMagnitude > MovementEpsilon * MovementEpsilon;
        if (hasMovement)
            lastDir = currentVelocity.normalized;

        if (rb != null)
            rb.linearVelocity = currentVelocity;
        else
            transform.position += new Vector3(currentVelocity.x, currentVelocity.y, 0f) * deltaTime;
    }

    private static Vector2 ToCardinal(Vector2 v)
    {
        if (v == Vector2.zero)
            return Vector2.zero;

        return Mathf.Abs(v.x) > Mathf.Abs(v.y)
            ? new Vector2(Mathf.Sign(v.x), 0f)
            : new Vector2(0f, Mathf.Sign(v.y));
    }

    private void UpdateAnimator(Vector2 moveDir)
    {
        bool isMoving =
            !isDashing &&
            !isAttacking &&
            currentVelocity.sqrMagnitude > MovementEpsilon * MovementEpsilon;

        if (isMoving && !wasMoving)
        {
            playerAudioMovingEvent?.Raise(playerAudios.StepsAudio);
        }
        else if (!isMoving && wasMoving)
        {
            playerAudioStopEvent?.Raise();
        }

        wasMoving = isMoving;

        Vector2 facing = moveDir != Vector2.zero ? moveDir : lastDir;

        if (animator != null)
        {
            animator.SetBool("IsMoving", isMoving);
            animator.SetFloat("MoveX", facing.x);
            animator.SetFloat("MoveY", facing.y);
        }
    }

    private float ResolveStat(StatType type, float fallback)
    {
        return statResolver != null ? statResolver.Get(type, fallback) : fallback;
    }

    private bool ResolveRule(PlayerRuleType type, bool fallback)
    {
        PactManager manager = PactManager.Instance;
        if (manager == null || manager.PlayerRules == null)
            return fallback;

        return manager.PlayerRules.Get(type, fallback, statResolver != null ? statResolver.Tags : null);
    }

    private float ResolveMoveSpeed(float statValue)
    {
        float min = Mathf.Min(minMoveSpeed, maxMoveSpeed);
        float max = Mathf.Max(minMoveSpeed, maxMoveSpeed);
        return Mathf.Clamp(statValue, min, max);
    }

    private void RefreshRuntimeStats()
    {
        float moveStat = ResolveStat(StatType.MaxSpeed, 3.5f);
        currentMaxSpeed = ResolveMoveSpeed(moveStat);

        currentAttackSpeed = ResolveStat(StatType.AttackSpeed, 1f);

        currentDashSpeed = ResolveStat(StatType.DashSpeed, dashSpeed);
        currentDashDuration = ResolveStat(StatType.DashDuration, dashDuration);
        currentDashCooldown = ResolveStat(StatType.DashCooldown, dashCooldown);

        currentAcceleration = ResolveStat(StatType.Acceleration, acceleration);
        currentDeceleration = ResolveStat(StatType.Deceleration, deceleration);
        currentTurnAcceleration = ResolveStat(StatType.TurnAcceleration, turnAcceleration);
    }

    private bool IsMeleeAttackSelected()
    {
        if (playerDataService == null)
            return true;

        return playerDataService.SelectedAttack == AttackType.Melee;
    }

    private void DisableNavMeshAgentIfPresent()
    {
        if (!TryGetComponent(out NavMeshAgent agent) || !agent.enabled)
            return;

        if (agent.isOnNavMesh)
            agent.ResetPath();

        agent.enabled = false;
    }

    public void EndAttack()
    {
        isAttacking = false;
    }

    public void ResetMotionForTeleport()
    {
        rawInput = Vector2.zero;
        input = Vector2.zero;
        desiredVelocity = Vector2.zero;
        currentVelocity = Vector2.zero;
        isDashing = false;
        dashTimeLeft = 0f;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    private void HandleStatsChanged()
    {
        RefreshRuntimeStats();
        RefreshMovementTagState();
    }

    private void RefreshMovementTagState()
    {
        IReadOnlyList<GameplayTag> effectiveTags = ResolveEffectiveTags();
        UpdateFlyingCollisionBypass(effectiveTags);
    }

    private IReadOnlyList<GameplayTag> ResolveEffectiveTags()
    {
        IReadOnlyList<GameplayTag> effectiveTags = statResolver != null ? statResolver.Tags : null;
        PactManager manager = PactManager.Instance;
        if (manager != null && manager.Stats != null)
            effectiveTags = manager.Stats.GetEffectiveTags(effectiveTags);

        return effectiveTags;
    }

    private void UpdateFlyingCollisionBypass(IReadOnlyList<GameplayTag> effectiveTags)
    {
        bool shouldBypass = HasTagNamed(effectiveTags, "Flying") || HasTagNamed(effectiveTags, "Floating");
        if (flyingCollisionBypassActive == shouldBypass)
            return;

        ApplyFlyingCollisionBypass(shouldBypass);
    }

    private void ApplyFlyingCollisionBypass(bool enabled)
    {
        flyingCollisionBypassActive = enabled;
        EnsureCriticalWallLayerCollisionsEnabled();

        if (enabled)
        {
            CollectPlayerSolidColliders();
            PrimeFlyingIgnoresForBreakables();
            flyingCollisionRefreshTimer = 0f;
            return;
        }

        RestoreIgnoredFlyingColliders();
    }

    private void RefreshRuntimeTagAndFlyingCollisions(float deltaTime)
    {
        flyingCollisionRefreshTimer -= deltaTime;
        if (flyingCollisionRefreshTimer > 0f)
            return;

        flyingCollisionRefreshTimer = Mathf.Max(0.05f, flyingCollisionRefreshInterval);
        RefreshMovementTagState();

        if (flyingCollisionBypassActive)
            ReconcileIgnoredFlyingColliders();
    }

    private void RestoreIgnoredFlyingColliders()
    {
        if (ignoredFlyingColliders.Count == 0)
            return;

        CollectPlayerSolidColliders();

        Collider2D[] snapshot = new Collider2D[ignoredFlyingColliders.Count];
        ignoredFlyingColliders.CopyTo(snapshot);
        ignoredFlyingColliders.Clear();

        for (int i = 0; i < snapshot.Length; i++)
        {
            Collider2D collider = snapshot[i];
            if (collider == null)
                continue;

            SetIgnoreWithPlayerSolidColliders(collider, false);
        }
    }

    private void HandleFlyingCollisionCandidate(Collider2D collider)
    {
        if (!flyingCollisionBypassActive || collider == null || !collider.enabled || collider.isTrigger)
            return;

        if (collider.transform == transform || collider.transform.IsChildOf(transform))
            return;

        bool shouldIgnore = !IsWallCollider(collider);
        bool isIgnored = ignoredFlyingColliders.Contains(collider);

        if (shouldIgnore && !isIgnored)
        {
            CollectPlayerSolidColliders();
            SetIgnoreWithPlayerSolidColliders(collider, true);
            ignoredFlyingColliders.Add(collider);
            return;
        }

        if (!shouldIgnore && isIgnored)
        {
            CollectPlayerSolidColliders();
            SetIgnoreWithPlayerSolidColliders(collider, false);
            ignoredFlyingColliders.Remove(collider);
        }
    }

    private void PrimeFlyingIgnoresForBreakables()
    {
        BreakableBase[] breakables = FindObjectsOfType<BreakableBase>();
        if (breakables == null || breakables.Length == 0)
            return;

        CollectPlayerSolidColliders();

        for (int i = 0; i < breakables.Length; i++)
        {
            BreakableBase breakable = breakables[i];
            if (breakable == null || !breakable.isActiveAndEnabled)
                continue;

            Collider2D[] colliders = breakable.GetComponentsInChildren<Collider2D>();
            for (int j = 0; j < colliders.Length; j++)
            {
                Collider2D collider = colliders[j];
                if (collider == null || !collider.enabled || collider.isTrigger)
                    continue;

                if (ignoredFlyingColliders.Contains(collider))
                    continue;

                SetIgnoreWithPlayerSolidColliders(collider, true);
                ignoredFlyingColliders.Add(collider);
            }
        }
    }

    private void ReconcileIgnoredFlyingColliders()
    {
        if (ignoredFlyingColliders.Count == 0)
            return;

        CollectPlayerSolidColliders();

        Collider2D[] snapshot = new Collider2D[ignoredFlyingColliders.Count];
        ignoredFlyingColliders.CopyTo(snapshot);
        for (int i = 0; i < snapshot.Length; i++)
        {
            Collider2D collider = snapshot[i];
            if (collider == null || !IsWallCollider(collider))
                continue;

            SetIgnoreWithPlayerSolidColliders(collider, false);
            ignoredFlyingColliders.Remove(collider);
        }
    }

    private void CollectPlayerSolidColliders()
    {
        playerSolidColliders.Clear();
        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
                continue;

            playerSolidColliders.Add(collider);
        }
    }

    private void SetIgnoreWithPlayerSolidColliders(Collider2D target, bool ignore)
    {
        for (int i = 0; i < playerSolidColliders.Count; i++)
        {
            Collider2D source = playerSolidColliders[i];
            if (source == null || target == null)
                continue;

            Physics2D.IgnoreCollision(source, target, ignore);
        }
    }

    private static bool IsWallCollider(Collider2D collider)
    {
        if (collider == null)
            return false;

        if (collider.TryGetComponent(out BreakableBase _))
            return false;

        if (collider.CompareTag("Door"))
            return true;

        if (collider is TilemapCollider2D || collider is CompositeCollider2D)
            return true;

        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleLayer >= 0 && collider.gameObject.layer == obstacleLayer)
            return true;

        Rigidbody2D attachedBody = collider.attachedRigidbody;
        if (attachedBody == null || attachedBody.bodyType == RigidbodyType2D.Static)
            return true;

        return false;
    }

    private void EnsureCriticalWallLayerCollisionsEnabled()
    {
        int playerLayer = gameObject.layer;

        Physics2D.IgnoreLayerCollision(playerLayer, 0, false); // Default

        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleLayer >= 0)
            Physics2D.IgnoreLayerCollision(playerLayer, obstacleLayer, false);
    }

    private static bool HasTagNamed(IReadOnlyList<GameplayTag> tags, string expectedTag)
    {
        if (tags == null || tags.Count == 0 || string.IsNullOrWhiteSpace(expectedTag))
            return false;

        string expectedWithSuffix = $"{expectedTag}Tag";

        for (int i = 0; i < tags.Count; i++)
        {
            GameplayTag tag = tags[i];
            if (tag == null)
                continue;

            if (string.Equals(tag.TagName, expectedTag, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag.TagName, expectedWithSuffix, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag.name, expectedTag, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag.name, expectedWithSuffix, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
