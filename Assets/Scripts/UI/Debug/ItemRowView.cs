using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ItemRowView : MonoBehaviour
{
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text amountLabel;
    [SerializeField] private Image iconImage;

    [Header("Optional Action")]
    [SerializeField] private Button actionButton;
    [SerializeField] private TMP_Text actionLabel;

    private Action onAction;

    public void Set(string title, int amount, Sprite icon)
    {
        if (nameLabel != null) nameLabel.text = title ?? string.Empty;
        if (amountLabel != null) amountLabel.text = amount > 1 ? amount.ToString() : string.Empty;
        if (iconImage != null) iconImage.sprite = icon;

        ClearAction();
    }

    public void SetAction(string label, bool interactable, Action onClick)
    {
        onAction = onClick;

        if (actionButton != null)
        {
            actionButton.gameObject.SetActive(true);
            actionButton.interactable = interactable;
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(() => onAction?.Invoke());
        }

        if (actionLabel != null)
            actionLabel.text = label ?? string.Empty;
    }

    public void ClearAction()
    {
        onAction = null;

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.gameObject.SetActive(false);
        }

        if (actionLabel != null)
            actionLabel.text = string.Empty;
    }
}
