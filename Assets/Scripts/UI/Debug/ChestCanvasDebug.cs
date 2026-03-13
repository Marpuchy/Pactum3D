using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ChestCanvasDebug : MonoBehaviour, IInteractorBoundUI
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text currencyLabel;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private ItemRowView rowPrefab;
    [SerializeField] private Button takeAllButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private bool closeOnKey = true;
    [SerializeField] private KeyCode closeKey = KeyCode.E;
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private int sortingOrder = GameplayUIState.GameplayCanvasSortOrder;

    private readonly List<ItemRowView> rows = new();
    private readonly IItemFactory itemFactory = new DefaultItemFactory();
    private InventorySO chestInventory;
    private Interactor interactor;

    public bool IsVisible => root != null && root.activeSelf;

    private void Awake()
    {
        if (root == null)
            Debug.LogError($"{nameof(ChestCanvasDebug)}: root is not assigned.", this);
        if (root != null)
            root.SetActive(false);

        if (contentRoot == null)
            Debug.LogError($"{nameof(ChestCanvasDebug)}: contentRoot is not assigned.", this);
        if (rowPrefab == null)
            Debug.LogError($"{nameof(ChestCanvasDebug)}: rowPrefab is not assigned.", this);

        if (rootCanvas == null)
        {
            rootCanvas = root != null ? root.GetComponentInChildren<Canvas>(true) : null;
            if (rootCanvas == null)
                rootCanvas = GetComponentInChildren<Canvas>(true);
        }

        GameplayUIState.ConfigureCanvas(rootCanvas, sortingOrder);

        CacheExistingRows();
        WarnIfContentRootHasNonRowChildren();

        if (takeAllButton != null)
            takeAllButton.onClick.AddListener(TakeAll);

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

    public void Show(OpenChestRequest request)
    {
        if (request.Interactor != null)
            interactor = request.Interactor;
        chestInventory = request.ChestInventory;

        if (root != null)
            root.SetActive(true);

        GameplayUIState.Register(this);
        Refresh();
    }

    private void Refresh()
    {
        if (currencyLabel != null && interactor != null && interactor.Inventory != null)
            currencyLabel.text = $"Currency: {interactor.Inventory.CurrencyAmount}";

        if (titleLabel != null)
            titleLabel.text = "Chest";

        if (contentRoot == null || rowPrefab == null || chestInventory == null)
            return;

        var items = chestInventory.Items;
        int rowIndex = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].Item == null) continue;

            var data = items[i].Item;
            var row = GetOrCreateRow(rowIndex);
            if (row == null) break;

            row.gameObject.SetActive(true);
            string name = string.IsNullOrWhiteSpace(data.DisplayName) ? data.name : data.DisplayName;
            row.Set(name, items[i].Amount, data.Icon);
            var currentData = data;
            row.SetAction("Take", true, () => TakeOne(currentData));
            rowIndex++;
        }

        DisableUnusedRows(rowIndex);
    }

    private void TakeAll()
    {
        if (interactor == null || interactor.Inventory == null || chestInventory == null)
            return;

        var items = chestInventory.Items;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].Item == null) continue;

            var data = items[i].Item;
            int amount = items[i].Amount;
            if (data.Stackable || data.IsCurrency)
            {
                var itemInstance = itemFactory.Create(data, amount);
                if (itemInstance != null)
                    interactor.AddItem(itemInstance);
                continue;
            }

            for (int j = 0; j < amount; j++)
            {
                var itemInstance = itemFactory.Create(data, 1);
                if (itemInstance != null)
                    interactor.AddItem(itemInstance);
            }
        }

        chestInventory.Clear();
        Refresh();
    }

    private void TakeOne(ItemDataSO data)
    {
        if (data == null || interactor == null || interactor.Inventory == null || chestInventory == null)
            return;

        if (!chestInventory.Remove(data, 1))
            return;

        var itemInstance = itemFactory.Create(data, 1);
        if (itemInstance != null)
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

        var existing = contentRoot.GetComponentsInChildren<ItemRowView>(true);
        for (int i = 0; i < existing.Length; i++)
            rows.Add(existing[i]);
    }

    private void WarnIfContentRootHasNonRowChildren()
    {
        if (contentRoot == null) return;

        for (int i = 0; i < contentRoot.childCount; i++)
        {
            var child = contentRoot.GetChild(i);
            if (child.GetComponent<ItemRowView>() == null)
            {
                Debug.LogWarning(
                    $"{nameof(ChestCanvasDebug)}: contentRoot should only contain ItemRowView rows. Found '{child.name}'.",
                    this);
                break;
            }
        }
    }

    private ItemRowView GetOrCreateRow(int index)
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
