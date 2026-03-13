using System;

namespace SaveSystem
{
    [Serializable]
    public struct ItemStateData
    {
        public string ItemId;
        public int Amount;
    }

    [Serializable]
    public struct EquippedItemsStateData
    {
        public ItemStateData Weapon;
        public ItemStateData Armor;
        public ItemStateData Consumable;
        public ItemStateData Ability;
    }

    [Serializable]
    public struct PlayerStateData
    {
        public int SelectedAttack;
        public string CharacterStatsId;
        public float MissingHealthPercent;
        public int CurrencyAmount;
        public ItemStateData[] InventoryItems;
        public EquippedItemsStateData EquippedItems;
    }

    [Serializable]
    public struct StateData
    {
        public string[] ActivePactIds;
        public int RoomNumber;
        public int RunSeed;
        public int NpcRoomSeed;
        public string NpcRoomNpcId;
        public string[] NpcRoomOfferPactIds;
        public PlayerStateData Player;
    }

    [Serializable]
    public struct PlayerSaveData
    {
        public int SelectedAttack;
    }

    [Serializable]
    public struct GameSaveData
    {
        public int SchemaVersion;
        public string SceneName;
        public StateData State;
        public PlayerSaveData Player;
        public int SelectedAttack;
        public string SavedAtUtc;
    }
}
