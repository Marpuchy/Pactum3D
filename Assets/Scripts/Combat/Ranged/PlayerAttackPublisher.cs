using Injections;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerAttackPublisher : MonoBehaviour
{
    [SerializeField] private AttackEventChannelSO attackEvent;
    [SerializeField] private Transform originOverride;
    [SerializeField] private Camera aimCamera;
    [SerializeField] private bool useMouseAim = true;
    [SerializeField] private float baseAttackSpeed = 1f;
    [SerializeField] private CharacterStatResolver statResolver;

    private float nextFireTime;
    private Vector2 lastAimDirection = Vector2.right;
    private IPlayerDataService playerDataService;

    private void Awake()
    {
        if (aimCamera == null)
            aimCamera = Camera.main;

        if (statResolver == null)
            statResolver = GetComponent<CharacterStatResolver>();

        playerDataService = RuntimeServiceFallback.PlayerDataService;
    }

    public void OnLook(InputValue value)
    {
        if (GameplayUIState.IsGameplayInputBlocked)
            return;

        Vector2 look = value.Get<Vector2>();
        if (look.sqrMagnitude > 0.0001f)
            lastAimDirection = look.normalized;
    }

    public void OnFire(InputValue value)
    {
        if (GameplayUIState.IsGameplayInputBlocked)
            return;

        if (!value.isPressed)
            return;

        if (!IsRangedAttackSelected())
            return;

        TryFire();
    }

    private void TryFire()
    {
        if (attackEvent == null)
            return;

        if (Time.time < nextFireTime)
            return;

        float attacksPerSecond = statResolver != null
            ? statResolver.Get(StatType.AttackSpeed, baseAttackSpeed)
            : baseAttackSpeed;

        float cooldown = 1f / Mathf.Max(0.01f, attacksPerSecond);
        nextFireTime = Time.time + cooldown;

        Vector2 origin = originOverride != null ? (Vector2)originOverride.position : (Vector2)transform.position;
        Vector2 direction = GetAimDirection(origin);
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        attackEvent.Raise(new AttackRequest(gameObject, origin, direction));
    }

    private bool IsRangedAttackSelected()
    {
        if (playerDataService == null)
            return false;

        return playerDataService.SelectedAttack == AttackType.Ranged;
    }

    private Vector2 GetAimDirection(Vector2 origin)
    {
        if (useMouseAim && aimCamera != null && Mouse.current != null)
        {
            Vector2 screenPos = Mouse.current.position.ReadValue();
            Vector3 world = aimCamera.ScreenToWorldPoint(screenPos);
            Vector2 dir = (Vector2)world - origin;
            if (dir.sqrMagnitude > 0.0001f)
                return dir.normalized;
        }

        if (lastAimDirection.sqrMagnitude > 0.0001f)
            return lastAimDirection.normalized;

        return transform.right;
    }
}
