using System.Collections.Generic;
using UnityEngine;

public sealed class ItemGridView : MonoBehaviour
{
    [SerializeField] private List<ItemSlotView> slots = new();

    public void Initialize(List<ItemSlotView> providedSlots)
    {
        slots = providedSlots ?? new List<ItemSlotView>();
        if (slots.Count == 0 || AreAllSlotsNull())
            AutoDetectSlots();
    }

    private void Awake()
    {
        if (slots == null)
            slots = new List<ItemSlotView>();

        if (slots.Count == 0 || AreAllSlotsNull())
            AutoDetectSlots();

        if (slots.Count == 0)
        {
            Debug.LogWarning($"{nameof(ItemGridView)}: No slots assigned or found in children.", this);
        }
    }

    public void SetSlotClickHandler(System.Action<ItemSlotView> handler)
    {
        if (slots == null || slots.Count == 0 || AreAllSlotsNull())
            AutoDetectSlots();

        for (int i = 0; i < slots.Count; i++)
            slots[i]?.SetClickHandler(handler);
    }

    public void SetSlotClickHandlers(System.Action<ItemSlotView> leftHandler, System.Action<ItemSlotView> rightHandler)
    {
        if (slots == null || slots.Count == 0 || AreAllSlotsNull())
            AutoDetectSlots();

        for (int i = 0; i < slots.Count; i++)
            slots[i]?.SetClickHandlers(leftHandler, rightHandler);
    }

    public int GetSlotCount()
    {
        if (slots == null || slots.Count == 0 || AreAllSlotsNull())
            AutoDetectSlots();

        return slots != null ? slots.Count : 0;
    }

    public void ForEachSlot(System.Action<ItemSlotView> action)
    {
        if (action == null)
            return;

        if (slots == null || slots.Count == 0 || AreAllSlotsNull())
            AutoDetectSlots();

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot != null)
                action(slot);
        }
    }

    public void ForEachSlot(System.Action<int, ItemSlotView> action)
    {
        if (action == null)
            return;

        if (slots == null || slots.Count == 0 || AreAllSlotsNull())
            AutoDetectSlots();

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot != null)
                action(i, slot);
        }
    }

    public void RenderFromSlotList(IReadOnlyList<IItem> items, bool excludeCurrency = true, int maxSlots = -1)
    {
        if (slots == null || slots.Count == 0)
        {
            Debug.LogWarning($"{nameof(ItemGridView)}: Cannot render without slots.", this);
            return;
        }

        int slotLimit = maxSlots > 0 ? Mathf.Min(maxSlots, slots.Count) : slots.Count;

        for (int i = 0; i < slotLimit; i++)
        {
            var slot = slots[i];
            if (slot == null)
                continue;

            IItem item = (items != null && i < items.Count) ? items[i] : null;
            if (item == null || (excludeCurrency && IsCurrency(item)))
            {
                slot.Clear();
                continue;
            }

            int amount = 1;
            if (item is IStackableItem stackable)
                amount = Mathf.Max(1, stackable.Count);

            slot.Set(item, amount);
        }

        for (int i = slotLimit; i < slots.Count; i++)
            slots[i]?.Clear();
    }

    public void RenderFromItems(IEnumerable<IItem> items, bool excludeCurrency = true, int maxSlots = -1)
    {
        if (slots == null || slots.Count == 0)
        {
            Debug.LogWarning($"{nameof(ItemGridView)}: Cannot render without slots.", this);
            return;
        }

        int slotLimit = maxSlots > 0 ? Mathf.Min(maxSlots, slots.Count) : slots.Count;
        int slotIndex = 0;

        if (items != null)
        {
            foreach (var item in items)
            {
                if (item == null) continue;
                if (excludeCurrency && IsCurrency(item)) continue;
                if (slotIndex >= slotLimit) break;

                int amount = 1;
                if (item is IStackableItem stackable)
                    amount = Mathf.Max(1, stackable.Count);

                var slot = slots[slotIndex];
                if (slot != null)
                    slot.Set(item, amount);

                slotIndex++;
            }
        }

        for (int i = slotIndex; i < slots.Count; i++)
            slots[i]?.Clear();
    }

    private static bool IsCurrency(IItem item)
    {
        if (item is IStackableItem stackable && stackable.Data != null)
            return stackable.Data.IsCurrency;

        return false;
    }

    private bool AreAllSlotsNull()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
                return false;
        }

        return true;
    }

    private void AutoDetectSlots()
    {
        var found = GetComponentsInChildren<ItemSlotView>(true);
        if (found == null || found.Length == 0)
        {
            slots = new List<ItemSlotView>();
            return;
        }

        slots = new List<ItemSlotView>(found);

        if (HasNumberedSlotNames(slots))
        {
            slots.Sort(CompareSlotNames);
        }
        else
        {
            Debug.LogWarning(
                $"{nameof(ItemGridView)}: Slots auto-detected but names do not follow Slot_01 convention. Check ordering.",
                this);
        }
    }

    private static bool HasNumberedSlotNames(List<ItemSlotView> detectedSlots)
    {
        if (detectedSlots == null || detectedSlots.Count == 0) return false;

        for (int i = 0; i < detectedSlots.Count; i++)
        {
            if (detectedSlots[i] == null)
                return false;

            if (!TryGetTrailingNumber(detectedSlots[i].name, out _))
                return false;
        }

        return true;
    }

    private static int CompareSlotNames(ItemSlotView a, ItemSlotView b)
    {
        if (a == b) return 0;
        if (a == null) return 1;
        if (b == null) return -1;

        bool aHas = TryGetTrailingNumber(a.name, out int aIndex);
        bool bHas = TryGetTrailingNumber(b.name, out int bIndex);

        if (aHas && bHas)
            return aIndex.CompareTo(bIndex);

        return string.CompareOrdinal(a.name, b.name);
    }

    private static bool TryGetTrailingNumber(string value, out int number)
    {
        number = 0;
        if (string.IsNullOrEmpty(value)) return false;

        int i = value.Length - 1;
        while (i >= 0 && char.IsDigit(value[i]))
            i--;

        if (i == value.Length - 1)
            return false;

        var digits = value.Substring(i + 1);
        return int.TryParse(digits, out number);
    }
}
