using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class ShopItemRowView : MonoBehaviour
{
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text priceLabel;
    [SerializeField] private Image icon;
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text buyLabel;

    public void Set(string displayName, int price, Sprite sprite, string buttonLabel, bool interactable, UnityAction onBuy)
    {
        if (nameLabel != null)
            nameLabel.text = displayName;

        if (priceLabel != null)
            priceLabel.text = price.ToString();

        if (icon != null)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.interactable = interactable;
            if (onBuy != null)
                buyButton.onClick.AddListener(onBuy);
        }

        var resolvedBuyLabel = buyLabel;
        if (resolvedBuyLabel == null && buyButton != null)
            resolvedBuyLabel = buyButton.GetComponentInChildren<TMP_Text>(true);

        if (resolvedBuyLabel != null)
            resolvedBuyLabel.text = buttonLabel ?? string.Empty;
    }

    public void Set(string displayName, int price, Sprite sprite, UnityAction onBuy)
    {
        Set(displayName, price, sprite, "Buy", true, onBuy);
    }
}
