using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class PlayerDeathTelemetryReporter : MonoBehaviour
{
    [Header("Events")]
    [SerializeField] private OnDeathEventSO onDeathEvent;
    [SerializeField] private RoomSpawnEvent roomSpawnEvent;

    [Header("Supabase")]
    [SerializeField] private string supabaseProjectUrl;
    [SerializeField] private string supabaseApiKey;

    [Header("Telemetry")]
    [SerializeField] private string telemetryEventName = "player_death_snapshot";
    [SerializeField] private string playerId = "player_unknown";
    [SerializeField] private string sessionId;
    [SerializeField] private bool logPayloadInConsole;
    [SerializeField] private bool persistSnapshotLocally = true;
    [SerializeField] private string localSnapshotKey = "telemetry_last_death_snapshot";

    [Header("Player References")]
    [SerializeField] private Interactor interactor;
    [SerializeField] private PlayerMiniInventory miniInventory;

    private readonly Dictionary<string, string> trackedPactsById = new(StringComparer.Ordinal);

    private SupabaseTelemetryClient telemetryClient;
    private OnDeathEventSO subscribedOnDeathEvent;
    private RoomSpawnEvent subscribedRoomSpawnEvent;
    private PactManager subscribedPactManager;
    private bool isSendingTelemetry;
    private bool hasSentForCurrentDeath;
    private int lastKnownRoomNumber = 1;
    private int trackedRunSeed;
    private float nextBindingRetryTime;

    private void Awake()
    {
        EnsureSessionId();
        ResolvePlayerReferences();
        RefreshRoomNumberFromCurrentBuilder();
    }

    private void OnEnable()
    {
        TrySubscribeEvents();
        TrySubscribePactManager();
        CaptureActivePactsFromManager();
        RefreshRoomNumberFromCurrentBuilder();
    }

    private void Update()
    {
        if ((subscribedOnDeathEvent == null || subscribedRoomSpawnEvent == null) &&
            Time.unscaledTime >= nextBindingRetryTime)
        {
            TrySubscribeEvents();
            nextBindingRetryTime = Time.unscaledTime + 1f;
        }

        if (subscribedPactManager == null)
            TrySubscribePactManager();

        if (interactor == null || miniInventory == null)
            ResolvePlayerReferences();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        UnsubscribePactManager();
    }

    private void TrySubscribeEvents()
    {
        EnsureEventBindings();

        if (subscribedOnDeathEvent == null && onDeathEvent != null)
        {
            onDeathEvent.RegisterListener(HandlePlayerDeath);
            subscribedOnDeathEvent = onDeathEvent;
        }

        if (subscribedRoomSpawnEvent == null && roomSpawnEvent != null)
        {
            roomSpawnEvent.OnRoomSpawn += HandleRoomSpawn;
            subscribedRoomSpawnEvent = roomSpawnEvent;
        }
    }

    private void UnsubscribeEvents()
    {
        if (subscribedOnDeathEvent != null)
        {
            subscribedOnDeathEvent.UnregisterListener(HandlePlayerDeath);
            subscribedOnDeathEvent = null;
        }

        if (subscribedRoomSpawnEvent != null)
        {
            subscribedRoomSpawnEvent.OnRoomSpawn -= HandleRoomSpawn;
            subscribedRoomSpawnEvent = null;
        }
    }

    private void TrySubscribePactManager()
    {
        if (subscribedPactManager != null)
            return;

        if (PactManager.Instance == null)
            return;

        subscribedPactManager = PactManager.Instance;
        subscribedPactManager.PactApplied += HandlePactApplied;
    }

    private void UnsubscribePactManager()
    {
        if (subscribedPactManager == null)
            return;

        subscribedPactManager.PactApplied -= HandlePactApplied;
        subscribedPactManager = null;
    }

    private void HandleRoomSpawn(Vector3 _)
    {
        hasSentForCurrentDeath = false;
        RefreshRoomNumberFromCurrentBuilder();
    }

    private void HandlePactApplied(PactDefinition pact)
    {
        TrackPact(pact);
    }

    private async void HandlePlayerDeath()
    {
        if (hasSentForCurrentDeath || isSendingTelemetry)
            return;

        hasSentForCurrentDeath = true;
        isSendingTelemetry = true;

        try
        {
            ResolvePlayerReferences();
            RefreshRoomNumberFromCurrentBuilder();
            CaptureActivePactsFromManager();

            string payloadJson = BuildPayloadJson();
            if (logPayloadInConsole)
                Debug.Log($"PlayerDeathTelemetryReporter payload: {payloadJson}", this);

            PersistSnapshotLocally(payloadJson);

            if (!IsTelemetryConfigured())
            {
                Debug.LogWarning(
                    "PlayerDeathTelemetryReporter: Supabase URL/API key are missing. Payload was captured but not sent.",
                    this);
                return;
            }

            EnsureTelemetryClient();
            var telemetryEvent = new TelemetryEvent(
                telemetryEventName,
                ResolvePlayerId(),
                sessionId,
                DateTime.UtcNow,
                payloadJson);

            await telemetryClient.SendAsync(telemetryEvent);
        }
        catch (Exception ex)
        {
            Debug.LogWarning(
                $"PlayerDeathTelemetryReporter: failed to send telemetry. {ex.GetType().Name}: {ex.Message}",
                this);
        }
        finally
        {
            isSendingTelemetry = false;
        }
    }

    private void EnsureEventBindings()
    {
        if (roomSpawnEvent == null && RoomBuilder.Current != null)
            roomSpawnEvent = RoomBuilder.Current.RoomSpawnEvent;

        if (onDeathEvent == null)
        {
            OnDeathEventSO[] events = Resources.FindObjectsOfTypeAll<OnDeathEventSO>();
            if (events != null && events.Length > 0)
                onDeathEvent = events[0];
        }
    }

    private void ResolvePlayerReferences()
    {
        if (interactor == null)
            interactor = ResolvePlayerInteractor();

        if (miniInventory == null)
            miniInventory = ResolvePlayerMiniInventory();
    }

    private void EnsureSessionId()
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
            return;

        sessionId = Guid.NewGuid().ToString("N");
    }

    private void EnsureTelemetryClient()
    {
        if (telemetryClient != null)
            return;

        string normalizedUrl = supabaseProjectUrl != null
            ? supabaseProjectUrl.Trim().TrimEnd('/')
            : string.Empty;

        telemetryClient = new SupabaseTelemetryClient(normalizedUrl, supabaseApiKey);
    }

    private bool IsTelemetryConfigured()
    {
        return !string.IsNullOrWhiteSpace(supabaseProjectUrl) &&
               !string.IsNullOrWhiteSpace(supabaseApiKey);
    }

    private void RefreshRoomNumberFromCurrentBuilder()
    {
        if (RoomBuilder.Current == null)
            return;

        int currentRunSeed = RoomBuilder.Current.CurrentRunSeed;
        if (currentRunSeed != 0)
        {
            if (trackedRunSeed != 0 && trackedRunSeed != currentRunSeed)
            {
                trackedPactsById.Clear();
                hasSentForCurrentDeath = false;
            }

            trackedRunSeed = currentRunSeed;
        }

        lastKnownRoomNumber = Mathf.Max(1, RoomBuilder.Current.CurrentRoomNumber);
    }

    private void CaptureActivePactsFromManager()
    {
        PactManager manager = PactManager.Instance;
        if (manager == null || manager.ActivePacts == null || manager.ActivePacts.Count == 0)
            return;

        for (int i = 0; i < manager.ActivePacts.Count; i++)
            TrackPact(manager.ActivePacts[i]);
    }

    private void TrackPact(PactDefinition pact)
    {
        if (pact == null)
            return;

        string pactId = ResolvePactId(pact);
        if (pactId.Length == 0)
            return;

        trackedPactsById[pactId] = ResolvePactTitle(pact);
    }

    private string BuildPayloadJson()
    {
        var payload = new DeathSnapshotPayload
        {
            sceneName = SceneManager.GetActiveScene().name,
            runSeed = RoomBuilder.Current != null ? RoomBuilder.Current.CurrentRunSeed : 0,
            roomNumber = Mathf.Max(1, lastKnownRoomNumber),
            pacts = BuildPactSnapshots(),
            inventoryItems = BuildInventoryItemArray(),
            equippedItems = BuildEquippedItemArray(),
            currencyAmount = ResolveCurrencyAmount()
        };

        return JsonUtility.ToJson(payload);
    }

    private PactSnapshot[] BuildPactSnapshots()
    {
        if (trackedPactsById.Count == 0)
            return Array.Empty<PactSnapshot>();

        var pactIds = new List<string>(trackedPactsById.Keys);
        pactIds.Sort(StringComparer.Ordinal);

        var pacts = new PactSnapshot[pactIds.Count];
        for (int i = 0; i < pactIds.Count; i++)
        {
            string pactId = pactIds[i];
            pacts[i] = new PactSnapshot
            {
                pactId = pactId,
                pactTitle = trackedPactsById[pactId]
            };
        }

        return pacts;
    }

    private ItemSnapshot[] BuildInventoryItemArray()
    {
        Inventory inventory = interactor != null ? interactor.Inventory : null;
        if (inventory == null || inventory.Items == null || inventory.Items.Count == 0)
            return Array.Empty<ItemSnapshot>();

        var items = new List<ItemSnapshot>(inventory.Items.Count);
        for (int i = 0; i < inventory.Items.Count; i++)
        {
            if (TryBuildItemSnapshot("inventory", inventory.Items[i], out ItemSnapshot snapshot))
                items.Add(snapshot);
        }

        return items.ToArray();
    }

    private ItemSnapshot[] BuildEquippedItemArray()
    {
        if (miniInventory == null)
            return Array.Empty<ItemSnapshot>();

        var equipped = new List<ItemSnapshot>(4);
        TryAddEquippedItem(equipped, "weapon", miniInventory.EquippedWeapon);
        TryAddEquippedItem(equipped, "armor", miniInventory.EquippedArmor);
        TryAddEquippedItem(equipped, "consumable", miniInventory.EquippedConsumable);
        TryAddEquippedItem(equipped, "ability", miniInventory.EquippedAbility);
        return equipped.ToArray();
    }

    private static void TryAddEquippedItem(List<ItemSnapshot> target, string slot, IItem item)
    {
        if (target == null)
            return;

        if (!TryBuildItemSnapshot(slot, item, out ItemSnapshot snapshot))
            return;

        target.Add(snapshot);
    }

    private static bool TryBuildItemSnapshot(string slot, IItem item, out ItemSnapshot snapshot)
    {
        snapshot = null;
        if (item == null)
            return false;

        string itemId = string.Empty;
        string itemName = item.Name;
        string itemType = "Unknown";

        if (item is IItemDataProvider provider && provider.Data != null)
        {
            ItemDataSO data = provider.Data;
            itemId = string.IsNullOrWhiteSpace(data.SaveId) ? data.name : data.SaveId;

            if (!string.IsNullOrWhiteSpace(data.DisplayName))
                itemName = data.DisplayName;

            itemType = data.ItemType.ToString();
        }

        if (string.IsNullOrWhiteSpace(itemId))
            itemId = string.IsNullOrWhiteSpace(itemName) ? "unknown_item" : itemName;

        int amount = 1;
        if (item is IStackableItem stackable)
            amount = Mathf.Max(1, stackable.Count);

        snapshot = new ItemSnapshot
        {
            slot = slot,
            itemId = itemId,
            itemName = itemName,
            itemType = itemType,
            amount = amount
        };
        return true;
    }

    private int ResolveCurrencyAmount()
    {
        Inventory inventory = interactor != null ? interactor.Inventory : null;
        return inventory != null ? Mathf.Max(0, inventory.CurrencyAmount) : 0;
    }

    private void PersistSnapshotLocally(string payloadJson)
    {
        if (!persistSnapshotLocally)
            return;

        if (string.IsNullOrWhiteSpace(localSnapshotKey))
            return;

        if (payloadJson == null)
            payloadJson = string.Empty;

        PlayerPrefs.SetString(localSnapshotKey, payloadJson);
        PlayerPrefs.Save();
    }

    private string ResolvePlayerId()
    {
        if (!string.IsNullOrWhiteSpace(playerId))
            return playerId.Trim();

        GameObject player = ResolvePlayerObject();
        return player != null ? player.name : "player_unknown";
    }

    private static string ResolvePactId(PactDefinition pact)
    {
        if (pact == null)
            return string.Empty;

        string pactId = string.IsNullOrWhiteSpace(pact.SaveId)
            ? pact.name
            : pact.SaveId;

        return string.IsNullOrWhiteSpace(pactId)
            ? string.Empty
            : pactId.Trim();
    }

    private static string ResolvePactTitle(PactDefinition pact)
    {
        if (pact == null)
            return string.Empty;

        string title = pact.Title;
        if (string.IsNullOrWhiteSpace(title))
            title = pact.name;

        return string.IsNullOrWhiteSpace(title)
            ? string.Empty
            : title.Trim();
    }

    private static Interactor ResolvePlayerInteractor()
    {
        GameObject player = ResolvePlayerObject();
        if (player != null && player.TryGetComponent(out Interactor taggedInteractor))
            return taggedInteractor;

        return FindFirstObjectByType<Interactor>();
    }

    private static PlayerMiniInventory ResolvePlayerMiniInventory()
    {
        GameObject player = ResolvePlayerObject();
        if (player != null && player.TryGetComponent(out PlayerMiniInventory taggedMiniInventory))
            return taggedMiniInventory;

        return FindFirstObjectByType<PlayerMiniInventory>();
    }

    private static GameObject ResolvePlayerObject()
    {
        try
        {
            return GameObject.FindGameObjectWithTag("Player");
        }
        catch (UnityException)
        {
            return null;
        }
    }

    [Serializable]
    private sealed class DeathSnapshotPayload
    {
        public string sceneName;
        public int runSeed;
        public int roomNumber;
        public PactSnapshot[] pacts;
        public ItemSnapshot[] inventoryItems;
        public ItemSnapshot[] equippedItems;
        public int currencyAmount;
    }

    [Serializable]
    private sealed class PactSnapshot
    {
        public string pactId;
        public string pactTitle;
    }

    [Serializable]
    private sealed class ItemSnapshot
    {
        public string slot;
        public string itemId;
        public string itemName;
        public string itemType;
        public int amount;
    }
}
