using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
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
    
    private SpriteRenderer spriteRenderer;
    private DoorView doorView;
    private DoorSpriteSet activeSprites;
    private bool hasStarted;
    public bool isOpen { get; private set; }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        doorView = GetComponent<DoorView>();
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
        roomClearedEvent.Register(Open);
    }

    private void OnDisable()
    {
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
        
        blockCollider.enabled = false;
        triggerCollider.enabled = true;

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
        
        blockCollider.enabled = true;
        triggerCollider.enabled = false;

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

    public void SyncFromView()
    {
        DoorDirection direction = doorView != null ? doorView.direction : DoorDirection.Up;
        ApplyDirectionalSprites(direction);
    }

    private void ApplyDirectionalSprites(DoorDirection direction)
    {
        activeSprites = direction switch
        {
            DoorDirection.Up => upSprites,
            DoorDirection.Down => downSprites,
            DoorDirection.Left => leftSprites,
            DoorDirection.Right => rightSprites,
            _ => null
        };

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

        triggerCollider.enabled = false;
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
