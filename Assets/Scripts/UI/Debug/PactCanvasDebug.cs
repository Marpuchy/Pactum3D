using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PactCanvasDebug : MonoBehaviour, IInteractorBoundUI
{
    private const string TitleLabelName = "Title";
    private const string BodyLabelName = "Body";
    private const string CardIconName = "CardIcon";

    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text bodyLabel;
    [SerializeField] private Image iconImage;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private float optionButtonSpacing = 8f;
    [SerializeField] private bool allowManualClose = false;
    [SerializeField] private bool closeOnKey = true;
    [SerializeField] private KeyCode closeKey = KeyCode.E;
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private int sortingOrder = GameplayUIState.GameplayCanvasSortOrder;

    private Interactor interactor;
    private readonly List<Button> optionButtons = new();
    private OfferPactRequest currentRequest;
    private IReadOnlyList<PactDefinition> currentOptions;
    private Vector2 optionBasePosition;
    private bool optionBasePositionCached;
    private Sprite defaultIconSprite;
    private readonly Dictionary<Button, Sprite> defaultCardIconByButton = new();

    public bool IsVisible => root != null && root.activeSelf;

    private void Awake()
    {
        if (root == null)
            Debug.LogError($"{nameof(PactCanvasDebug)}: root is not assigned.", this);
        if (root != null)
            root.SetActive(false);

        if (iconImage != null)
            defaultIconSprite = iconImage.sprite;

        if (acceptButton != null)
        {
            acceptButton.onClick.RemoveAllListeners();
            CacheOptionBasePosition();
            optionButtons.Add(acceptButton);
            CacheDefaultCardIcon(acceptButton);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }

        if (rootCanvas == null)
        {
            rootCanvas = root != null ? root.GetComponentInChildren<Canvas>(true) : null;
            if (rootCanvas == null)
                rootCanvas = GetComponentInChildren<Canvas>(true);
        }

        GameplayUIState.ConfigureCanvas(rootCanvas, sortingOrder);
        UpdateCloseControls();
    }

    private void Update()
    {
        if (!IsVisible)
            return;

        if (!allowManualClose)
            return;

        if (closeOnKey && Input.GetKeyDown(closeKey))
            Close();
    }

    public void Bind(Interactor interactor)
    {
        this.interactor = interactor;
    }

    public void Show(OfferPactRequest request)
    {
        if (request.Interactor != null)
            interactor = request.Interactor;
        currentRequest = request;
        currentOptions = request.Pacts;

        if (root != null)
            root.SetActive(true);

        UpdateCloseControls();
        GameplayUIState.Register(this);

        Refresh();
    }

    private void Refresh()
    {
        if (currentOptions == null || currentOptions.Count == 0)
        {
            if (titleLabel != null)
                titleLabel.text = "No available pacts";
            if (bodyLabel != null)
                bodyLabel.text = string.Empty;
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }

            DisableOptionButtons();
            return;
        }

        if (titleLabel != null)
            titleLabel.text = "Choose a pact";
        if (bodyLabel != null)
            bodyLabel.text = string.Empty;
        RefreshNpcPortrait();


        EnsureOptionButtons(currentOptions.Count);
        for (int i = 0; i < currentOptions.Count; i++)
        {
            var pact = currentOptions[i];
            var button = optionButtons[i];
            if (button == null) continue;

            if (pact == null)
            {
                button.gameObject.SetActive(false);
                continue;
            }

            button.gameObject.SetActive(true);
            SetOptionButtonLabels(button, pact);
            SetOptionButtonCardIcon(button, pact);

            button.onClick.RemoveAllListeners();
            var selectedPact = pact;
            button.onClick.AddListener(() => SelectPact(selectedPact));

            PositionOptionButton(button, i);
        }

        for (int i = currentOptions.Count; i < optionButtons.Count; i++)
        {
            if (optionButtons[i] != null)
                optionButtons[i].gameObject.SetActive(false);
        }
    }

    private void SelectPact(PactDefinition pact)
    {
        if (pact == null) return;

        // Mark interaction first so selection side-effects (lock/consumed state) are applied before pact effects.
        currentRequest.NotifySelected(pact);

        var manager = PactManager.Instance;
        if (manager != null)
            manager.ApplyPact(pact);

        Close();
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

    public void SetManualCloseAllowed(bool allowed)
    {
        allowManualClose = allowed;
        UpdateCloseControls();
    }

    private void UpdateCloseControls()
    {
        if (closeButton != null)
            closeButton.gameObject.SetActive(allowManualClose);
    }

    private void OnDisable()
    {
        GameplayUIState.Unregister(this);
    }

    private void OnDestroy()
    {
        GameplayUIState.Unregister(this);
    }

    private void SetOptionButtonLabels(Button button, PactDefinition pact)
    {
        if (button == null || pact == null)
            return;

        var title = FindLabel(button, TitleLabelName);
        if (title != null)
            title.text = pact.Title;

        var body = FindLabel(button, BodyLabelName);
        if (body != null)
            body.text = BuildPactBody(pact);
    }

    private void SetOptionButtonCardIcon(Button button, PactDefinition pact)
    {
        if (button == null)
            return;

        Image cardIconImage = FindImage(button, CardIconName);
        if (cardIconImage == null)
            return;

        CacheDefaultCardIcon(button, cardIconImage);

        Sprite icon = ResolvePactCardIcon(pact);
        if (icon == null && defaultCardIconByButton.TryGetValue(button, out Sprite fallback))
            icon = fallback;

        cardIconImage.sprite = icon;
        cardIconImage.enabled = icon != null;
    }

    private static TMP_Text FindLabel(Button button, string labelName)
    {
        if (button == null || string.IsNullOrEmpty(labelName))
            return null;

        var labels = button.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            var label = labels[i];
            if (label != null && label.gameObject.name == labelName)
                return label;
        }

        return null;
    }

    private static Image FindImage(Button button, string imageName)
    {
        if (button == null || string.IsNullOrEmpty(imageName))
            return null;

        Image[] images = button.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image != null && image.gameObject.name == imageName)
                return image;
        }

        return null;
    }

    private static string BuildPactBody(PactDefinition pact)
    {
        if (pact == null)
            return string.Empty;

        return string.IsNullOrWhiteSpace(pact.Description)
            ? string.Empty
            : pact.Description.Trim();
    }

    private void RefreshNpcPortrait()
    {
        if (iconImage == null)
            return;

        Sprite spriteToShow = currentRequest.NpcCanvasSprite != null
            ? currentRequest.NpcCanvasSprite
            : defaultIconSprite;

        iconImage.sprite = spriteToShow;
        iconImage.enabled = spriteToShow != null;
    }

    private void EnsureOptionButtons(int count)
    {
        if (acceptButton == null)
        {
            Debug.LogError($"{nameof(PactCanvasDebug)}: acceptButton is not assigned.", this);
            return;
        }

        if (optionButtons.Count == 0)
            optionButtons.Add(acceptButton);

        for (int i = optionButtons.Count; i < count; i++)
        {
            var clone = Instantiate(acceptButton, acceptButton.transform.parent);
            clone.onClick.RemoveAllListeners();
            optionButtons.Add(clone);
            CacheDefaultCardIcon(clone);
        }
    }

    private void DisableOptionButtons()
    {
        for (int i = 0; i < optionButtons.Count; i++)
        {
            if (optionButtons[i] != null)
                optionButtons[i].gameObject.SetActive(false);
        }
    }

    private void CacheOptionBasePosition()
    {
        if (optionBasePositionCached || acceptButton == null)
            return;

        var rect = acceptButton.GetComponent<RectTransform>();
        if (rect == null)
            return;

        optionBasePosition = rect.anchoredPosition;
        optionBasePositionCached = true;
    }

    private void PositionOptionButton(Button button, int index)
    {
        if (button == null)
            return;

        CacheOptionBasePosition();
        if (!optionBasePositionCached)
            return;

        var rect = button.GetComponent<RectTransform>();
        if (rect == null)
            return;

        float step = rect.rect.height;
        if (step <= 0f)
            step = rect.sizeDelta.y;
        if (step <= 0f)
            step = 30f;

        step += optionButtonSpacing;
        rect.anchoredPosition = optionBasePosition + new Vector2(0f, -(step * index));
    }

    private void CacheDefaultCardIcon(Button button)
    {
        if (button == null)
            return;

        Image cardIconImage = FindImage(button, CardIconName);
        CacheDefaultCardIcon(button, cardIconImage);
    }

    private void CacheDefaultCardIcon(Button button, Image cardIconImage)
    {
        if (button == null || cardIconImage == null || defaultCardIconByButton.ContainsKey(button))
            return;

        defaultCardIconByButton[button] = cardIconImage.sprite;
    }

    private Sprite ResolvePactCardIcon(PactDefinition pact)
    {
        if (pact == null)
            return null;

        if (currentRequest.TryGetPactSourcePool(pact, out PactPoolSO sourcePool) && sourcePool != null)
        {
            Sprite poolTierIcon = sourcePool.GetLineScrollIcon(pact.LineTier);
            if (poolTierIcon != null)
                return poolTierIcon;
        }

        return pact.Icon;
    }
}
