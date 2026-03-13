using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaveSystem
{
    public sealed class GameSaveService : IGameSaveService
    {
        private const int CurrentSchemaVersion = 8;

        private readonly ISaveSystem saveSystem;
        private readonly IPlayerDataService playerDataService;

        public GameSaveService(ISaveSystem saveSystem, IPlayerDataService playerDataService)
        {
            this.saveSystem = saveSystem;
            this.playerDataService = playerDataService;
        }

        public void SaveCurrentState(string slot, string sceneName)
        {
            AttackType selectedAttack = playerDataService != null
                ? playerDataService.SelectedAttack
                : AttackType.Melee;

            int roomNumber = ResolveRoomNumber();
            int runSeed = ResolveRunSeed();
            CaptureNpcRoomOfferSnapshot(
                out int npcRoomSeed,
                out string npcRoomNpcId,
                out string[] npcRoomOfferPactIds);
            var state = new StateData
            {
                ActivePactIds = BuildActivePactIds(PactManager.Instance),
                RoomNumber = roomNumber,
                RunSeed = runSeed,
                NpcRoomSeed = npcRoomSeed,
                NpcRoomNpcId = npcRoomNpcId,
                NpcRoomOfferPactIds = npcRoomOfferPactIds,
                Player = BuildPlayerStateData(selectedAttack)
            };

            var data = new GameSaveData
            {
                SchemaVersion = CurrentSchemaVersion,
                SceneName = sceneName,
                State = state,
                Player = new PlayerSaveData
                {
                    SelectedAttack = (int)selectedAttack
                },
                SelectedAttack = (int)selectedAttack,
                SavedAtUtc = DateTime.UtcNow.ToString("O")
            };

            saveSystem.Save(slot, data);
            LoadedNpcRoomOfferState.SetFromSave(data);
        }

        public bool TryLoadState(string slot, out GameSaveData data)
        {
            if (!saveSystem.TryLoad(slot, out data))
                return false;

            if (data.SchemaVersion > CurrentSchemaVersion)
                return false;

            if (data.SchemaVersion < CurrentSchemaVersion)
                data = UpgradeLegacyData(data);

            if (data.State.RoomNumber <= 0)
                data.State.RoomNumber = 1;

            if (data.State.RunSeed == 0)
                data.State.RunSeed = GenerateRunSeed();

            int attackValue = ResolveAttackValue(data);
            SetSelectedAttack(attackValue);

            LoadedNpcRoomOfferState.SetFromSave(data);
            PendingGameSaveState.Set(data);

            return true;
        }

        public bool TryApplyPendingState()
        {
            if (!PendingGameSaveState.TryGet(out GameSaveData data))
                return false;

            bool appliedPlayer = ApplyPlayerState(data.State.Player);

            if (!appliedPlayer)
                return false;

            PendingGameSaveState.Clear();
            return true;
        }

        public bool Exists(string slot)
        {
            return saveSystem.Exists(slot);
        }

        public bool Delete(string slot)
        {
            return saveSystem.Delete(slot);
        }

        private PlayerStateData BuildPlayerStateData(AttackType selectedAttack)
        {
            HealthComponent playerHealth = ResolvePlayerHealth();
            Interactor playerInteractor = ResolvePlayerInteractor();
            PlayerMiniInventory miniInventory = ResolvePlayerMiniInventory();
            Inventory inventory = playerInteractor != null ? playerInteractor.Inventory : null;

            return new PlayerStateData
            {
                SelectedAttack = (int)selectedAttack,
                CharacterStatsId = ResolveCharacterStatsId(playerHealth),
                MissingHealthPercent = ResolveMissingHealthPercent(playerHealth),
                CurrencyAmount = inventory != null ? Mathf.Max(0, inventory.CurrencyAmount) : 0,
                InventoryItems = BuildInventoryItemStates(inventory),
                EquippedItems = BuildEquippedItemState(miniInventory)
            };
        }

        private static string[] BuildActivePactIds(PactManager manager)
        {
            if (manager == null || manager.ActivePacts == null || manager.ActivePacts.Count == 0)
                return Array.Empty<string>();

            var pactIds = new List<string>(manager.ActivePacts.Count);
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < manager.ActivePacts.Count; i++)
            {
                PactDefinition pact = manager.ActivePacts[i];
                if (pact == null)
                    continue;

                string pactId = string.IsNullOrWhiteSpace(pact.SaveId) ? pact.name : pact.SaveId;
                if (!string.IsNullOrWhiteSpace(pactId) && seenIds.Add(pactId))
                    pactIds.Add(pactId);
            }

            return pactIds.ToArray();
        }

        private static int ResolveRoomNumber()
        {
            if (RoomBuilder.Current == null)
                return 1;

            return Mathf.Max(1, RoomBuilder.Current.CurrentRoomNumber);
        }

        private static int ResolveRunSeed()
        {
            if (RoomBuilder.Current != null)
            {
                int roomBuilderSeed = RoomBuilder.Current.CurrentRunSeed;
                if (roomBuilderSeed != 0)
                    return roomBuilderSeed;
            }

            if (PendingGameSaveState.TryGet(out GameSaveData pendingData))
            {
                int pendingSeed = pendingData.State.RunSeed;
                if (pendingSeed != 0)
                    return pendingSeed;
            }

            return GenerateRunSeed();
        }

        private static int GenerateRunSeed()
        {
            int seed = Guid.NewGuid().GetHashCode();
            return seed == 0 ? 1 : seed;
        }

        private static string ResolveCharacterStatsId(HealthComponent health)
        {
            if (health == null || health.BaseStats == null)
                return string.Empty;

            return string.IsNullOrWhiteSpace(health.BaseStats.SaveId)
                ? health.BaseStats.name
                : health.BaseStats.SaveId;
        }

        private static float ResolveMissingHealthPercent(HealthComponent health)
        {
            if (health == null || health.MaxHealth <= 0f)
                return 0f;

            float normalized = Mathf.Clamp01(health.CurrentHealth / health.MaxHealth);
            return 1f - normalized;
        }

        private static ItemStateData[] BuildInventoryItemStates(Inventory inventory)
        {
            if (inventory == null || inventory.Items == null || inventory.Items.Count == 0)
                return Array.Empty<ItemStateData>();

            var items = new List<ItemStateData>(inventory.Items.Count);
            for (int i = 0; i < inventory.Items.Count; i++)
            {
                IItem item = inventory.Items[i];
                if (item == null)
                    continue;

                if (!TryBuildItemState(item, out ItemStateData itemState))
                    continue;

                items.Add(itemState);
            }

            return items.ToArray();
        }

        private static EquippedItemsStateData BuildEquippedItemState(PlayerMiniInventory miniInventory)
        {
            var equipped = new EquippedItemsStateData();
            if (miniInventory == null)
                return equipped;

            if (TryBuildItemState(miniInventory.EquippedWeapon, out ItemStateData weapon))
                equipped.Weapon = weapon;

            if (TryBuildItemState(miniInventory.EquippedArmor, out ItemStateData armor))
                equipped.Armor = armor;

            if (TryBuildItemState(miniInventory.EquippedConsumable, out ItemStateData consumable))
                equipped.Consumable = consumable;

            if (TryBuildItemState(miniInventory.EquippedAbility, out ItemStateData ability))
                equipped.Ability = ability;

            return equipped;
        }

        private static bool TryBuildItemState(IItem item, out ItemStateData state)
        {
            state = default;
            if (item is not IItemDataProvider provider || provider.Data == null)
                return false;

            string itemId = provider.Data.SaveId;
            if (string.IsNullOrWhiteSpace(itemId))
                itemId = provider.Data.name;

            if (string.IsNullOrWhiteSpace(itemId))
                return false;

            int amount = 1;
            if (item is IStackableItem stackable)
                amount = Mathf.Max(1, stackable.Count);

            state = new ItemStateData
            {
                ItemId = itemId,
                Amount = amount
            };

            return true;
        }

        private bool ApplyPlayerState(PlayerStateData playerState)
        {
            bool applied = false;

            SetSelectedAttack(playerState.SelectedAttack);

            HealthComponent playerHealth = ResolvePlayerHealth();
            if (playerHealth != null)
            {
                if (!string.IsNullOrWhiteSpace(playerState.CharacterStatsId))
                {
                    CharacterStats statsAsset = ResolveCharacterStats(playerState.CharacterStatsId);
                    if (statsAsset != null)
                    {
                        playerHealth.SetBaseStats(statsAsset, resetCurrentHealth: false);

                        if (playerHealth.TryGetComponent(out CharacterStatResolver statResolver))
                            statResolver.SetBaseStats(statsAsset);

                        if (playerHealth.TryGetComponent(out AttackComponent attackComponent))
                            attackComponent.SetBaseStats(statsAsset);
                    }
                }

                playerHealth.SetMissingHealthPercent(playerState.MissingHealthPercent);
                applied = true;
            }

            Interactor playerInteractor = ResolvePlayerInteractor();
            if (playerInteractor != null && playerInteractor.Inventory != null)
            {
                Inventory inventory = playerInteractor.Inventory;
                inventory.Clear();

                ItemStateData[] inventoryItems = playerState.InventoryItems ?? Array.Empty<ItemStateData>();
                for (int i = 0; i < inventoryItems.Length; i++)
                {
                    if (TryCreateItem(inventoryItems[i], out IItem item))
                        inventory.AddItem(item);
                }

                if (playerState.CurrencyAmount > 0)
                    inventory.AddCurrency(playerState.CurrencyAmount);

                applied = true;
            }

            PlayerMiniInventory miniInventory = ResolvePlayerMiniInventory();
            if (miniInventory != null)
            {
                TryCreateItem(playerState.EquippedItems.Weapon, out IItem weapon);
                TryCreateItem(playerState.EquippedItems.Armor, out IItem armor);
                TryCreateItem(playerState.EquippedItems.Consumable, out IItem consumable);
                TryCreateItem(playerState.EquippedItems.Ability, out IItem ability);
                miniInventory.LoadEquippedItemsFromSave(weapon, armor, consumable, ability);
                applied = true;
            }

            return applied;
        }

        private static bool TryCreateItem(ItemStateData state, out IItem item)
        {
            item = null;
            if (string.IsNullOrWhiteSpace(state.ItemId))
                return false;

            if (state.Amount <= 0)
                return false;

            ItemDataSO data = ResolveItemData(state.ItemId);
            if (data == null)
                return false;

            item = ItemFactory.CreateItem(data, state.Amount);
            return item != null;
        }

        private static ItemDataSO ResolveItemData(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            ItemCatalog catalog = GameReferences.Instance != null ? GameReferences.Instance.ItemCatalog : null;
            if (catalog != null && catalog.TryGetBySaveId(itemId, out ItemDataSO fromCatalog))
                return fromCatalog;

            ItemDataSO[] allItems = Resources.FindObjectsOfTypeAll<ItemDataSO>();
            for (int i = 0; i < allItems.Length; i++)
            {
                ItemDataSO item = allItems[i];
                if (item == null)
                    continue;

                if (string.Equals(item.SaveId, itemId, StringComparison.Ordinal) ||
                    string.Equals(item.name, itemId, StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }

        private static CharacterStats ResolveCharacterStats(string statsId)
        {
            if (string.IsNullOrWhiteSpace(statsId))
                return null;

            CharacterStats[] allStats = Resources.FindObjectsOfTypeAll<CharacterStats>();
            for (int i = 0; i < allStats.Length; i++)
            {
                CharacterStats stats = allStats[i];
                if (stats == null)
                    continue;

                if (string.Equals(stats.SaveId, statsId, StringComparison.Ordinal) ||
                    string.Equals(stats.name, statsId, StringComparison.Ordinal))
                {
                    return stats;
                }
            }

            return null;
        }

        private static HealthComponent ResolvePlayerHealth()
        {
            try
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null && player.TryGetComponent(out HealthComponent taggedHealth))
                    return taggedHealth;
            }
            catch (UnityException)
            {
                // Player tag not defined in project settings.
            }

            return UnityEngine.Object.FindFirstObjectByType<HealthComponent>();
        }

        private static Interactor ResolvePlayerInteractor()
        {
            try
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null && player.TryGetComponent(out Interactor taggedInteractor))
                    return taggedInteractor;
            }
            catch (UnityException)
            {
                // Player tag not defined in project settings.
            }

            return UnityEngine.Object.FindFirstObjectByType<Interactor>();
        }

        private static PlayerMiniInventory ResolvePlayerMiniInventory()
        {
            try
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null && player.TryGetComponent(out PlayerMiniInventory taggedMiniInventory))
                    return taggedMiniInventory;
            }
            catch (UnityException)
            {
                // Player tag not defined in project settings.
            }

            return UnityEngine.Object.FindFirstObjectByType<PlayerMiniInventory>();
        }

        private void SetSelectedAttack(int attackValue)
        {
            if (playerDataService == null)
                return;

            playerDataService.SetSelectedAttack(ToAttackType(attackValue));
        }

        private static GameSaveData UpgradeLegacyData(GameSaveData data)
        {
            int selectedAttack = ResolveAttackValue(data);

            if (data.SchemaVersion < 3)
            {
                data.State = new StateData
                {
                    ActivePactIds = Array.Empty<string>(),
                    RoomNumber = 1,
                    RunSeed = GenerateRunSeed(),
                    NpcRoomSeed = 0,
                    NpcRoomNpcId = string.Empty,
                    NpcRoomOfferPactIds = Array.Empty<string>(),
                    Player = new PlayerStateData
                    {
                        SelectedAttack = selectedAttack,
                        CharacterStatsId = string.Empty,
                        MissingHealthPercent = 0f,
                        CurrencyAmount = 0,
                        InventoryItems = Array.Empty<ItemStateData>(),
                        EquippedItems = default
                    }
                };
            }
            else
            {
                if (data.State.ActivePactIds == null)
                    data.State.ActivePactIds = Array.Empty<string>();

                if (data.State.RoomNumber <= 0)
                    data.State.RoomNumber = 1;

                if (data.State.RunSeed == 0)
                    data.State.RunSeed = GenerateRunSeed();

                if (data.SchemaVersion < 8)
                {
                    data.State.NpcRoomSeed = 0;
                    data.State.NpcRoomNpcId = string.Empty;
                    data.State.NpcRoomOfferPactIds = Array.Empty<string>();
                }
                else if (data.State.NpcRoomOfferPactIds == null)
                {
                    data.State.NpcRoomOfferPactIds = Array.Empty<string>();
                }

                if (data.State.Player.InventoryItems == null)
                    data.State.Player.InventoryItems = Array.Empty<ItemStateData>();
            }

            data.SchemaVersion = CurrentSchemaVersion;
            return data;
        }

        private static int ResolveAttackValue(GameSaveData data)
        {
            if (data.SchemaVersion >= 3)
                return data.State.Player.SelectedAttack;

            if (data.SchemaVersion >= 2)
                return data.Player.SelectedAttack;

            return data.SelectedAttack;
        }

        private static void CaptureNpcRoomOfferSnapshot(
            out int npcRoomSeed,
            out string npcRoomNpcId,
            out string[] npcRoomOfferPactIds)
        {
            npcRoomSeed = 0;
            npcRoomNpcId = string.Empty;
            npcRoomOfferPactIds = Array.Empty<string>();

            if (RoomBuilder.Current == null || !RoomBuilder.Current.IsCurrentNpcRoom)
                return;

            npcRoomSeed = RoomBuilder.Current.CurrentRoomSeed;
            if (npcRoomSeed == 0)
                return;

            PactNpc[] pactNpcs = UnityEngine.Object.FindObjectsOfType<PactNpc>();
            if (pactNpcs == null || pactNpcs.Length == 0)
                return;

            Array.Sort(
                pactNpcs,
                (left, right) => StringComparer.Ordinal.Compare(
                    PactIdentity.Normalize(left != null ? left.NpcId : string.Empty),
                    PactIdentity.Normalize(right != null ? right.NpcId : string.Empty)));

            for (int i = 0; i < pactNpcs.Length; i++)
            {
                PactNpc pactNpc = pactNpcs[i];
                if (pactNpc == null)
                    continue;

                if (!pactNpc.TryBuildOfferSnapshot(
                        out string resolvedNpcId,
                        out _,
                        out string[] offerPactIds))
                {
                    continue;
                }

                npcRoomNpcId = resolvedNpcId;
                npcRoomOfferPactIds = offerPactIds ?? Array.Empty<string>();
                return;
            }
        }

        private static AttackType ToAttackType(int value)
        {
            return value == (int)AttackType.Ranged
                ? AttackType.Ranged
                : AttackType.Melee;
        }
    }
}
