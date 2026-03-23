using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

/// <summary>
/// Handles detection, selection, and execution of interactions for the player.
/// Keeps a list of nearby candidates and picks the best one based on distance
/// and facing before relaying input to it.
/// </summary>
public sealed class Interactor : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float maxDistance = 1.8f;
    [SerializeField] private LayerMask interactableMask = ~0;

    [Header("Events")]
    [SerializeField] private InteractionFocusEventChannelSO focusChannel;

    [Header("Inventory")]
    [SerializeField] private PlayerInventoryHolder inventoryHolder;
    [SerializeField] private PlayerMiniInventory miniInventory;

    [Header("Drop")]
    [SerializeField] private GameObject dropPrefab;
    [SerializeField] private Vector3 dropOffset = new Vector3(0f, -0.5f, 0f);
    [SerializeField] private float dropScatter = 0.2f;

    private IInstantiator instantiator;
    private readonly HashSet<IInteractable> _candidates = new();
    private IInteractable _current;
    private RoomWorldSpaceSettings worldSpaceSettings;
    private Collider interactionRangeCollider3D;
    private Collider2D interactionRangeCollider2D;

    /// <summary>
    /// Current interactable selected by the interactor or null if none.
    /// </summary>
    public IInteractable Current => _current;

    public Inventory Inventory => inventoryHolder != null ? inventoryHolder.Inventory : null;

    public bool AddItem(IItem item)
    {
        return AddItem(item, showFeedback: true);
    }

    public bool AddItem(IItem item, bool showFeedback)
    {
        if (item == null) return false;

        if (miniInventory == null)
            miniInventory = GetComponent<PlayerMiniInventory>();

        if (miniInventory != null && miniInventory.TryAbsorbPickup(item))
            return true;

        bool added = Inventory != null && Inventory.AddItem(item);
        if (!added && showFeedback)
            MiniInventoryGridCanvas.TryShowInventoryFull();

        return added;
    }

    public bool DropItemFromInventory(IItem item, bool dropWholeStack)
    {
        if (item == null) return false;
        if (Inventory == null) return false;
        if (dropPrefab == null)
        {
            Debug.LogWarning($"{nameof(Interactor)}: dropPrefab is not assigned.", this);
            return false;
        }

        if (!TryGetItemData(item, out ItemDataSO data))
            return false;

        int amount = 1;
        bool removed;
        if (item is IStackableItem stackable)
        {
            amount = dropWholeStack ? Mathf.Max(1, stackable.Count) : 1;
            removed = dropWholeStack ? Inventory.RemoveAll(item) : Inventory.RemoveItem(item);
        }
        else
        {
            removed = Inventory.RemoveItem(item);
        }

        if (!removed)
            return false;

        Vector3 position = ResolveDropPosition();
        
        Transform parent = RoomContext.Current != null
            ? RoomContext.Current.ItemsRoot
            : null;

        new WorldItemSpawner(dropPrefab, instantiator).SpawnItem(data, amount, position, parent);
        return true;
    }

    [Inject]
    private void Construct([InjectOptional] IInstantiator injectedInstantiator)
    {
        instantiator = injectedInstantiator;
    }

    private void Awake()
    {
        if (inventoryHolder == null)
            inventoryHolder = GetComponent<PlayerInventoryHolder>();

        if (miniInventory == null)
            miniInventory = GetComponent<PlayerMiniInventory>();

        worldSpaceSettings = ResolveWorldSpaceSettings();
        CacheInteractionRangeColliders();
    }

    private static bool TryGetItemData(IItem item, out ItemDataSO data)
    {
        if (item is IItemDataProvider provider && provider.Data != null)
        {
            data = provider.Data;
            return true;
        }

        data = null;
        return false;
    }

    private void Reset()
    {
        if (TryGetComponent(out Collider2D collider2D))
        {
            collider2D.isTrigger = true;
            return;
        }

        Transform interactionRange = transform.Find("InteractionRange");
        if (interactionRange != null && interactionRange.TryGetComponent(out Collider rangeCollider))
            rangeCollider.isTrigger = true;
    }

    private void Update()
    {
        if (UsesXZInteractionQuery())
        {
            UpdateSceneQueryInteractions();
            return;
        }

        UpdateFocus();
    }

    /// <summary>
    /// Attempts to interact with the current candidate if possible.
    /// </summary>
    public void TryInteract()
    {
        if (_current == null) return;
        if (_current.Mode != InteractionMode.Manual) return;
        if (!_current.CanInteract(this)) return;

        _current.Interact(this);
        UpdateFocus(true);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (UsesXZInteractionQuery())
            return;

        if (((1 << other.gameObject.layer) & interactableMask) == 0) return;

        if (other.TryGetComponent<IInteractable>(out var interactable))
        {
            if (interactable.Mode == InteractionMode.Automatic)
            {
                if (interactable.CanInteract(this))
                    interactable.Interact(this);
                return;
            }

            _candidates.Add(interactable);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (UsesXZInteractionQuery())
            return;

        if (other.TryGetComponent<IInteractable>(out var interactable))
        {
            _candidates.Remove(interactable);
            if (_current == interactable)
            {
                _current = null;
                focusChannel?.Raise(null);
            }
        }
    }

    private void UpdateFocus(bool force = false)
    {
        if (UsesXZInteractionQuery())
        {
            UpdateSceneQueryInteractions(force);
            return;
        }

        var best = SelectBestInteractable();
        if (!force && ReferenceEquals(best, _current)) return;

        _current = best;
        focusChannel?.Raise(_current);
    }

    private IInteractable SelectBestInteractable()
    {
        if (_candidates.Count == 0) return null;

        var origin = transform.position;
        var forward = (Vector2)transform.up;

        return _candidates
            .Where(c => c != null && c.InteractionPoint != null && c.Mode == InteractionMode.Manual)
            .Select(c =>
            {
                var to = (Vector2)(c.InteractionPoint.position - origin);
                var dist = to.magnitude;
                if (dist > maxDistance) return (c, score: float.NegativeInfinity);

                var dir = dist <= 0.001f ? forward : to / dist;
                var facing = Vector2.Dot(forward, dir);
                var score = (facing * 2f) - dist;
                return (c, score);
            })
            .Where(x => x.score > float.NegativeInfinity && x.c.CanInteract(this))
            .OrderByDescending(x => x.score)
            .Select(x => x.c)
            .FirstOrDefault();
    }

    private void UpdateSceneQueryInteractions(bool force = false)
    {
        InteractableBase[] interactables = FindObjectsByType<InteractableBase>(FindObjectsSortMode.None);
        IInteractable bestManual = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < interactables.Length; i++)
        {
            InteractableBase interactable = interactables[i];
            if (interactable == null || !interactable.isActiveAndEnabled)
                continue;

            if (((1 << interactable.gameObject.layer) & interactableMask) == 0)
                continue;

            Transform interactionPoint = interactable.InteractionPoint;
            if (interactionPoint == null || !interactable.CanInteract(this))
                continue;

            if (!IsWithinXZInteractionRange(interactionPoint.position))
                continue;

            float planarDistance = ResolvePlanarDistance(transform.position, interactionPoint.position);
            if (interactionRangeCollider3D == null && planarDistance > maxDistance)
                continue;

            if (interactable.Mode == InteractionMode.Automatic)
            {
                interactable.Interact(this);
                continue;
            }

            float score = -planarDistance;
            if (score > bestScore)
            {
                bestScore = score;
                bestManual = interactable;
            }
        }

        if (!force && ReferenceEquals(bestManual, _current))
            return;

        _current = bestManual;
        focusChannel?.Raise(_current);
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

    private void CacheInteractionRangeColliders()
    {
        Transform interactionRange = transform.Find("InteractionRange");
        if (interactionRange != null)
        {
            interactionRangeCollider3D = interactionRange.GetComponent<Collider>();
            interactionRangeCollider2D = interactionRange.GetComponent<Collider2D>();
        }

        if (interactionRangeCollider3D == null)
            interactionRangeCollider3D = GetComponent<Collider>();

        if (interactionRangeCollider2D == null)
            interactionRangeCollider2D = GetComponent<Collider2D>();
    }

    private bool UsesXZInteractionQuery()
    {
        RoomWorldSpaceSettings settings = ResolveWorldSpaceSettings();
        return settings != null && settings.UsesXZPlane;
    }

    private bool IsWithinXZInteractionRange(Vector3 worldPosition)
    {
        if (interactionRangeCollider3D == null)
            return ResolvePlanarDistance(transform.position, worldPosition) <= maxDistance;

        Vector3 closestPoint = interactionRangeCollider3D.ClosestPoint(worldPosition);
        return (closestPoint - worldPosition).sqrMagnitude <= 0.0001f;
    }

    private float ResolvePlanarDistance(Vector3 from, Vector3 to)
    {
        RoomWorldSpaceSettings settings = ResolveWorldSpaceSettings();
        if (settings != null)
            return settings.PlanarDistance(from, to);

        from.z = 0f;
        to.z = 0f;
        return Vector3.Distance(from, to);
    }

    private Vector3 ResolveDropPosition()
    {
        RoomWorldSpaceSettings settings = ResolveWorldSpaceSettings();
        bool usesXZ = settings != null && settings.UsesXZPlane;

        Vector3 resolvedOffset = usesXZ
            ? new Vector3(dropOffset.x, 0f, Mathf.Approximately(dropOffset.z, 0f) ? dropOffset.y : dropOffset.z)
            : dropOffset;

        Vector3 position = transform.position + resolvedOffset;
        if (dropScatter <= 0f)
            return position;

        if (usesXZ)
        {
            position += new Vector3(
                Random.Range(-dropScatter, dropScatter),
                0f,
                Random.Range(-dropScatter, dropScatter));
            return position;
        }

        position += new Vector3(
            Random.Range(-dropScatter, dropScatter),
            Random.Range(-dropScatter, dropScatter),
            0f);
        return position;
    }
}
