using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class InventoryGridCanvas : MonoBehaviour, IInteractorBoundUI
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text currencyLabel;
    [SerializeField] private ItemGridView grid;
    [SerializeField] private PlayerMiniInventory miniInventory;
    [SerializeField] private MiniInventoryGridCanvas miniInventoryView;
    [SerializeField] private bool dropWholeStack = true;

    [Header("Close")]
    [SerializeField] private Button closeButton;
    [SerializeField] private bool closeOnKey = true;
    [SerializeField] private KeyCode closeKey = KeyCode.I;
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private int sortingOrder = GameplayUIState.GameplayCanvasSortOrder;

    [Header("Action Buttons")]
    [SerializeField] private Button primaryActionButton;
    [SerializeField] private TMP_Text primaryActionLabel;
    [SerializeField] private Button secondaryActionButton;
    [SerializeField] private TMP_Text secondaryActionLabel;
    [SerializeField] private string inventoryPrimaryActionText = "Equipar";
    [SerializeField] private string inventorySecondaryActionText = "Dropear";
    [SerializeField] private string miniPrimaryActionText = "Equipado";
    [SerializeField] private string miniSecondaryActionText = "Desequipar";

    [Header("Tooltip")]
    [SerializeField] private ItemStatsTooltip itemTooltip;

    private Interactor interactor;
    private readonly Dictionary<ItemSlotView, SlotContext> slotContexts = new();
    private readonly List<IItem> slotItems = new();
    private readonly List<IItem> inventoryBuffer = new();
    private ItemSlotView hoveredSlot;
    private SlotContext hoveredContext;
    private System.Action primaryAction;
    private System.Action secondaryAction;
    private bool miniInventorySubscribed;

    private enum SlotContextKind
    {
        None,
        Inventory,
        MiniInventory
    }

    private struct SlotContext
    {
        public SlotContextKind Kind;
        public MiniInventorySlotType MiniSlotType;
        public int InventoryIndex;

        public SlotContext(SlotContextKind kind, MiniInventorySlotType miniSlotType, int inventoryIndex)
        {
            Kind = kind;
            MiniSlotType = miniSlotType;
            InventoryIndex = inventoryIndex;
        }
    }

    public bool IsVisible => root != null && root.activeSelf;

    private void Awake()
    {
        if (root == null)
            Debug.LogError($"{nameof(InventoryGridCanvas)}: root is not assigned.", this);

        if (grid == null)
            Debug.LogError($"{nameof(InventoryGridCanvas)}: grid is not assigned.", this);

        if (miniInventory == null)
            miniInventory = FindFirstObjectByType<PlayerMiniInventory>();

        if (miniInventoryView == null)
            miniInventoryView = GetComponentInChildren<MiniInventoryGridCanvas>(true);

        if (rootCanvas == null)
        {
            rootCanvas = root != null ? root.GetComponentInChildren<Canvas>(true) : null;
            if (rootCanvas == null)
                rootCanvas = GetComponentInChildren<Canvas>(true);
        }

        GameplayUIState.ConfigureCanvas(rootCanvas, sortingOrder);

        itemTooltip = ItemStatsTooltip.Ensure(itemTooltip, rootCanvas);

        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        if (root != null)
            root.SetActive(false);

        if (grid != null)
            grid.SetSlotClickHandlers(HandleSlotClicked, HandleSlotRightClicked);

        RegisterSlotContexts();
        BindMiniInventoryView();
        SubscribeMiniInventory();

        if (primaryActionButton != null)
            primaryActionButton.onClick.AddListener(InvokePrimaryAction);
        if (secondaryActionButton != null)
            secondaryActionButton.onClick.AddListener(InvokeSecondaryAction);

        UpdateActionButtons();
    }

    private void Update()
    {
        if (!IsVisible)
            return;

        if (closeOnKey && Input.GetKeyDown(closeKey))
            Hide();
    }

    public void Bind(Interactor interactor)
    {
        this.interactor = interactor;
        SubscribeMiniInventory();
    }

    public void Show()
    {
        if (root != null)
        {
            root.SetActive(true);
            EnsureRootScale();
        }

        GameplayUIState.Register(this);
        BindMiniInventoryView();
        SubscribeMiniInventory();
        Refresh();
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);

        itemTooltip?.Hide();
        GameplayUIState.Unregister(this);
        hoveredSlot = null;
        hoveredContext = default;
        UpdateActionButtons();
    }

    public void Refresh()
    {
        if (interactor == null || interactor.Inventory == null)
        {
            UpdateActionButtons();
            return;
        }

        if (currencyLabel != null)
            currencyLabel.text = interactor.Inventory.CurrencyAmount.ToString();

        if (grid == null)
        {
            UpdateActionButtons();
            return;
        }

        EnsureSlotItems();
        SyncSlotItemsWithInventory();
        grid.RenderFromSlotList(slotItems, excludeCurrency: true);
        UpdateActionButtons();
    }

    private void HandleSlotClicked(ItemSlotView slot)
    {
        if (slot == null || !slot.HasItem)
            return;

        if (miniInventory == null)
            miniInventory = FindFirstObjectByType<PlayerMiniInventory>();

        if (miniInventory != null && miniInventory.TryEquip(slot.CurrentItem))
            Refresh();
    }

    private void HandleSlotRightClicked(ItemSlotView slot)
    {
        if (slot == null || !slot.HasItem)
            return;

        if (interactor == null)
            return;

        if (interactor.DropItemFromInventory(slot.CurrentItem, dropWholeStack))
            Refresh();
    }

    private void RegisterSlotContexts()
    {
        slotContexts.Clear();

        if (grid != null)
        {
            grid.ForEachSlot((index, slot) =>
            {
                slotContexts[slot] = new SlotContext(SlotContextKind.Inventory, default, index);
                slot.SetHoverHandlers(HandleSlotHoverEnter, HandleSlotHoverExit);
                slot.SetDropHandler(HandleSlotDropped);
            });
        }

        if (miniInventoryView != null)
        {
            miniInventoryView.ForEachSlot((slotType, slot) =>
            {
                slotContexts[slot] = new SlotContext(SlotContextKind.MiniInventory, slotType, -1);
                slot.SetHoverHandlers(HandleSlotHoverEnter, HandleSlotHoverExit);
                slot.SetDropHandler(HandleSlotDropped);
            });
        }
    }

    private void HandleSlotHoverEnter(ItemSlotView slot)
    {
        if (slot == null || !slotContexts.TryGetValue(slot, out var context))
            return;

        hoveredSlot = slot;
        hoveredContext = context;
        UpdateActionButtons();
    }

    private void HandleSlotHoverExit(ItemSlotView slot)
    {
        if (slot == null || hoveredSlot != slot)
            return;

        hoveredSlot = null;
        hoveredContext = default;
        UpdateActionButtons();
    }

    private void HandleSlotDropped(ItemSlotView target, ItemSlotView source)
    {
        if (target == null || source == null || target == source)
            return;

        if (!source.HasItem)
            return;

        if (!slotContexts.TryGetValue(target, out var targetContext))
            return;

        if (!slotContexts.TryGetValue(source, out var sourceContext))
            return;

        if (targetContext.Kind == SlotContextKind.Inventory &&
            sourceContext.Kind == SlotContextKind.Inventory)
        {
            if (TrySwapInventorySlots(sourceContext.InventoryIndex, targetContext.InventoryIndex))
                Refresh();

            return;
        }

        if (targetContext.Kind == SlotContextKind.MiniInventory &&
            sourceContext.Kind == SlotContextKind.Inventory)
        {
            if (!IsCompatibleWithMiniSlot(source.CurrentItem, targetContext.MiniSlotType))
                return;

            EnsureMiniInventory();
            if (miniInventory != null && miniInventory.TryEquip(source.CurrentItem))
                Refresh();

            return;
        }

        if (targetContext.Kind == SlotContextKind.Inventory &&
            sourceContext.Kind == SlotContextKind.MiniInventory)
        {
            if (!CanUnequipSlot(sourceContext.MiniSlotType))
                return;

            EnsureMiniInventory();
            if (miniInventory != null && miniInventory.TryUnequip(sourceContext.MiniSlotType))
                Refresh();
        }
    }

    private bool TrySwapInventorySlots(int sourceIndex, int targetIndex)
    {
        if (sourceIndex < 0 || targetIndex < 0)
            return false;

        EnsureSlotItems();
        SyncSlotItemsWithInventory();

        if (sourceIndex >= slotItems.Count || targetIndex >= slotItems.Count)
            return false;

        if (sourceIndex == targetIndex)
            return false;

        var sourceItem = slotItems[sourceIndex];
        if (sourceItem == null)
            return false;

        var targetItem = slotItems[targetIndex];
        slotItems[sourceIndex] = targetItem;
        slotItems[targetIndex] = sourceItem;
        return true;
    }

    private void EnsureSlotItems()
    {
        if (grid == null)
            return;

        int slotCount = grid.GetSlotCount();
        if (slotCount <= 0)
            return;

        if (slotItems.Count < slotCount)
        {
            while (slotItems.Count < slotCount)
                slotItems.Add(null);
        }
        else if (slotItems.Count > slotCount)
        {
            slotItems.RemoveRange(slotCount, slotItems.Count - slotCount);
        }
    }

    private void SyncSlotItemsWithInventory()
    {
        if (slotItems.Count == 0)
            return;

        if (interactor == null || interactor.Inventory == null)
        {
            for (int i = 0; i < slotItems.Count; i++)
                slotItems[i] = null;
            return;
        }

        inventoryBuffer.Clear();
        var items = interactor.Inventory.Items;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item == null)
                continue;
            if (IsCurrency(item))
                continue;

            inventoryBuffer.Add(item);
        }

        for (int i = 0; i < slotItems.Count; i++)
        {
            var item = slotItems[i];
            if (item == null)
                continue;

            if (!ContainsReference(inventoryBuffer, item))
                slotItems[i] = null;
        }

        for (int i = 0; i < inventoryBuffer.Count; i++)
        {
            var item = inventoryBuffer[i];
            if (ContainsReference(slotItems, item))
                continue;

            int emptyIndex = FindEmptySlotIndex();
            if (emptyIndex < 0)
                break;

            slotItems[emptyIndex] = item;
        }
    }

    private static bool ContainsReference(List<IItem> items, IItem target)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (ReferenceEquals(items[i], target))
                return true;
        }

        return false;
    }

    private int FindEmptySlotIndex()
    {
        for (int i = 0; i < slotItems.Count; i++)
        {
            if (slotItems[i] == null)
                return i;
        }

        return -1;
    }

    private static bool IsCurrency(IItem item)
    {
        if (item is IStackableItem stackable && stackable.Data != null)
            return stackable.Data.IsCurrency;

        return false;
    }

    private void EnsureMiniInventory()
    {
        if (miniInventory == null)
            miniInventory = FindFirstObjectByType<PlayerMiniInventory>();
    }

    private void BindMiniInventoryView()
    {
        if (miniInventoryView == null)
            return;

        EnsureMiniInventory();
        if (miniInventory != null)
            miniInventory.BindView(miniInventoryView);
    }

    private void SubscribeMiniInventory()
    {
        if (miniInventorySubscribed)
            return;

        EnsureMiniInventory();
        if (miniInventory == null)
            return;

        miniInventory.Unequipped += HandleMiniInventoryUnequipped;
        miniInventorySubscribed = true;
    }

    private void UnsubscribeMiniInventory()
    {
        if (!miniInventorySubscribed)
            return;

        if (miniInventory != null)
            miniInventory.Unequipped -= HandleMiniInventoryUnequipped;

        miniInventorySubscribed = false;
    }

    private void HandleMiniInventoryUnequipped()
    {
        if (IsVisible)
            Refresh();
    }

    private void OnDisable()
    {
        GameplayUIState.Unregister(this);
    }

    private void OnDestroy()
    {
        GameplayUIState.Unregister(this);
        UnsubscribeMiniInventory();
    }

    private void UpdateActionButtons()
    {
        primaryAction = null;
        secondaryAction = null;

        SetActionButton(primaryActionButton, primaryActionLabel, string.Empty, false);
        SetActionButton(secondaryActionButton, secondaryActionLabel, string.Empty, false);

        UpdateTooltip();

        if (hoveredSlot == null || !hoveredSlot.HasItem)
            return;

        if (!slotContexts.TryGetValue(hoveredSlot, out var context))
            return;

        if (context.Kind == SlotContextKind.Inventory)
        {
            ConfigureInventoryActions(hoveredSlot);
            return;
        }

        if (context.Kind == SlotContextKind.MiniInventory)
            ConfigureMiniInventoryActions(context.MiniSlotType);
    }

    private void UpdateTooltip()
    {
        if (itemTooltip == null)
            return;

        if (hoveredSlot != null && hoveredSlot.HasItem)
            itemTooltip.ShowForItem(hoveredSlot.CurrentItem);
        else
            itemTooltip.Hide();
    }

    private void ConfigureInventoryActions(ItemSlotView slot)
    {
        bool canEquip = CanEquipItem(slot.CurrentItem);
        primaryAction = canEquip ? () => HandleSlotClicked(slot) : null;
        secondaryAction = () => HandleSlotRightClicked(slot);

        SetActionButton(primaryActionButton, primaryActionLabel, inventoryPrimaryActionText, canEquip);
        SetActionButton(secondaryActionButton, secondaryActionLabel, inventorySecondaryActionText, true);
    }

    private void ConfigureMiniInventoryActions(MiniInventorySlotType slotType)
    {
        bool canUnequip = CanUnequipSlot(slotType);
        primaryAction = null;
        secondaryAction = canUnequip ? () => HandleMiniSlotRightClicked(slotType) : null;

        SetActionButton(primaryActionButton, primaryActionLabel, miniPrimaryActionText, false);
        SetActionButton(secondaryActionButton, secondaryActionLabel, miniSecondaryActionText, canUnequip);
    }

    private void HandleMiniSlotRightClicked(MiniInventorySlotType slotType)
    {
        EnsureMiniInventory();
        if (miniInventory != null && miniInventory.TryUnequip(slotType))
            Refresh();
    }

    private void InvokePrimaryAction()
    {
        primaryAction?.Invoke();
    }

    private void InvokeSecondaryAction()
    {
        secondaryAction?.Invoke();
    }

    private static void SetActionButton(Button button, TMP_Text label, string text, bool interactable)
    {
        if (label != null)
            label.text = text ?? string.Empty;

        if (button != null)
            button.interactable = interactable;
    }

    private static bool CanEquipItem(IItem item)
    {
        return TryGetMiniSlotTypeForItem(item, out _);
    }

    private static bool CanUnequipSlot(MiniInventorySlotType slotType)
    {
        return slotType != MiniInventorySlotType.Weapon &&
               slotType != MiniInventorySlotType.Ability;
    }

    private static bool IsCompatibleWithMiniSlot(IItem item, MiniInventorySlotType targetSlot)
    {
        return TryGetMiniSlotTypeForItem(item, out var slotType) && slotType == targetSlot;
    }

    private static bool TryGetMiniSlotTypeForItem(IItem item, out MiniInventorySlotType slotType)
    {
        slotType = default;
        if (item is not IItemDataProvider provider || provider.Data == null)
            return false;

        switch (provider.Data.ItemType)
        {
            case ItemType.Weapon:
                slotType = MiniInventorySlotType.Weapon;
                return true;
            case ItemType.Armor:
                slotType = MiniInventorySlotType.Armor;
                return true;
            case ItemType.Consumable:
                slotType = MiniInventorySlotType.Consumable;
                return true;
            case ItemType.Passive:
                slotType = MiniInventorySlotType.Ability;
                return true;
            default:
                return false;
        }
    }

    public void SetCloseKey(KeyCode key)
    {
        closeKey = key;
    }

    public void SetCloseOnKey(bool enabled)
    {
        closeOnKey = enabled;
    }

    private void EnsureRootScale()
    {
        var rootTransform = root != null ? root.transform : null;
        if (rootTransform == null)
            return;

        if (rootTransform.localScale.sqrMagnitude < 0.0001f)
            rootTransform.localScale = Vector3.one;
    }
}
