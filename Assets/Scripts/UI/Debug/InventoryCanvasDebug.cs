using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class InventoryCanvasDebug : MonoBehaviour, IInteractorBoundUI
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text currencyLabel;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private ItemRowView rowPrefab;
    [SerializeField] private float refreshInterval = 0.25f;
    [SerializeField] private PlayerMiniInventory miniInventory;
    [SerializeField] private Button closeButton;
    [SerializeField] private bool closeOnKey = true;
    [SerializeField] private KeyCode closeKey = KeyCode.I;
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private int sortingOrder = GameplayUIState.GameplayCanvasSortOrder;

    private readonly List<ItemRowView> rows = new();
    private Interactor interactor;
    private float nextRefreshTime;

    public bool IsVisible => root != null && root.activeSelf;

    private void Awake()
    {
        if (root == null) Debug.LogError($"{nameof(InventoryCanvasDebug)}: root is not assigned.", this);
        if (contentRoot == null) Debug.LogError($"{nameof(InventoryCanvasDebug)}: contentRoot is not assigned.", this);
        if (rowPrefab == null) Debug.LogError($"{nameof(InventoryCanvasDebug)}: rowPrefab is not assigned.", this);

        if (miniInventory == null)
            miniInventory = FindFirstObjectByType<PlayerMiniInventory>();

        if (rootCanvas == null)
        {
            rootCanvas = root != null ? root.GetComponentInChildren<Canvas>(true) : null;
            if (rootCanvas == null)
                rootCanvas = GetComponentInChildren<Canvas>(true);
        }

        GameplayUIState.ConfigureCanvas(rootCanvas, sortingOrder);

        if (root != null) root.SetActive(false);
        CacheExistingRows();
        WarnIfContentRootHasNonRowChildren();

        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
    }

    private void Update()
    {
        if (root == null || !root.activeSelf) return;

        if (closeOnKey && Input.GetKeyDown(closeKey))
        {
            Hide();
            return;
        }

        if (Time.unscaledTime < nextRefreshTime) return;

        nextRefreshTime = Time.unscaledTime + refreshInterval;
        Refresh();
    }

    public void Bind(Interactor interactor)
    {
        this.interactor = interactor;
    }

    public void Show()
    {
        if (root != null) root.SetActive(true);
        GameplayUIState.Register(this);
        nextRefreshTime = 0f;
        Refresh();
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
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

    public void Refresh()
    {
        if (interactor == null) return;
        var inventory = interactor.Inventory;
        if (inventory == null) return;

        if (currencyLabel != null)
            currencyLabel.text = $"{inventory.CurrencyAmount}";

        if (contentRoot == null || rowPrefab == null) return;

        var items = inventory.Items;
        var rowIndex = 0;

        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] == null) continue;

            if (items[i] is IStackableItem stackableCurrency
                && stackableCurrency.Data != null
                && stackableCurrency.Data.IsCurrency)
            {
                continue;
            }

            var amount = 1;
            if (items[i] is IStackableItem stackable)
                amount = stackable.Count;

            var row = GetOrCreateRow(rowIndex);
            if (row == null) break;

            row.gameObject.SetActive(true);
            var currentItem = items[i];
            row.Set(currentItem.Name, amount, currentItem.Icon);
            if (miniInventory != null && currentItem is IItemDataProvider)
            {
                row.SetAction("Equip", true, () =>
                {
                    if (miniInventory.TryEquip(currentItem))
                        Refresh();
                });
            }
            else
            {
                row.SetAction("Use", true, () =>
                {
                    if (interactor == null || interactor.Inventory == null) return;
                    interactor.Inventory.UseItem(currentItem);
                    Refresh();
                });
            }
            rowIndex++;
        }

        for (var i = rowIndex; i < rows.Count; i++)
        {
            if (rows[i] != null)
                rows[i].gameObject.SetActive(false);
        }
    }

    private void CacheExistingRows()
    {
        rows.Clear();
        if (contentRoot == null) return;

        var existing = contentRoot.GetComponentsInChildren<ItemRowView>(true);
        for (var i = 0; i < existing.Length; i++)
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
                    $"{nameof(InventoryCanvasDebug)}: contentRoot should only contain ItemRowView rows. Found '{child.name}'.",
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
}
