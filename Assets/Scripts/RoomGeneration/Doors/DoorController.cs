using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(DoorView))]
public class DoorController : MonoBehaviour
{
    [Header("Directional Sprites")]
    [SerializeField] private DoorSpriteSet upSprites;
    [SerializeField] private DoorSpriteSet downSprites;
    [SerializeField] private DoorSpriteSet leftSprites;
    [SerializeField] private DoorSpriteSet rightSprites;
    
    
    [Header("Events")]
    [SerializeField] private RoomClearedEvent roomClearedEvent;
    [SerializeField] private DoorEnteredEvent doorEnteredEvent;
    
    [Header("Colliders")]
    [SerializeField] private Collider2D triggerCollider;
    [SerializeField] private Collider2D blockCollider;
    [SerializeField] private Collider triggerCollider3D;
    [SerializeField] private Collider blockCollider3D;
    
    private SpriteRenderer spriteRenderer;
    private DoorView doorView;
    private DoorSpriteSet activeSprites;
    private bool hasStarted;
    public bool isOpen { get; private set; }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        doorView = GetComponent<DoorView>();
        ResolveRuntimeReferences();
        SyncFromView();
        CloseInternal(notifyWorldChanged: false);
        //Open();
    }

    private void Start()
    {
        hasStarted = true;
    }

    private void OnEnable()
    {
        if (roomClearedEvent != null)
            roomClearedEvent.Register(Open);
    }

    private void OnDisable()
    {
        if (roomClearedEvent != null)
            roomClearedEvent.Unregister(Open);
    }

    private void Open()
    {
        OpenInternal(notifyWorldChanged: true);
    }

    private void OpenInternal(bool notifyWorldChanged)
    {
        if(isOpen) return;
        
        isOpen = true;
        RefreshSprite();
        ApplyColliderState(isOpen);

        if (notifyWorldChanged)
            RequestNavMeshRefresh();
    }

    private void Close()
    {
        CloseInternal(notifyWorldChanged: true);
    }

    private void CloseInternal(bool notifyWorldChanged)
    {
        if (!isOpen && blockCollider != null && blockCollider.enabled && triggerCollider != null && !triggerCollider.enabled)
            return;

        isOpen = false;
        RefreshSprite();
        ApplyColliderState(isOpen);

        if (notifyWorldChanged)
            RequestNavMeshRefresh();
    }

    public void SetOpenState(bool open)
    {
        if (open)
            OpenInternal(notifyWorldChanged: true);
        else
            CloseInternal(notifyWorldChanged: true);
    }

    public void ApplySpriteSets(DoorSpriteSet up, DoorSpriteSet down, DoorSpriteSet left, DoorSpriteSet right)
    {
        if (HasSprites(up)) upSprites = up;
        if (HasSprites(down)) downSprites = down;
        if (HasSprites(left)) leftSprites = left;
        if (HasSprites(right)) rightSprites = right;

        SyncFromView();
    }

    public void ConfigureRuntime3DColliders(Collider block, Collider trigger)
    {
        blockCollider3D = block;
        triggerCollider3D = trigger;
        ApplyColliderState(isOpen);
    }

    public Sprite GetClosedSprite(DoorDirection direction)
    {
        DoorSpriteSet set = ResolveSpriteSet(direction);
        if (set != null && set.closed != null)
            return set.closed;

        DoorSpriteSet fallback = GetFallbackSprites();
        return fallback != null ? fallback.closed : null;
    }

    public void SyncFromView()
    {
        DoorDirection direction = doorView != null ? doorView.direction : DoorDirection.Up;
        ApplyDirectionalSprites(direction);
    }

    private void ApplyDirectionalSprites(DoorDirection direction)
    {
        activeSprites = ResolveSpriteSet(direction);

        if (!HasSprites(activeSprites))
            activeSprites = GetFallbackSprites();

        RefreshSprite();
    }

    private DoorSpriteSet GetFallbackSprites()
    {
        if (HasSprites(upSprites)) return upSprites;
        if (HasSprites(downSprites)) return downSprites;
        if (HasSprites(leftSprites)) return leftSprites;
        if (HasSprites(rightSprites)) return rightSprites;
        return null;
    }

    private void RefreshSprite()
    {
        if (spriteRenderer == null)
            return;

        Sprite sprite = isOpen
            ? activeSprites != null ? activeSprites.open : null
            : activeSprites != null ? activeSprites.closed : null;

        spriteRenderer.sprite = sprite;
    }

    private static bool HasSprites(DoorSpriteSet set)
    {
        return set != null && (set.closed != null || set.open != null);
    }

    private DoorSpriteSet ResolveSpriteSet(DoorDirection direction)
    {
        return direction switch
        {
            DoorDirection.Up => upSprites,
            DoorDirection.Down => downSprites,
            DoorDirection.Left => leftSprites,
            DoorDirection.Right => rightSprites,
            _ => null
        };
    }

    private void ResolveRuntimeReferences()
    {
        if (triggerCollider == null || blockCollider == null)
        {
            Collider2D[] colliders2D = GetComponents<Collider2D>();
            for (int i = 0; i < colliders2D.Length; i++)
            {
                Collider2D collider2D = colliders2D[i];
                if (collider2D == null)
                    continue;

                if (collider2D.isTrigger)
                    triggerCollider ??= collider2D;
                else
                    blockCollider ??= collider2D;
            }
        }

        if (triggerCollider3D == null || blockCollider3D == null)
        {
            Collider[] colliders3D = GetComponents<Collider>();
            for (int i = 0; i < colliders3D.Length; i++)
            {
                Collider collider3D = colliders3D[i];
                if (collider3D == null)
                    continue;

                if (collider3D.isTrigger)
                    triggerCollider3D ??= collider3D;
                else
                    blockCollider3D ??= collider3D;
            }
        }
    }

    private void ApplyColliderState(bool open)
    {
        if (blockCollider != null)
            blockCollider.enabled = !open;
        if (triggerCollider != null)
            triggerCollider.enabled = open;
        if (blockCollider3D != null)
            blockCollider3D.enabled = !open;
        if (triggerCollider3D != null)
            triggerCollider3D.enabled = open;
    }

    private void RequestNavMeshRefresh()
    {
        if (!Application.isPlaying || !hasStarted)
            return;

        WorldChangedEvent.Raise();
    }
    
    
    //Player entered the door
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isOpen) return;
        if (!other.CompareTag("Player")) return;

        if (triggerCollider != null)
            triggerCollider.enabled = false;
        doorEnteredEvent?.Raise();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isOpen) return;
        if (!other.CompareTag("Player")) return;

        if (triggerCollider3D != null)
            triggerCollider3D.enabled = false;
        doorEnteredEvent?.Raise();
    }

    
    // ───────── TESTING ─────────

    [ContextMenu("TEST/Open Door")]
    private void TestOpen()
    {
        Open();
    }

    [ContextMenu("TEST/Close Door")]
    private void TestClose()
    {
        Close();
    }
}
