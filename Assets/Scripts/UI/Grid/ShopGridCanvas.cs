using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ShopGridCanvas : MonoBehaviour, IInteractorBoundUI
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text currencyLabel;
    [SerializeField] private ItemGridView grid;
    [SerializeField] private Button closeButton;
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
    private readonly List<ShopItemListing> listings = new();
    private readonly IItemFactory itemFactory = new DefaultItemFactory();
    private readonly Dictionary<ShopCatalogSO, HashSet<ItemDataSO>> purchasedByCatalog = new();
    private Interactor interactor;
    private ShopCatalogSO catalog;
    private Coroutine feedbackRoutine;
    private ItemSlotView hoveredSlot;

    public bool IsVisible => root != null && root.activeSelf;

    private struct ShopItemListing
    {
        public readonly IItem Item;
        public readonly ItemDataSO Data;
        public readonly int Price;

        public ShopItemListing(IItem item, ItemDataSO data, int price)
        {
            Item = item;
            Data = data;
            Price = price;
        }
    }

    private void Awake()
    {
        if (root == null)
            Debug.LogError($"{nameof(ShopGridCanvas)}: root is not assigned.", this);

        if (grid == null)
            Debug.LogError($"{nameof(ShopGridCanvas)}: grid is not assigned.", this);

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

    public void Bind(Interactor interactor, ShopCatalogSO catalog)
    {
        this.interactor = interactor;
        this.catalog = catalog;
        EnsurePurchasedSet();
        BuildListings();
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
            titleLabel.text = catalog != null ? catalog.DisplayName : "Shop";

        if (grid == null)
            return;

        FillRenderBuffer();
        grid.RenderFromItems(itemBuffer, excludeCurrency: false);
        ApplyPricesToSlots();
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

    private void BuildListings()
    {
        listings.Clear();

        if (catalog == null || catalog.Items == null)
            return;

        EnsurePurchasedSet();
        var purchased = purchasedByCatalog[catalog];

        var entries = catalog.Items;
        for (int i = 0; i < entries.Count; i++)
        {
            var data = entries[i].item;
            if (data == null) continue;
            if (purchased.Contains(data)) continue;

            int price = Mathf.Max(0, catalog.GetPrice(entries[i]));
            IItem wrapper = data.Stackable || data.IsCurrency
                ? new CatalogStackableItemView(data, 1)
                : new CatalogItemView(data);

            listings.Add(new ShopItemListing(wrapper, data, price));
        }
    }

    private void FillRenderBuffer()
    {
        itemBuffer.Clear();
        for (int i = 0; i < listings.Count; i++)
        {
            if (listings[i].Item != null)
                itemBuffer.Add(listings[i].Item);
        }
    }

    private void ApplyPricesToSlots()
    {
        if (grid == null)
            return;

        grid.ForEachSlot(slot =>
        {
            if (slot == null)
                return;

            if (!slot.HasItem)
            {
                slot.ClearPrice();
                return;
            }

            int listingIndex = FindListingIndex(slot.CurrentItem);
            if (listingIndex >= 0)
                slot.SetPrice(listings[listingIndex].Price);
            else
                slot.ClearPrice();
        });
    }

    private void HandleSlotClicked(ItemSlotView slot)
    {
        if (slot == null || !slot.HasItem) return;
        if (interactor == null || interactor.Inventory == null) return;

        int listingIndex = FindListingIndex(slot.CurrentItem);
        if (listingIndex < 0) return;

        var listing = listings[listingIndex];
        if (listing.Data == null) return;

        var itemInstance = itemFactory.Create(listing.Data, 1);
        if (itemInstance == null)
            return;

        if (listing.Price > 0 && !interactor.Inventory.SpendCurrency(listing.Price))
            return;

        if (!interactor.AddItem(itemInstance, showFeedback: false))
        {
            if (listing.Price > 0)
                interactor.Inventory.AddCurrency(listing.Price);

            ShowFeedback(inventoryFullMessage);
            return;
        }

        EnsurePurchasedSet();
        purchasedByCatalog[catalog].Add(listing.Data);
        listings.RemoveAt(listingIndex);
        Refresh();
    }

    private int FindListingIndex(IItem item)
    {
        for (int i = 0; i < listings.Count; i++)
        {
            if (ReferenceEquals(listings[i].Item, item))
                return i;
        }

        return -1;
    }

    private void EnsurePurchasedSet()
    {
        if (catalog == null) return;
        if (!purchasedByCatalog.ContainsKey(catalog))
            purchasedByCatalog[catalog] = new HashSet<ItemDataSO>();
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
            Debug.LogWarning($"{nameof(ShopGridCanvas)}: feedbackLabel is not assigned.", this);
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
