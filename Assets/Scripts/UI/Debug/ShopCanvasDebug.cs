using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ShopCanvasDebug : MonoBehaviour, IInteractorBoundUI
{
    [SerializeField] private List<ShopCatalogSO> catalogs = new();
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text currencyLabel;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private ShopItemRowView rowPrefab;
    [SerializeField] private Button closeButton;
    [SerializeField] private bool closeOnKey = true;
    [SerializeField] private KeyCode closeKey = KeyCode.E;
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private int sortingOrder = GameplayUIState.GameplayCanvasSortOrder;

    private readonly List<ShopItemRowView> rows = new();
    private readonly IItemFactory itemFactory = new DefaultItemFactory();
    private Interactor interactor;
    private ShopCatalogSO currentCatalog;

    public bool IsVisible => root != null && root.activeSelf;

    private void Awake()
    {
        if (root == null)
            Debug.LogError($"{nameof(ShopCanvasDebug)}: root is not assigned.", this);
        if (root != null)
            root.SetActive(false);

        if (contentRoot == null)
            Debug.LogError($"{nameof(ShopCanvasDebug)}: contentRoot is not assigned.", this);
        if (rowPrefab == null)
            Debug.LogError($"{nameof(ShopCanvasDebug)}: rowPrefab is not assigned.", this);

        if (rootCanvas == null)
        {
            rootCanvas = root != null ? root.GetComponentInChildren<Canvas>(true) : null;
            if (rootCanvas == null)
                rootCanvas = GetComponentInChildren<Canvas>(true);
        }

        GameplayUIState.ConfigureCanvas(rootCanvas, sortingOrder);

        CacheExistingRows();
        WarnIfContentRootHasNonRowChildren();

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    private void Update()
    {
        if (!IsVisible)
            return;

        if (closeOnKey && Input.GetKeyDown(closeKey))
            Close();
    }

    public void Bind(Interactor interactor)
    {
        this.interactor = interactor;
    }

    public void Show(OpenShopRequest request)
    {
        if (request.Interactor != null)
            interactor = request.Interactor;
        currentCatalog = request.Catalog != null ? request.Catalog : FindCatalog(request.ShopId);

        if (root != null)
            root.SetActive(true);

        GameplayUIState.Register(this);
        Refresh();
    }

    private ShopCatalogSO FindCatalog(string shopId)
    {
        if (string.IsNullOrWhiteSpace(shopId)) return null;

        for (int i = 0; i < catalogs.Count; i++)
        {
            if (catalogs[i] != null && catalogs[i].ShopId == shopId)
                return catalogs[i];
        }

        return null;
    }

    private void Refresh()
    {
        if (currencyLabel != null && interactor != null && interactor.Inventory != null)
            currencyLabel.text = $"Currency: {interactor.Inventory.CurrencyAmount}";

        if (titleLabel != null)
            titleLabel.text = currentCatalog != null ? currentCatalog.DisplayName : "Shop";

        if (contentRoot == null || rowPrefab == null)
            return;

        int rowIndex = 0;
        if (currentCatalog == null || currentCatalog.Items == null)
        {
            DisableUnusedRows(rowIndex);
            return;
        }

        var items = currentCatalog.Items;
        for (int i = 0; i < items.Count; i++)
        {
            var entry = items[i];
            if (entry.item == null) continue;

            int price = currentCatalog.GetPrice(entry);
            string name = string.IsNullOrWhiteSpace(entry.item.DisplayName) ? entry.item.name : entry.item.DisplayName;
            var row = GetOrCreateRow(rowIndex);
            if (row == null) break;

            bool canAfford = interactor != null
                && interactor.Inventory != null
                && price >= 0
                && interactor.Inventory.CurrencyAmount >= price;
            string buyLabel = $"Buy ({price})";
            var currentEntry = entry;
            int currentPrice = price;

            row.gameObject.SetActive(true);
            row.Set(name, price, entry.item.Icon, buyLabel, canAfford, () => TryBuy(currentEntry, currentPrice));
            rowIndex++;
        }

        DisableUnusedRows(rowIndex);
    }

    private void TryBuy(ShopItemEntry entry, int price)
    {
        if (interactor == null || interactor.Inventory == null) return;
        if (entry.item == null) return;
        if (price < 0) return;

        var itemInstance = itemFactory.Create(entry.item, 1);
        if (itemInstance == null)
            return;

        if (!interactor.Inventory.CanAddItem(itemInstance))
            return;

        if (price > 0 && !interactor.Inventory.SpendCurrency(price))
            return;

        interactor.AddItem(itemInstance);

        Refresh();
    }

    private void Close()
    {
        if (root != null)
            root.SetActive(false);

        GameplayUIState.Unregister(this);
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

    private void CacheExistingRows()
    {
        rows.Clear();
        if (contentRoot == null) return;

        var existing = contentRoot.GetComponentsInChildren<ShopItemRowView>(true);
        for (int i = 0; i < existing.Length; i++)
            rows.Add(existing[i]);
    }

    private void WarnIfContentRootHasNonRowChildren()
    {
        if (contentRoot == null) return;

        for (int i = 0; i < contentRoot.childCount; i++)
        {
            var child = contentRoot.GetChild(i);
            if (child.GetComponent<ShopItemRowView>() == null)
            {
                Debug.LogWarning(
                    $"{nameof(ShopCanvasDebug)}: contentRoot should only contain ShopItemRowView rows. Found '{child.name}'.",
                    this);
                break;
            }
        }
    }

    private ShopItemRowView GetOrCreateRow(int index)
    {
        if (index < rows.Count)
        {
            if (rows[index] == null && rowPrefab != null && contentRoot != null)
                rows[index] = Instantiate(rowPrefab, contentRoot);

            return rows[index];
        }

        if (rowPrefab == null || contentRoot == null)
            return null;

        var row = Instantiate(rowPrefab, contentRoot);
        rows.Add(row);
        return row;
    }

    private void DisableUnusedRows(int startIndex)
    {
        for (int i = startIndex; i < rows.Count; i++)
        {
            if (rows[i] != null)
                rows[i].gameObject.SetActive(false);
        }
    }
}
