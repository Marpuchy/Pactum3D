using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ChestGridCanvas : MonoBehaviour, IInteractorBoundUI
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text currencyLabel;
    [SerializeField] private ItemGridView grid;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button takeAllButton;
    [SerializeField] private TMP_Text feedbackLabel;
    [SerializeField] private string inventoryFullMessage = "Inventario lleno";
    [SerializeField] private float feedbackDuration = 1.5f;
    [SerializeField] private bool closeOnKey = true;
    [SerializeField] private KeyCode closeKey = KeyCode.E;
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private int sortingOrder = GameplayUIState.GameplayCanvasSortOrder;

    [Header("Tooltip")]
    [SerializeField] private ItemStatsTooltip itemTooltip;

    private readonly List<IItem> itemBuffer = new();
    private readonly List<ItemStack> stackBuffer = new();
    private readonly IItemFactory itemFactory = new DefaultItemFactory();
    private InventorySO chestInventory;
    private Interactor interactor;
    private Coroutine feedbackRoutine;
    private ItemSlotView hoveredSlot;

    public bool IsVisible => root != null && root.activeSelf;

    private void Awake()
    {
        if (root == null)
            Debug.LogError($"{nameof(ChestGridCanvas)}: root is not assigned.", this);

        if (grid == null)
            Debug.LogError($"{nameof(ChestGridCanvas)}: grid is not assigned.", this);

        if (rootCanvas == null)
        {
            rootCanvas = root != null ? root.GetComponentInChildren<Canvas>(true) : null;
            if (rootCanvas == null)
                rootCanvas = GetComponentInChildren<Canvas>(true);
        }

        GameplayUIState.ConfigureCanvas(rootCanvas, sortingOrder);

        itemTooltip = ItemStatsTooltip.Ensure(itemTooltip, rootCanvas);

        if (root != null)
            root.SetActive(false);

        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        if (takeAllButton != null)
            takeAllButton.onClick.AddListener(HandleTakeAllClicked);

        if (grid != null)
        {
            grid.SetSlotClickHandler(HandleSlotClicked);
            grid.ForEachSlot(slot => slot.SetHoverHandlers(HandleSlotHoverEnter, HandleSlotHoverExit));
        }
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
    }

    public void Bind(Interactor interactor, InventorySO chestInventory)
    {
        this.interactor = interactor;
        this.chestInventory = chestInventory;
    }

    public void Show()
    {
        if (root != null)
            root.SetActive(true);

        GameplayUIState.Register(this);
        Refresh();
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);

        hoveredSlot = null;
        itemTooltip?.Hide();
        GameplayUIState.Unregister(this);
    }

    public void Refresh()
    {
        if (currencyLabel != null && interactor != null && interactor.Inventory != null)
            currencyLabel.text = interactor.Inventory.CurrencyAmount.ToString();

        if (titleLabel != null)
            titleLabel.text = "Chest";

        if (grid == null)
            return;

        CatalogItemFactory.FillFromStacks(chestInventory != null ? chestInventory.Items : null, itemBuffer);
        grid.RenderFromItems(itemBuffer, excludeCurrency: false);
        UpdateTooltip();
    }

    private void HandleSlotHoverEnter(ItemSlotView slot)
    {
        hoveredSlot = slot;
        UpdateTooltip();
    }

    private void HandleSlotHoverExit(ItemSlotView slot)
    {
        if (slot == null || hoveredSlot != slot)
            return;

        hoveredSlot = null;
        UpdateTooltip();
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

    private void HandleSlotClicked(ItemSlotView slot)
    {
        if (slot == null || !slot.HasItem) return;
        if (interactor == null || interactor.Inventory == null) return;
        if (chestInventory == null) return;

        ItemDataSO data = null;
        int amount = 1;

        if (slot.CurrentItem is IStackableItem stackable)
        {
            data = stackable.Data;
            if (data != null && data.IsCurrency)
                amount = Mathf.Max(1, stackable.Count);
        }
        else if (slot.CurrentItem is CatalogItemView catalogItem)
        {
            data = catalogItem.Data;
        }

        if (data == null) return;

        var itemInstance = itemFactory.Create(data, amount);
        if (itemInstance == null)
            return;

        if (!interactor.AddItem(itemInstance, showFeedback: false))
        {
            ShowFeedback(inventoryFullMessage);
            return;
        }

        if (!chestInventory.Remove(data, amount))
            return;

        Refresh();
    }

    private void HandleTakeAllClicked()
    {
        if (interactor == null || interactor.Inventory == null)
            return;

        if (chestInventory == null || chestInventory.Items == null)
            return;

        stackBuffer.Clear();
        var stacks = chestInventory.Items;
        for (int i = 0; i < stacks.Count; i++)
            stackBuffer.Add(stacks[i]);

        bool inventoryFull = false;
        for (int i = 0; i < stackBuffer.Count; i++)
        {
            var stack = stackBuffer[i];
            var data = stack.Item;
            if (data == null)
                continue;

            int amount = Mathf.Max(1, stack.Amount);

            if (data.Stackable || data.IsCurrency)
            {
                if (!TryAddToInventory(data, amount))
                {
                    inventoryFull = true;
                    break;
                }

                chestInventory.Remove(data, amount);
                continue;
            }

            for (int j = 0; j < amount; j++)
            {
                if (!TryAddToInventory(data, 1))
                {
                    inventoryFull = true;
                    break;
                }

                chestInventory.Remove(data, 1);
            }

            if (inventoryFull)
                break;
        }

        if (inventoryFull)
            ShowFeedback(inventoryFullMessage);

        Refresh();
    }

    private bool TryAddToInventory(ItemDataSO data, int amount)
    {
        if (data == null || interactor == null || interactor.Inventory == null)
            return false;

        var itemInstance = itemFactory.Create(data, amount);
        if (itemInstance == null)
            return false;

        return interactor.AddItem(itemInstance, showFeedback: false);
    }

    public void SetCloseKey(KeyCode key)
    {
        closeKey = key;
    }

    public void SetCloseOnKey(bool enabled)
    {
        closeOnKey = enabled;
    }

    private void OnDisable()
    {
        GameplayUIState.Unregister(this);
    }

    private void OnDestroy()
    {
        GameplayUIState.Unregister(this);
    }

    private void ShowFeedback(string message)
    {
        if (feedbackLabel == null)
        {
            Debug.LogWarning($"{nameof(ChestGridCanvas)}: feedbackLabel is not assigned.", this);
            return;
        }

        feedbackLabel.text = message ?? string.Empty;

        if (feedbackDuration <= 0f)
            return;

        if (feedbackRoutine != null)
            StopCoroutine(feedbackRoutine);

        feedbackRoutine = StartCoroutine(ClearFeedbackAfterDelay(feedbackDuration));
    }

    private IEnumerator ClearFeedbackAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (feedbackLabel != null)
            feedbackLabel.text = string.Empty;

        feedbackRoutine = null;
    }
}
