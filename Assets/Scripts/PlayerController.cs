using Injections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using Zenject;

[DisallowMultipleComponent]
public class PlayerController : MonoBehaviour
{
    private const float MovementEpsilon = 0.05f;
    private const float Default3DColliderRadius = 0.18f;
    private const float Default3DColliderHeight = 1.2f;
    private const float Default3DGroundHeight = 0f;
    private const float MinScaleComponent = 0.0001f;

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

    private float currentMaxSpeed;
    private float currentAttackSpeed;
    private float currentDashSpeed;
    private float currentDashDuration;
    private float currentDashCooldown;
    private float currentAcceleration;
    private float currentDeceleration;
    private float currentTurnAcceleration;

    private Rigidbody2D rb;
    private Rigidbody rb3D;
    private CapsuleCollider capsule3D;
    private NavMeshAgent navMeshAgent;
    private RoomWorldSpaceSettings worldSpaceSettings;
    private IPlayerTransformRegistry playerTransformRegistry;
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
        set => currentMaxSpeed = Mathf.Max(0f, value);
    }

    private void Awake()
    {
        EnsureGameplaySupportComponents();
        playerDataService = RuntimeServiceFallback.PlayerDataService;
        rb = GetComponent<Rigidbody2D>();
        rb3D = GetComponent<Rigidbody>();
        capsule3D = GetComponent<CapsuleCollider>();
        navMeshAgent = GetComponent<NavMeshAgent>();
        worldSpaceSettings = ResolveWorldSpaceSettings();
        ConfigureMovementComponents();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (statResolver == null)
            statResolver = GetComponent<CharacterStatResolver>();

        RefreshRuntimeStats();
        RefreshMovementTagState();
        Room2_5DPresentationUtility.EnsureDepthSorting(gameObject, Room2_5DRenderPreset.Character);
        EnsureXZVisualProxy();
        RegisterAsCurrentPlayerTransform();
    }

    private void Start()
    {
        RefreshMovementTagState();
        transform.position = ClampToGameplayPlane(transform.position);
    }

    private void OnEnable()
    {
        EnsureGameplaySupportComponents();
        rb = GetComponent<Rigidbody2D>();
        rb3D = GetComponent<Rigidbody>();
        capsule3D = GetComponent<CapsuleCollider>();
        navMeshAgent = GetComponent<NavMeshAgent>();
        worldSpaceSettings = ResolveWorldSpaceSettings();
        ConfigureMovementComponents();

        if (statResolver == null)
            statResolver = GetComponent<CharacterStatResolver>();

        if (statResolver != null)
            statResolver.StatsChanged += HandleStatsChanged;

        RefreshRuntimeStats();
        RefreshMovementTagState();
        Room2_5DPresentationUtility.EnsureDepthSorting(gameObject, Room2_5DRenderPreset.Character);
        EnsureXZVisualProxy();
        RegisterAsCurrentPlayerTransform();
    }

    private void OnDisable()
    {
        playerTransformRegistry?.Unregister(transform);

        if (statResolver != null)
            statResolver.StatsChanged -= HandleStatsChanged;

        if (flyingCollisionBypassActive)
            ApplyFlyingCollisionBypass(false);
    }

    [Inject]
    private void Construct([InjectOptional] IPlayerTransformRegistry injectedPlayerTransformRegistry)
    {
        playerTransformRegistry = injectedPlayerTransformRegistry;
        RegisterAsCurrentPlayerTransform();
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
        if (Uses3DMovement() || collision == null)
            return;

        HandleFlyingCollisionCandidate(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (Uses3DMovement() || collision == null)
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
            ApplyMovementVelocity(Vector2.zero, deltaTime);
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

        ApplyMovementVelocity(currentVelocity, deltaTime);
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

        if (navMeshAgent != null)
        {
            navMeshAgent.speed = Mathf.Max(currentMaxSpeed, Mathf.Max(currentDashSpeed, 0.01f));
            navMeshAgent.acceleration = Mathf.Max(currentAcceleration, Mathf.Max(currentTurnAcceleration, 0.01f));
        }
    }

    private bool IsMeleeAttackSelected()
    {
        if (playerDataService == null)
            return true;

        return playerDataService.SelectedAttack == AttackType.Melee;
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

        if (rb3D != null)
        {
            rb3D.linearVelocity = Vector3.zero;
            rb3D.angularVelocity = Vector3.zero;
            rb3D.position = ClampToGameplayPlane(rb3D.position);
        }

        if (navMeshAgent != null && navMeshAgent.enabled)
        {
            if (navMeshAgent.isOnNavMesh)
                navMeshAgent.ResetPath();

            navMeshAgent.nextPosition = ClampToGameplayPlane(transform.position);
        }
    }

    private void HandleStatsChanged()
    {
        RefreshRuntimeStats();
        RefreshMovementTagState();
    }

    private void RefreshMovementTagState()
    {
        IReadOnlyList<GameplayTag> effectiveTags = ResolveEffectiveTags();

        if (Uses3DMovement())
        {
            if (navMeshAgent == null)
                navMeshAgent = GetComponent<NavMeshAgent>();

            if (navMeshAgent != null && navMeshAgent.enabled)
            {
                if (navMeshAgent.isOnNavMesh)
                    navMeshAgent.ResetPath();

                navMeshAgent.enabled = false;
            }

            if (flyingCollisionBypassActive)
                ApplyFlyingCollisionBypass(false);

            return;
        }

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
        if (Uses3DMovement())
        {
            flyingCollisionBypassActive = false;
            RestoreIgnoredFlyingColliders();
            return;
        }

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

        if (Uses3DMovement())
            return;

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
        BreakableBase[] breakables = FindObjectsByType<BreakableBase>(FindObjectsSortMode.None);
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
        if (Uses3DMovement())
            return;

        int playerLayer = gameObject.layer;

        Physics2D.IgnoreLayerCollision(playerLayer, 0, false);

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

    private Vector3 ResolveFallbackMovementDelta(float deltaTime)
    {
        RoomWorldSpaceSettings worldSpace = ResolveWorldSpaceSettings();
        if (worldSpace != null && worldSpace.UsesXZPlane)
            return new Vector3(currentVelocity.x, 0f, currentVelocity.y) * deltaTime;

        return new Vector3(currentVelocity.x, currentVelocity.y, 0f) * deltaTime;
    }

    private void ConfigureMovementComponents()
    {
        if (Uses3DMovement())
        {
            Configure3DMovement();
            return;
        }

        Configure2DMovement();
    }

    private void Configure2DMovement()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();

        rb.simulated = true;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.useFullKinematicContacts = false;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        rb.linearVelocity = Vector2.zero;

        if (navMeshAgent != null && navMeshAgent.enabled)
        {
            if (navMeshAgent.isOnNavMesh)
                navMeshAgent.ResetPath();

            navMeshAgent.enabled = false;
        }

        EnsureCriticalWallLayerCollisionsEnabled();
    }

    private void Configure3DMovement()
    {
        RemoveRoot2DPhysicsComponentsFor3D();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        if (navMeshAgent == null)
            navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent != null && navMeshAgent.enabled)
        {
            if (navMeshAgent.isOnNavMesh)
                navMeshAgent.ResetPath();

            navMeshAgent.enabled = false;
        }

        if (rb3D == null)
            rb3D = GetComponent<Rigidbody>();
        if (rb3D == null)
            rb3D = gameObject.AddComponent<Rigidbody>();

        if (capsule3D == null)
            capsule3D = GetComponent<CapsuleCollider>();
        if (capsule3D == null)
            capsule3D = gameObject.AddComponent<CapsuleCollider>();

        if (rb3D == null || capsule3D == null)
            return;

        rb3D.useGravity = false;
        rb3D.isKinematic = false;
        rb3D.interpolation = RigidbodyInterpolation.Interpolate;
        rb3D.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb3D.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        rb3D.linearVelocity = Vector3.zero;
        rb3D.angularVelocity = Vector3.zero;

        capsule3D.isTrigger = false;
        capsule3D.direction = 1;
        capsule3D.radius = ResolveLocal3DColliderRadius();
        capsule3D.height = ResolveLocal3DColliderHeight(capsule3D.radius);
        capsule3D.center = new Vector3(0f, capsule3D.height * 0.5f, 0f);

        Vector3 snappedPosition = ClampToGameplayPlane(transform.position);
        transform.position = snappedPosition;
        rb3D.position = snappedPosition;
    }

    private void ApplyMovementVelocity(Vector2 planarVelocity, float deltaTime)
    {
        if (Uses3DMovement())
        {
            Apply3DMovement(planarVelocity, deltaTime);
            return;
        }

        if (rb != null)
            rb.linearVelocity = planarVelocity;
        else
            transform.position += ResolveFallbackMovementDelta(deltaTime);
    }

    private void Apply3DMovement(Vector2 planarVelocity, float deltaTime)
    {
        Vector3 currentPosition = ClampToGameplayPlane(rb3D != null ? rb3D.position : transform.position);
        Vector3 targetPosition = currentPosition;

        if (planarVelocity.sqrMagnitude > MovementEpsilon * MovementEpsilon)
            targetPosition += ToWorldMovementDelta(planarVelocity * deltaTime);

        targetPosition = ClampToGameplayPlane(targetPosition);

        if (rb3D != null)
        {
            rb3D.linearVelocity = Vector3.zero;
            rb3D.angularVelocity = Vector3.zero;
            rb3D.MovePosition(targetPosition);
            return;
        }

        transform.position = targetPosition;
    }

    private RoomWorldSpaceSettings ResolveWorldSpaceSettings()
    {
        if (worldSpaceSettings == null)
        {
            worldSpaceSettings = RoomWorldSpaceSettings.Current != null
                ? RoomWorldSpaceSettings.Current
                : FindFirstObjectByType<RoomWorldSpaceSettings>();
        }

        return worldSpaceSettings;
    }

    private bool Uses3DMovement()
    {
        RoomWorldSpaceSettings settings = ResolveWorldSpaceSettings();
        return settings != null && settings.UsesXZPlane;
    }

    public Vector3 GetClampedTeleportPosition(Vector3 position)
    {
        return ClampToGameplayPlane(position);
    }

    private void RemoveRoot2DPhysicsComponentsFor3D()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            DestroyImmediate(rb);
            rb = null;
        }
    }

    private void EnsureXZVisualProxy()
    {
        SpriteRenderer rootRenderer = GetComponent<SpriteRenderer>();
        if (rootRenderer == null)
            return;

        XZSpriteVisualProxy proxy = GetComponent<XZSpriteVisualProxy>();
        bool use3DPresentation = Uses3DMovement();

        if (use3DPresentation)
        {
            if (proxy == null)
                proxy = gameObject.AddComponent<XZSpriteVisualProxy>();

            proxy.ConfigureRuntimeAnchoring(0.02f, 0.08f);
            proxy.enabled = true;
            proxy.Sync();
        }
        else if (proxy != null)
        {
            proxy.enabled = false;
            Transform visualChild = transform.Find("XZVisualSprite");
            if (visualChild != null)
                visualChild.gameObject.SetActive(false);
        }

        rootRenderer.enabled = !use3DPresentation;

        BillboardFacingCamera billboard = GetComponent<BillboardFacingCamera>();
        if (billboard != null)
            billboard.enabled = false;

        SpriteDepthSorter sorter = GetComponent<SpriteDepthSorter>();
        if (sorter != null)
        {
            sorter.RefreshRenderTargets();
            sorter.ApplySorting();
        }
    }

    private void EnsureGameplaySupportComponents()
    {
        if (GetComponent<Interactor>() == null)
            gameObject.AddComponent<Interactor>();

        if (GetComponent<PlayerInteractionInput>() == null)
            gameObject.AddComponent<PlayerInteractionInput>();

        if (GetComponent<PlayerInventoryInput>() == null)
            gameObject.AddComponent<PlayerInventoryInput>();
    }

    private void RegisterAsCurrentPlayerTransform()
    {
        playerTransformRegistry?.Register(transform);
    }

    private float ResolveLocal3DColliderRadius()
    {
        Vector3 lossyScale = transform.lossyScale;
        float horizontalScale = Mathf.Max(
            Mathf.Abs(lossyScale.x),
            Mathf.Abs(lossyScale.z),
            MinScaleComponent);

        return Default3DColliderRadius / horizontalScale;
    }

    private float ResolveLocal3DColliderHeight(float localRadius)
    {
        Vector3 lossyScale = transform.lossyScale;
        float verticalScale = Mathf.Max(Mathf.Abs(lossyScale.y), MinScaleComponent);
        float localHeight = Default3DColliderHeight / verticalScale;
        return Mathf.Max(localHeight, (localRadius * 2f) + 0.01f);
    }

    private Vector3 ClampToGameplayPlane(Vector3 position)
    {
        RoomWorldSpaceSettings settings = ResolveWorldSpaceSettings();
        if (settings != null)
        {
            position = settings.ClampToWalkPlane(position);
            if (settings.UsesXZPlane)
                position.y = settings.Origin.y + settings.OrthogonalAxisOffset + Default3DGroundHeight;

            return position;
        }

        position.z = 0f;
        return position;
    }

    private Vector3 ToWorldMovementDelta(Vector2 planarDelta)
    {
        RoomWorldSpaceSettings settings = ResolveWorldSpaceSettings();
        if (settings != null && settings.UsesXZPlane)
            return new Vector3(planarDelta.x, 0f, planarDelta.y);

        return new Vector3(planarDelta.x, planarDelta.y, 0f);
    }
}
