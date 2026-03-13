using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ItemSlotView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("UI References")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text amountLabel;
    [SerializeField] private TMP_Text priceLabel;
    [SerializeField] private Image priceIcon;
    [SerializeField] private GameObject emptyOverlay;

    [Header("Rarity Visuals")]
    [SerializeField] private Image backgroundImage; // fondo del slot
    [SerializeField] private Image borderImage;     // marco del slot

    [Header("Empty Visuals")]
    [SerializeField] private Sprite emptyBackgroundSprite;
    [SerializeField] private Sprite emptyBorderSprite;

    [Header("Rarity Sprites Mapping")]
    [SerializeField] private ItemRaritySpriteMapping[] rarityMappings;

    private Action<ItemSlotView> leftClickHandler;
    private Action<ItemSlotView> rightClickHandler;
    private Action<ItemSlotView> hoverEnterHandler;
    private Action<ItemSlotView> hoverExitHandler;
    private Action<ItemSlotView, ItemSlotView> dropHandler;
    private IItem currentItem;
    private int currentAmount;
    private bool isHovered;
    private bool isSelected;
    private bool isDragging;
    private RectTransform dragIcon;
    private Canvas dragCanvas;

    public IItem CurrentItem => currentItem;
    public int CurrentAmount => currentAmount;
    public bool HasItem => currentItem != null;
    
    [System.Serializable]
    private struct ItemRaritySpriteMapping
    {
        public ItemRaritySO raritySO;
        public Sprite background;
        public Sprite border;
    }
    
    private void Awake()
    {
        if (backgroundImage == null)
            backgroundImage = transform.Find("Background")?.GetComponent<Image>();

        if (borderImage == null)
            borderImage = transform.Find("Border")?.GetComponent<Image>();

        if (priceLabel == null)
            priceLabel = transform.Find("Price")?.GetComponent<TMP_Text>();

        if (priceLabel == null)
            priceLabel = transform.Find("PriceLabel")?.GetComponent<TMP_Text>();

        if (priceLabel == null)
            priceLabel = transform.Find("PriceText")?.GetComponent<TMP_Text>();

        if (priceIcon == null)
            priceIcon = transform.Find("PriceIcon")?.GetComponent<Image>();

        if (priceIcon == null)
            priceIcon = transform.Find("CoinIcon")?.GetComponent<Image>();

        if (priceIcon == null)
            priceIcon = transform.Find("CurrencyIcon")?.GetComponent<Image>();

        UpdateVisuals();
    }

    private void OnDisable()
    {
        isHovered = false;
        isDragging = false;
        DestroyDragIcon();
        ApplyBorderVisibility();
    }


    public void Set(IItem item, int amount)
    {
        currentItem = item;
        currentAmount = Mathf.Max(1, amount);

        if (icon != null)
        {
            icon.sprite = item?.Icon;
            icon.enabled = item != null && icon.sprite != null;
        }

        if (amountLabel != null)
            amountLabel.text = (item != null && currentAmount > 1) ? currentAmount.ToString() : string.Empty;

        ClearPrice();

        if (emptyOverlay != null)
            emptyOverlay.SetActive(item == null);

        UpdateVisuals();
    }


    private void UpdateVisuals()
    {
        if (currentItem is IItemDataProvider provider && provider.Data != null)
        {
            var raritySO = provider.Data.Rarity;
            if (raritySO != null && rarityMappings != null)
            {
                for (int i = 0; i < rarityMappings.Length; i++)
                {
                    var mapping = rarityMappings[i];
                    if (ReferenceEquals(mapping.raritySO, raritySO))
                    {
                        SetRaritySprites(mapping.background, mapping.border);
                        return;
                    }
                }
            }
        }

        // Slot vacío o rareza no encontrada
        SetRaritySprites(emptyBackgroundSprite, emptyBorderSprite);
    }
    
    private void SetRaritySprites(Sprite bg, Sprite border)
    {
        if (backgroundImage != null)
            backgroundImage.sprite = bg;

        if (borderImage != null)
            borderImage.sprite = border;

        ApplyBorderVisibility();
    }

    public void Clear()
    {
        currentItem = null;
        currentAmount = 0;

        if (icon != null)
        {
            icon.sprite = null;
            icon.enabled = false;
        }

        if (amountLabel != null)
            amountLabel.text = string.Empty;

        ClearPrice();

        if (emptyOverlay != null)
            emptyOverlay.SetActive(true);

        UpdateVisuals();
    }

    public void SetPrice(int price)
    {
        if (priceLabel == null)
        {
            SetPriceIconVisible(price >= 0);
            return;
        }

        bool hasPrice = price >= 0;
        priceLabel.text = hasPrice ? price.ToString() : string.Empty;
        SetPriceIconVisible(hasPrice);
    }

    public void ClearPrice()
    {
        if (priceLabel != null)
            priceLabel.text = string.Empty;

        SetPriceIconVisible(false);
    }

    private void SetPriceIconVisible(bool visible)
    {
        if (priceIcon != null)
            priceIcon.gameObject.SetActive(visible);
    }

    public void SetClickHandler(Action<ItemSlotView> handler)
    {
        leftClickHandler = handler;
        rightClickHandler = null;
    }

    public void SetClickHandlers(Action<ItemSlotView> leftHandler, Action<ItemSlotView> rightHandler)
    {
        leftClickHandler = leftHandler;
        rightClickHandler = rightHandler;
    }

    public void SetHoverHandlers(Action<ItemSlotView> enterHandler, Action<ItemSlotView> exitHandler)
    {
        hoverEnterHandler = enterHandler;
        hoverExitHandler = exitHandler;
    }

    public void SetDropHandler(Action<ItemSlotView, ItemSlotView> handler)
    {
        dropHandler = handler;
    }

    public void SetSelected(bool selected)
    {
        if (isSelected == selected)
            return;

        isSelected = selected;
        ApplyBorderVisibility();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null) return;
        if (currentItem == null) return;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            leftClickHandler?.Invoke(this);
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Right)
            rightClickHandler?.Invoke(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        ApplyBorderVisibility();
        hoverEnterHandler?.Invoke(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        ApplyBorderVisibility();
        hoverExitHandler?.Invoke(this);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData == null) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (currentItem == null || icon == null || icon.sprite == null) return;

        isDragging = true;
        CreateDragIcon(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        UpdateDragIconPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        isDragging = false;
        DestroyDragIcon();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData == null) return;
        var sourceSlot = eventData.pointerDrag != null
            ? eventData.pointerDrag.GetComponent<ItemSlotView>()
            : null;
        if (sourceSlot == null || sourceSlot == this) return;
        if (!sourceSlot.HasItem) return;

        dropHandler?.Invoke(this, sourceSlot);
    }

    private void CreateDragIcon(PointerEventData eventData)
    {
        dragCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (dragCanvas == null) return;

        var go = new GameObject("DragIcon", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        dragIcon = go.GetComponent<RectTransform>();
        dragIcon.SetParent(dragCanvas.transform, false);
        dragIcon.SetAsLastSibling();

        var image = go.GetComponent<Image>();
        image.sprite = icon.sprite;
        image.raycastTarget = false;

        var canvasGroup = go.GetComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        var iconRect = icon != null ? icon.rectTransform : null;
        var size = iconRect != null ? iconRect.rect.size : Vector2.zero;
        if (size.sqrMagnitude < 1f)
            size = new Vector2(32f, 32f);
        dragIcon.sizeDelta = size;

        UpdateDragIconPosition(eventData);
    }

    private void UpdateDragIconPosition(PointerEventData eventData)
    {
        if (dragIcon == null || dragCanvas == null || eventData == null)
            return;

        var canvasRect = dragCanvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        var camera = dragCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : dragCanvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, camera, out var pos))
            dragIcon.anchoredPosition = pos;
    }

    private void DestroyDragIcon()
    {
        if (dragIcon != null)
            Destroy(dragIcon.gameObject);

        dragIcon = null;
        dragCanvas = null;
    }

    private void ApplyBorderVisibility()
    {
        if (borderImage == null)
            return;

        borderImage.enabled = (isHovered || isSelected) && borderImage.sprite != null;
    }
}
