using TMPro;
using UnityEngine;

public sealed class WorldPromptPresenter : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private InteractionFocusEventChannelSO focusChannel;

    [Header("View")]
    [SerializeField] private GameObject promptPrefab;

    [Header("Positioning")]
    [SerializeField] private Vector3 worldOffset = new(0f, 0.35f, 0f);
    [SerializeField] private int canvasSortingOrder = 5000;

    private Transform _followTarget;
    private GameObject _instance;
    private TMP_Text _label;
    private Canvas _canvas;

    private void Awake()
    {
        if (promptPrefab == null)
            Debug.LogError($"{nameof(WorldPromptPresenter)}: promptPrefab is not assigned.");

        EnsureInstance();
        SetVisible(false);
    }

    private void OnEnable()
    {
        if (focusChannel != null) focusChannel.OnFocusChanged += OnFocusChanged;
        OnFocusChanged(null);
    }

    private void OnDisable()
    {
        if (focusChannel != null) focusChannel.OnFocusChanged -= OnFocusChanged;
    }

    private void LateUpdate()
    {
        if (_followTarget == null || _instance == null) return;

        _instance.transform.position = _followTarget.position + worldOffset;
    }

    private void OnFocusChanged(IInteractable interactable)
    {
        if (interactable == null)
        {
            _followTarget = null;
            SetVisible(false);
            return;
        }

        _followTarget = interactable.InteractionPoint != null
            ? interactable.InteractionPoint
            : null;

        EnsureInstance();

        if (_label != null) _label.text = interactable.Prompt;

        SetVisible(_followTarget != null);
        if (_followTarget != null)
            _instance.transform.position = _followTarget.position + worldOffset;

    }

    private void EnsureInstance()
    {
        if (_instance != null) return;
        if (promptPrefab == null) return;

        _instance = Instantiate(promptPrefab);
        _label = _instance.GetComponentInChildren<TMP_Text>(true);
        _canvas = _instance.GetComponentInChildren<Canvas>(true);

        ConfigurePromptCanvas();

        if (_label == null)
            Debug.LogError($"{nameof(WorldPromptPresenter)}: Prefab must contain a TMP_Text.");
    }

    private void SetVisible(bool visible)
    {
        if (_instance != null && _instance.activeSelf != visible)
            _instance.SetActive(visible);
    }

    private void ConfigurePromptCanvas()
    {
        if (_canvas == null)
            return;

        _canvas.overrideSorting = true;
        _canvas.sortingOrder = canvasSortingOrder;
    }
}
