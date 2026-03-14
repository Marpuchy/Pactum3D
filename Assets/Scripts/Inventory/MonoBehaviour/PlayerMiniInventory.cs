using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class PlayerMiniInventory : MonoBehaviour, IItemCapabilityProvider
{
    [Header("References")]
    [SerializeField] private Interactor interactor;
    [SerializeField] private MiniInventoryGridCanvas miniInventoryView;
    [SerializeField] private HealthComponent healthComponent;
    [SerializeField] private CharacterStatResolver statResolver;
    [SerializeField] private PlayerController playerController;

    [Header("Defaults")]
    [SerializeField] private ItemDataSO starterWeapon;

    [Header("Input")]
    [SerializeField] private KeyCode useConsumableKey = KeyCode.Alpha1;
    
    [Header("Events")]
    [SerializeField] private ConsumeAudioEvent consumeAudioEvent;

    public event System.Action Unequipped;

    private IItem equippedWeapon;
    private IItem equippedArmor;
    private IItem equippedConsumable;
    private IItem equippedAbility;
    private readonly List<IStatModifier> weaponModifiers = new();
    private readonly List<IStatModifier> armorModifiers = new();
    private readonly List<MiniInventoryGridCanvas> miniInventoryViews = new();

    public IItem EquippedWeapon => equippedWeapon;
    public IItem EquippedArmor => equippedArmor;
    public IItem EquippedConsumable => equippedConsumable;
    public IItem EquippedAbility => equippedAbility;

    private void Awake()
    {
        if (interactor == null)
            interactor = GetComponent<Interactor>();

        if (healthComponent == null)
            healthComponent = GetComponent<HealthComponent>();

        if (statResolver == null)
            statResolver = GetComponent<CharacterStatResolver>();

        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        RegisterExistingViews();

        if (miniInventoryView == null)
            miniInventoryView = FindFirstObjectByType<MiniInventoryGridCanvas>();

        if (miniInventoryView != null)
            RegisterView(miniInventoryView);
    }

    private void Start()
    {
        EquipStarterWeapon();
    }

    private void Update()
    {
        if (GameplayUIState.IsGameplayInputBlocked)
            return;

        if (Input.GetKeyDown(useConsumableKey))
            UseConsumable();
    }

    public bool TryEquip(IItem item)
    {
        return EquipItem(item, requireInventory: true, removeFromInventory: true);
    }

    public void LoadEquippedItemsFromSave(IItem weapon, IItem armor, IItem consumable, IItem ability)
    {
        ClearEquipmentModifiers(weaponModifiers);
        ClearEquipmentModifiers(armorModifiers);

        equippedWeapon = weapon;
        equippedArmor = armor;
        equippedConsumable = consumable;
        equippedAbility = ability;

        ApplyEquipmentModifiers(MiniInventorySlotType.Weapon, equippedWeapon, weaponModifiers);
        ApplyEquipmentModifiers(MiniInventorySlotType.Armor, equippedArmor, armorModifiers);

        SetSlotInViews(MiniInventorySlotType.Weapon, equippedWeapon);
        SetSlotInViews(MiniInventorySlotType.Armor, equippedArmor);
        SetSlotInViews(MiniInventorySlotType.Consumable, equippedConsumable);
        SetSlotInViews(MiniInventorySlotType.Ability, equippedAbility);
    }

    public void BindView(MiniInventoryGridCanvas view)
    {
        RegisterView(view);
    }

    public void RegisterView(MiniInventoryGridCanvas view)
    {
        if (view == null)
            return;

        if (miniInventoryViews.Contains(view))
            return;

        miniInventoryViews.Add(view);
        view.SetSlotClickHandlers(null, HandleSlotRightClicked);
        RefreshView(view);
    }

    public bool TryAbsorbPickup(IItem item)
    {
        if (item == null)
            return false;

        if (equippedConsumable == null)
            return false;

        if (!TryGetItemData(item, out ItemDataSO data))
            return false;

        if (data.ItemType != ItemType.Consumable || data.IsCurrency)
            return false;

        if (!TryGetItemData(equippedConsumable, out ItemDataSO equippedData))
            return false;

        if (equippedData != data)
            return false;

        if (equippedConsumable is IStackableItem equippedStack &&
            item is IStackableItem incomingStack)
        {
            equippedStack.Add(incomingStack.Count);
            SetSlotInViews(MiniInventorySlotType.Consumable, equippedConsumable);
            return true;
        }

        return false;
    }

    public bool TryUnequip(MiniInventorySlotType slotType)
    {
        if (slotType == MiniInventorySlotType.Weapon ||
            slotType == MiniInventorySlotType.Ability)
        {
            return false;
        }

        Inventory inventory = interactor != null ? interactor.Inventory : null;
        if (inventory == null)
            return false;

        switch (slotType)
        {
            case MiniInventorySlotType.Armor:
                if (equippedArmor == null) return false;
                if (!inventory.AddItem(equippedArmor))
                    return false;
                ClearEquipmentModifiers(armorModifiers);
                equippedArmor = null;
                ClearSlotInViews(MiniInventorySlotType.Armor);
                Unequipped?.Invoke();
                return true;
            case MiniInventorySlotType.Consumable:
                if (equippedConsumable == null) return false;
                if (!inventory.AddItem(equippedConsumable))
                    return false;
                ClearConsumableSlot();
                Unequipped?.Invoke();
                return true;
            default:
                return false;
        }
    }

    public bool UseConsumable()
    {
        if (equippedConsumable == null)
            return false;

        if (!TryGetItemData(equippedConsumable, out ItemDataSO data))
            return false;

        if (data.ItemType != ItemType.Consumable)
            return false;

        var stats = data.ConsumableStats;
        if (stats == null)
            return false;

        if (equippedConsumable is IStackableItem stackable && stackable.Count <= 0)
        {
            ClearConsumableSlot();
            return false;
        }

        if (healthComponent == null)
            healthComponent = GetComponent<HealthComponent>();

        if (healthComponent == null)
            return false;

        ApplyConsumableEffects(stats);
        equippedConsumable.Use();
        ConsumeEquippedConsumable();
        return true;
    }

    private void ApplyConsumableEffects(ConsumableStatsSO stats)
    {
        if (stats.healAmount > 0f)
            healthComponent.RestoreHealth(stats.healAmount);

        /*if (stats.regenAmountPerTick <= 0f || stats.regenDuration <= 0f)
            return;*/

        if (stats.extraSpeed > 0f)
        {
            StartCoroutine(SpeedTime( stats.speedTickInterval, stats.speedDuration, stats));
        }

        if (stats.extraAttack > 0)
        {
            ApplyAttackBoost(stats);
        }

        if (stats.maxHealth > 0)
        {
            ApplyMoreHealth(stats);
        }


        float tickInterval = stats.regenTickInterval > 0f ? stats.regenTickInterval : 1f;
        StartCoroutine(HealOverTime(stats.regenAmountPerTick, tickInterval, stats.regenDuration));
    }
    
    private void ApplyAttackBoost(ConsumableStatsSO stats)
    {
        if (stats.extraAttack <= 0f || statResolver == null)
            return;

        var modifier = new FlatStatModifier(StatType.AttackDamage, stats.extraAttack);
        statResolver.RegisterRuntimeModifier(modifier);

        StartCoroutine(RemoveRuntimeModifierAfterDuration(modifier, stats.attackDuration));
    }
    private void ApplyMoreHealth(ConsumableStatsSO stats)
    {
        if (stats.maxHealth <= 0f || statResolver == null)
            return;

        var modifier = new FlatStatModifier(StatType.MaxHealth, stats.maxHealth);
        statResolver.RegisterRuntimeModifier(modifier);
    }

    private IEnumerator RemoveRuntimeModifierAfterDuration(IStatModifier modifier, float duration)
    {
        yield return new WaitForSeconds(duration);

        if (statResolver != null)
            statResolver.UnregisterRuntimeModifier(modifier);
    }

    private IEnumerator HealOverTime(float amountPerTick, float tickInterval, float duration)
    {
        float elapsed = 0f;
        WaitForSeconds wait = new WaitForSeconds(tickInterval);

        while (elapsed + tickInterval <= duration)
        {
            yield return wait;

            if (healthComponent == null)
                yield break;

            healthComponent.RestoreHealth(amountPerTick);
            elapsed += tickInterval;
        }
    }
    private IEnumerator SpeedTime( float tickInterval, float duration, ConsumableStatsSO stats)
    {
        float elapsed = 0f;
        WaitForSeconds wait = new WaitForSeconds(tickInterval);
            
        playerController.CurrentSpeed += stats.extraSpeed;
    
        while (elapsed + tickInterval <= duration)
        {
            yield return wait;
    
            if (healthComponent == null)
                yield break;
    

            elapsed += tickInterval;
        }
            
        playerController.CurrentSpeed -= stats.extraSpeed;
            
    }

    private void ConsumeEquippedConsumable()
    {
        consumeAudioEvent?.Raise(equippedConsumable.UseSound);
        
        if (equippedConsumable is IStackableItem stackable)
        {
            stackable.Remove(1);

            if (stackable.Count <= 0)
            {
                ClearConsumableSlot();
                return;
            }

            SetSlotInViews(MiniInventorySlotType.Consumable, equippedConsumable);
            return;
        }

        ClearConsumableSlot();
    }

    private bool EquipItem(IItem item, bool requireInventory, bool removeFromInventory)
    {
        if (item == null)
            return false;

        if (!TryGetItemData(item, out ItemDataSO data))
            return false;

        Inventory inventory = interactor != null ? interactor.Inventory : null;
        if (requireInventory && (inventory == null || !inventory.Contains(item)))
            return false;

        switch (data.ItemType)
        {
            case ItemType.Weapon:
                return EquipEquipmentSlot(MiniInventorySlotType.Weapon, ref equippedWeapon, item, inventory, removeFromInventory, weaponModifiers);
            case ItemType.Armor:
                return EquipEquipmentSlot(MiniInventorySlotType.Armor, ref equippedArmor, item, inventory, removeFromInventory, armorModifiers);
            case ItemType.Consumable:
                return EquipConsumable(item, data, inventory, removeFromInventory);
            case ItemType.Passive:
                return EquipSlot(MiniInventorySlotType.Ability, ref equippedAbility, item, inventory, removeFromInventory);
            default:
                return false;
        }
    }

    private bool EquipConsumable(IItem item, ItemDataSO data, Inventory inventory, bool removeFromInventory)
    {
        if (equippedConsumable != null &&
            TryGetItemData(equippedConsumable, out ItemDataSO equippedData) &&
            equippedData == data &&
            equippedConsumable is IStackableItem equippedStack &&
            item is IStackableItem incomingStack)
        {
            if (removeFromInventory)
            {
                if (inventory == null || !inventory.RemoveAll(item))
                    return false;
            }

            equippedStack.Add(incomingStack.Count);
            SetSlotInViews(MiniInventorySlotType.Consumable, equippedConsumable);
            return true;
        }

        return EquipSlot(MiniInventorySlotType.Consumable, ref equippedConsumable, item, inventory, removeFromInventory);
    }

    private bool EquipEquipmentSlot(MiniInventorySlotType slotType, ref IItem currentItem, IItem newItem, Inventory inventory, bool removeFromInventory, List<IStatModifier> modifiers)
    {
        if (currentItem != null)
        {
            if (inventory == null)
                return false;

            if (!inventory.AddItem(currentItem))
                return false;
        }

        if (removeFromInventory)
        {
            if (inventory == null || !inventory.RemoveAll(newItem))
                return false;
        }

        ClearEquipmentModifiers(modifiers);
        currentItem = newItem;
        SetSlotInViews(slotType, newItem);
        ApplyEquipmentModifiers(slotType, newItem, modifiers);
        return true;
    }

    private bool EquipSlot(MiniInventorySlotType slotType, ref IItem currentItem, IItem newItem, Inventory inventory, bool removeFromInventory)
    {
        if (currentItem != null)
        {
            if (inventory == null)
                return false;

            if (!inventory.AddItem(currentItem))
                return false;
        }

        if (removeFromInventory)
        {
            if (inventory == null || !inventory.RemoveAll(newItem))
                return false;
        }

        currentItem = newItem;
        SetSlotInViews(slotType, newItem);
        return true;
    }

    private void ApplyEquipmentModifiers(MiniInventorySlotType slotType, IItem item, List<IStatModifier> modifiers)
    {
        if (statResolver == null || item == null)
            return;

        if (!TryGetItemData(item, out ItemDataSO data))
            return;

        switch (slotType)
        {
            case MiniInventorySlotType.Weapon:
                if (data.WeaponStats == null)
                    return;
                AddEquipmentModifier(StatType.AttackDamage, data.WeaponStats.damage, modifiers);
                AddEquipmentModifier(StatType.AttackSpeed, data.WeaponStats.attackSpeed, modifiers);
                break;
            case MiniInventorySlotType.Armor:
                if (data.ArmorStats == null)
                    return;
                AddEquipmentModifier(StatType.ShieldArmor, data.ArmorStats.defense, modifiers);
                AddEquipmentModifier(StatType.MaxHealth, data.ArmorStats.healthBonus, modifiers);
                break;
        }
    }

    private void AddEquipmentModifier(StatType type, float amount, List<IStatModifier> modifiers)
    {
        if (statResolver == null)
            return;

        if (Mathf.Approximately(amount, 0f))
            return;

        var modifier = new FlatStatModifier(type, amount);
        modifiers.Add(modifier);
        statResolver.RegisterEquipmentModifier(modifier);
    }

    private void ClearEquipmentModifiers(List<IStatModifier> modifiers)
    {
        if (modifiers == null || modifiers.Count == 0)
            return;

        if (statResolver != null)
        {
            for (int i = 0; i < modifiers.Count; i++)
                statResolver.UnregisterEquipmentModifier(modifiers[i]);
        }

        modifiers.Clear();
    }

    private void EquipStarterWeapon()
    {
        if (starterWeapon == null)
            return;

        if (equippedWeapon != null)
            return;

        if (starterWeapon.ItemType != ItemType.Weapon)
            return;

        var item = ItemFactory.CreateItem(starterWeapon);
        if (item == null)
            return;

        EquipItem(item, requireInventory: false, removeFromInventory: false);
    }

    private void ClearConsumableSlot()
    {
        equippedConsumable = null;
        ClearSlotInViews(MiniInventorySlotType.Consumable);
    }

    private void HandleSlotRightClicked(MiniInventorySlotType slotType)
    {
        TryUnequip(slotType);
    }

    private void RefreshView(MiniInventoryGridCanvas view)
    {
        if (view == null)
            return;

        view.SetSlot(MiniInventorySlotType.Weapon, equippedWeapon);
        view.SetSlot(MiniInventorySlotType.Armor, equippedArmor);
        view.SetSlot(MiniInventorySlotType.Consumable, equippedConsumable);
        view.SetSlot(MiniInventorySlotType.Ability, equippedAbility);
    }

    private void SetSlotInViews(MiniInventorySlotType slotType, IItem item)
    {
        ForEachView(view => view.SetSlot(slotType, item));
    }

    private void ClearSlotInViews(MiniInventorySlotType slotType)
    {
        ForEachView(view => view.ClearSlot(slotType));
    }

    private void ForEachView(System.Action<MiniInventoryGridCanvas> action)
    {
        if (action == null)
            return;

        CleanupViews();
        for (int i = 0; i < miniInventoryViews.Count; i++)
            action(miniInventoryViews[i]);
    }

    private void CleanupViews()
    {
        for (int i = miniInventoryViews.Count - 1; i >= 0; i--)
        {
            if (miniInventoryViews[i] == null)
                miniInventoryViews.RemoveAt(i);
        }
    }

    private void RegisterExistingViews()
    {
        var views = FindObjectsByType<MiniInventoryGridCanvas>(FindObjectsSortMode.None);
        for (int i = 0; i < views.Length; i++)
            RegisterView(views[i]);
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

    
    // Item decorators
    public bool HasLavaImmunity()
    {
        
        return HasLavaImmunity(equippedWeapon)
               || HasLavaImmunity(equippedArmor)
               || HasLavaImmunity(equippedAbility)
               || HasLavaImmunity(equippedConsumable);
    }

    private static bool HasLavaImmunity(IItem item)
    {
        if (item == null)
            return false;
        
        return item is ILavaImmunity lava && lava.IsImmuneToLava;
    }

}
