using System.Collections;
using TMPro;
using UnityEngine;

public sealed class MiniInventoryGridCanvas : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private ItemSlotView weaponSlot;
    [SerializeField] private ItemSlotView armorSlot;
    [SerializeField] private ItemSlotView consumableSlot;
    [SerializeField] private ItemSlotView abilitySlot;
    [SerializeField] private TMP_Text feedbackLabel;
    [SerializeField] private string inventoryFullMessage = "Inventario lleno";
    [SerializeField] private float feedbackDuration = 1.5f;

    private Coroutine feedbackRoutine;

    private void OnEnable()
    {
        var miniInventory = FindFirstObjectByType<PlayerMiniInventory>();
        if (miniInventory != null)
            miniInventory.RegisterView(this);
    }

    public void Show()
    {
        if (root != null)
            root.SetActive(true);
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);
    }

    public void SetSlot(MiniInventorySlotType slotType, IItem item)
    {
        var slot = ResolveSlot(slotType);
        if (slot == null) return;

        if (item == null)
        {
            slot.Clear();
            return;
        }

        int amount = 1;
        if (item is IStackableItem stackable)
            amount = Mathf.Max(1, stackable.Count);

        slot.Set(item, amount);
    }

    public void ClearSlot(MiniInventorySlotType slotType)
    {
        var slot = ResolveSlot(slotType);
        if (slot != null)
            slot.Clear();
    }

    public void ClearAll()
    {
        weaponSlot?.Clear();
        armorSlot?.Clear();
        consumableSlot?.Clear();
        abilitySlot?.Clear();
    }

    public void ShowInventoryFull()
    {
        ShowFeedback(inventoryFullMessage);
    }

    public static bool TryShowInventoryFull()
    {
        var views = FindObjectsByType<MiniInventoryGridCanvas>(FindObjectsSortMode.None);
        MiniInventoryGridCanvas best = null;

        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] == null)
                continue;

            if (!views[i].IsFeedbackVisible())
                continue;

            best = views[i];
            break;
        }

        if (best == null && views.Length > 0)
            best = views[0];

        if (best == null)
            return false;

        best.ShowInventoryFull();
        return true;
    }

    public void SetSlotClickHandlers(
        System.Action<MiniInventorySlotType> leftHandler,
        System.Action<MiniInventorySlotType> rightHandler)
    {
        AssignClickHandlers(MiniInventorySlotType.Weapon, leftHandler, rightHandler);
        AssignClickHandlers(MiniInventorySlotType.Armor, leftHandler, rightHandler);
        AssignClickHandlers(MiniInventorySlotType.Consumable, leftHandler, rightHandler);
        AssignClickHandlers(MiniInventorySlotType.Ability, leftHandler, rightHandler);
    }

    public void ForEachSlot(System.Action<MiniInventorySlotType, ItemSlotView> action)
    {
        if (action == null)
            return;

        if (weaponSlot != null)
            action(MiniInventorySlotType.Weapon, weaponSlot);
        if (armorSlot != null)
            action(MiniInventorySlotType.Armor, armorSlot);
        if (consumableSlot != null)
            action(MiniInventorySlotType.Consumable, consumableSlot);
        if (abilitySlot != null)
            action(MiniInventorySlotType.Ability, abilitySlot);
    }

    private ItemSlotView ResolveSlot(MiniInventorySlotType slotType)
    {
        return slotType switch
        {
            MiniInventorySlotType.Weapon => weaponSlot,
            MiniInventorySlotType.Armor => armorSlot,
            MiniInventorySlotType.Consumable => consumableSlot,
            MiniInventorySlotType.Ability => abilitySlot,
            _ => null
        };
    }

    private void AssignClickHandlers(
        MiniInventorySlotType slotType,
        System.Action<MiniInventorySlotType> leftHandler,
        System.Action<MiniInventorySlotType> rightHandler)
    {
        var slot = ResolveSlot(slotType);
        if (slot == null) return;

        System.Action<ItemSlotView> left = leftHandler != null ? _ => leftHandler(slotType) : null;
        System.Action<ItemSlotView> right = rightHandler != null ? _ => rightHandler(slotType) : null;
        slot.SetClickHandlers(left, right);
    }

    private bool IsFeedbackVisible()
    {
        if (root != null)
            return root.activeInHierarchy;

        return gameObject.activeInHierarchy;
    }

    private void ShowFeedback(string message)
    {
        if (feedbackLabel == null)
        {
            Debug.LogWarning($"{nameof(MiniInventoryGridCanvas)}: feedbackLabel is not assigned.", this);
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
