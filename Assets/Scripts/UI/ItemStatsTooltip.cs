using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ItemStatsTooltip : MonoBehaviour
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text statsLabel;
    [SerializeField] private TMP_Text descriptionLabel;
    [SerializeField] private Vector2 screenOffset = new Vector2(16f, -16f);
    [SerializeField] private Vector2 clampPadding = new Vector2(8f, 8f);
    [SerializeField] private bool clampToCanvas = true;
    [SerializeField] private RectTransform canvasRect;

    private Canvas canvas;
    private bool isVisible;

    private void Awake()
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        if (titleLabel == null)
            titleLabel = transform.Find("Title")?.GetComponent<TMP_Text>();

        if (statsLabel == null)
            statsLabel = transform.Find("Stats")?.GetComponent<TMP_Text>();

        if (descriptionLabel == null)
            descriptionLabel = transform.Find("Description")?.GetComponent<TMP_Text>();

        ResolveCanvas();
        Hide();
    }

    private void LateUpdate()
    {
        if (isVisible)
            FollowMouse();
    }

    public void BindCanvas(Canvas targetCanvas)
    {
        canvas = targetCanvas;
        canvasRect = targetCanvas != null ? targetCanvas.transform as RectTransform : null;
    }

    public void ShowForItem(IItem item)
    {
        if (item == null)
        {
            Hide();
            return;
        }

        EnsureVisible();

        var data = TryGetItemData(item);
        if (titleLabel != null)
            titleLabel.text = data != null ? ResolveItemName(data) : (item.Name ?? string.Empty);

        string statsText = data != null ? BuildStatsText(data) : string.Empty;
        SetLabel(statsLabel, statsText);

        string description = data != null ? data.Description : item.Description;
        SetLabel(descriptionLabel, description);

        if (rectTransform != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);

        FollowMouse();
    }

    public void Hide()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        isVisible = false;
    }

    private void EnsureVisible()
    {
        isVisible = true;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        transform.SetAsLastSibling();
    }

    private void FollowMouse()
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;

        if (canvasRect == null || canvas == null)
            ResolveCanvas();

        if (canvasRect == null || rectTransform == null)
            return;

        var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                Input.mousePosition,
                camera,
                out var localPoint))
            return;

        var anchored = localPoint + screenOffset;
        if (clampToCanvas)
            anchored = ClampToCanvas(anchored);

        rectTransform.anchoredPosition = anchored;
    }

    private Vector2 ClampToCanvas(Vector2 position)
    {
        if (canvasRect == null || rectTransform == null)
            return position;

        var canvasSize = canvasRect.rect.size;
        var tooltipSize = rectTransform.rect.size;

        float halfWidth = canvasSize.x * 0.5f;
        float halfHeight = canvasSize.y * 0.5f;

        float minX = -halfWidth + clampPadding.x;
        float maxX = halfWidth - clampPadding.x - tooltipSize.x;
        float maxY = halfHeight - clampPadding.y;
        float minY = -halfHeight + clampPadding.y + tooltipSize.y;

        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.y = Mathf.Clamp(position.y, minY, maxY);
        return position;
    }

    private void ResolveCanvas()
    {
        if (canvasRect != null)
        {
            canvas = canvasRect.GetComponent<Canvas>();
            if (canvas != null)
                return;
        }

        canvas = GetComponentInParent<Canvas>();
        canvasRect = canvas != null ? canvas.transform as RectTransform : null;
    }

    private static void SetLabel(TMP_Text label, string text)
    {
        if (label == null)
            return;

        bool hasText = !string.IsNullOrWhiteSpace(text);
        label.gameObject.SetActive(hasText);
        label.text = hasText ? text : string.Empty;
    }

    private static ItemDataSO TryGetItemData(IItem item)
    {
        return item is IItemDataProvider provider ? provider.Data : null;
    }

    private static string ResolveItemName(ItemDataSO data)
    {
        if (data == null)
            return string.Empty;

        return string.IsNullOrWhiteSpace(data.DisplayName) ? data.name : data.DisplayName;
    }

    private static string BuildStatsText(ItemDataSO data)
    {
        if (data == null)
            return string.Empty;

        var sb = new StringBuilder(128);

        switch (data.ItemType)
        {
            case ItemType.Weapon:
                if (data.WeaponStats != null)
                    AppendEquipmentStats(sb, data.WeaponStats.Modifiers);
                break;
            case ItemType.Armor:
                if (data.ArmorStats != null)
                    AppendEquipmentStats(sb, data.ArmorStats.Modifiers);
                break;
            case ItemType.Consumable:
                if (data.ConsumableStats != null)
                {
                    var stats = data.ConsumableStats;
                    AppendStat(sb, "Heal", stats.healAmount, "0");
                    AppendTimedStat(sb, "Regen", stats.regenAmountPerTick, stats.regenTickInterval, stats.regenDuration);
                    AppendTimedStat(sb, "Speed", stats.extraSpeed, stats.speedTickInterval, stats.speedDuration);
                    AppendTimedStat(sb, "Attack", stats.extraAttack, stats.attackTickInterval, stats.attackDuration);
                }
                break;
        }

        return sb.ToString().TrimEnd();
    }

    private static void AppendEquipmentStats(StringBuilder sb, IReadOnlyList<ItemStatModifierEntry> modifiers)
    {
        if (modifiers == null || modifiers.Count == 0)
            return;

        for (int i = 0; i < modifiers.Count; i++)
        {
            ItemStatModifierEntry modifier = modifiers[i];
            if (modifier == null || StatModifierFactory.IsNoOp(modifier.Operation, modifier.Value))
                continue;

            string label = HumanizeStatType(modifier.StatType);
            string value = modifier.Operation == StatModifierOperation.Multiply
                ? FormatPercentModifier(modifier.Value)
                : modifier.Value >= 0f
                    ? $"+{modifier.Value:0.##}"
                    : modifier.Value.ToString("0.##");

            sb.Append(label).Append(": ").Append(value).Append('\n');
        }
    }

    private static void AppendStat(StringBuilder sb, string label, float value, string format)
    {
        if (Mathf.Abs(value) < 0.0001f)
            return;

        sb.Append(label).Append(": ").Append(value.ToString(format)).Append('\n');
    }

    private static void AppendTimedStat(StringBuilder sb, string label, float amount, float interval, float duration)
    {
        if (Mathf.Abs(amount) < 0.0001f)
            return;

        sb.Append(label).Append(": ").Append(amount.ToString("0.##"));

        bool hasInterval = interval > 0f;
        bool hasDuration = duration > 0f;

        if (hasInterval || hasDuration)
        {
            sb.Append(" (");
            bool needsSeparator = false;

            if (hasInterval)
            {
                sb.Append(interval.ToString("0.##")).Append("s");
                needsSeparator = true;
            }

            if (hasDuration)
            {
                if (needsSeparator)
                    sb.Append(" / ");

                sb.Append(duration.ToString("0.##")).Append("s");
            }

            sb.Append(')');
        }

        sb.Append('\n');
    }

    private static string HumanizeStatType(StatType statType)
    {
        switch (statType)
        {
            case StatType.ShieldArmor:
                return "Armor";
            case StatType.MaxSpeed:
                return "Speed";
            case StatType.AttackLockTime:
                return "Attack Lock Time";
            default:
                return PactDescriptionFormatter.HumanizeEnum(statType);
        }
    }

    private static string FormatPercentModifier(float factor)
    {
        float percent = (factor - 1f) * 100f;
        return percent >= 0f
            ? $"+{percent:0.#}%"
            : $"{percent:0.#}%";
    }

    public static ItemStatsTooltip Ensure(ItemStatsTooltip existing, Canvas targetCanvas)
    {
        if (existing != null)
        {
            existing.BindCanvas(targetCanvas);
            return existing;
        }

        if (targetCanvas == null)
            return null;

        return CreateDefault(targetCanvas.transform, targetCanvas);
    }

    public static ItemStatsTooltip CreateDefault(Transform parent, Canvas targetCanvas)
    {
        if (parent == null)
            return null;

        var root = new GameObject("ItemTooltip", typeof(RectTransform));
        var rect = root.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = Vector2.zero;

        var image = root.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.85f);
        image.raycastTarget = false;

        var group = root.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        var layout = root.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 6, 6);
        layout.spacing = 2f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var fitter = root.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var title = CreateTextChild(root.transform, "Title", 18f, FontStyles.Bold);
        var stats = CreateTextChild(root.transform, "Stats", 14f, FontStyles.Normal);
        var desc = CreateTextChild(root.transform, "Description", 12f, FontStyles.Italic);

        var tooltip = root.AddComponent<ItemStatsTooltip>();
        tooltip.rectTransform = rect;
        tooltip.canvasGroup = group;
        tooltip.titleLabel = title;
        tooltip.statsLabel = stats;
        tooltip.descriptionLabel = desc;
        tooltip.BindCanvas(targetCanvas);
        tooltip.Hide();

        return tooltip;
    }

    private static TMP_Text CreateTextChild(Transform parent, string name, float size, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = Vector2.zero;

        var text = go.AddComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.fontSize = size;
        text.fontStyle = style;
        text.enableWordWrapping = true;
        text.alignment = TextAlignmentOptions.TopLeft;

        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;

        var layout = go.AddComponent<LayoutElement>();
        layout.preferredWidth = 260f;

        return text;
    }
}
