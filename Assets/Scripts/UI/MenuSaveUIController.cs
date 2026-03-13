using System;
using Injections;
using SaveSystem;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zenject;

public sealed class MenuSaveUIController : MonoBehaviour
{
    private enum MenuContext
    {
        MainMenu,
        PauseMenu
    }

    [Header("Context")]
    [SerializeField] private MenuContext context = MenuContext.MainMenu;

    [Header("Save")]
    [SerializeField] private string slotName = "slot_0";
    [SerializeField] private string defaultGameplayScene = "Salas";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Attack UI (Optional View)")]
    [SerializeField] private Toggle meleeToggle;
    [SerializeField] private Toggle rangedToggle;

    [Header("Pause")]
    [SerializeField] private PauseController pauseController;

    [Header("Optional UI Feedback")]
    [SerializeField] private Button continueButton;
    [SerializeField] private TMP_Text feedbackLabel;
    [SerializeField] private string noSaveMessage = "No hay partida guardada.";
    [SerializeField] private string manualSaveDisabledMessage = "Guardado manual deshabilitado.";
    [SerializeField] private string saveDeletedMessage = "Partida eliminada.";

    private IGameSaveService gameSaveService;
    private IPlayerDataService playerDataService;

    [Inject]
    private void Construct(
        [InjectOptional] IGameSaveService injectedSaveService,
        [InjectOptional] IPlayerDataService injectedPlayerDataService)
    {
        gameSaveService = injectedSaveService;
        playerDataService = injectedPlayerDataService;
    }

    private void Awake()
    {
        if (playerDataService == null)
            playerDataService = RuntimeServiceFallback.PlayerDataService;

        if (gameSaveService == null)
            gameSaveService = RuntimeServiceFallback.GameSaveService;

        if (pauseController == null && context == MenuContext.PauseMenu)
            pauseController = GetComponent<PauseController>();

        RegisterToggleListeners();
        SyncTogglesFromPlayerData();
        RefreshContinueButton();
    }

    private void OnEnable()
    {
        SyncTogglesFromPlayerData();
        RefreshContinueButton();
    }

    public void StartNewGame()
    {
        PendingGameSaveState.Clear();
        LoadedNpcRoomOfferState.Clear();

        string targetScene = defaultGameplayScene;
        if (string.IsNullOrWhiteSpace(targetScene))
        {
            SetFeedback("No se configuro la escena de juego.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetScene))
        {
            SetFeedback($"Escena no disponible: {targetScene}");
            Debug.LogError($"MenuSaveUIController: scene '{targetScene}' is not in Build Settings.", this);
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(targetScene);
    }

    public void ContinueGame()
    {
        PendingGameSaveState.Clear();
        LoadedNpcRoomOfferState.Clear();

        if (gameSaveService == null)
        {
            SetFeedback("Save service no disponible.");
            return;
        }

        if (!gameSaveService.TryLoadState(slotName, out GameSaveData data))
        {
            SetFeedback(noSaveMessage);
            Debug.LogWarning($"MenuSaveUIController: could not load save slot '{slotName}'.", this);
            RefreshContinueButton();
            return;
        }

        SyncTogglesFromPlayerData();

        string targetScene = ResolveTargetSceneFromSave(data.SceneName);

        if (string.IsNullOrWhiteSpace(targetScene))
        {
            SetFeedback("Save sin escena valida.");
            Debug.LogError(
                $"MenuSaveUIController: save slot '{slotName}' does not resolve to a playable scene. " +
                $"Saved scene='{data.SceneName}', fallback='{defaultGameplayScene}', main menu='{mainMenuSceneName}'.",
                this);
            return;
        }

        Time.timeScale = 1f;
        Debug.Log($"MenuSaveUIController: loading scene '{targetScene}' from save slot '{slotName}'.", this);
        SceneManager.LoadScene(targetScene);
    }

    public void SaveCurrentGame()
    {
        SetFeedback(manualSaveDisabledMessage);
    }

    public void SaveAndQuitToMainMenu()
    {
        if (pauseController != null)
            pauseController.ResumeGame();
        else
            Time.timeScale = 1f;

        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            SetFeedback("No se configuro escena de menu.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            SetFeedback($"Escena no disponible: {mainMenuSceneName}");
            Debug.LogError($"MenuSaveUIController: scene '{mainMenuSceneName}' is not in Build Settings.", this);
            return;
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void SelectMelee()
    {
        SetSelectedAttack(AttackType.Melee);
    }

    public void SelectRanged()
    {
        SetSelectedAttack(AttackType.Ranged);
    }

    public void ResumeGame()
    {
        if (pauseController != null)
            pauseController.ResumeGame();
    }

    public void DeleteSave()
    {
        if (gameSaveService == null)
        {
            SetFeedback("Save service no disponible.");
            return;
        }

        if (gameSaveService.Delete(slotName))
            SetFeedback(saveDeletedMessage);
        else
            SetFeedback(noSaveMessage);

        RefreshContinueButton();
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    private void RegisterToggleListeners()
    {
        if (meleeToggle != null)
            meleeToggle.onValueChanged.AddListener(HandleMeleeToggleChanged);

        if (rangedToggle != null)
            rangedToggle.onValueChanged.AddListener(HandleRangedToggleChanged);
    }

    private void OnDestroy()
    {
        if (meleeToggle != null)
            meleeToggle.onValueChanged.RemoveListener(HandleMeleeToggleChanged);

        if (rangedToggle != null)
            rangedToggle.onValueChanged.RemoveListener(HandleRangedToggleChanged);
    }

    private void HandleMeleeToggleChanged(bool isOn)
    {
        if (!isOn)
            return;

        SetSelectedAttack(AttackType.Melee);
    }

    private void HandleRangedToggleChanged(bool isOn)
    {
        if (!isOn)
            return;

        SetSelectedAttack(AttackType.Ranged);
    }

    private void SetSelectedAttack(AttackType selectedAttack)
    {
        if (playerDataService != null)
            playerDataService.SetSelectedAttack(selectedAttack);

        SyncTogglesFromPlayerData();
    }

    private void SyncTogglesFromPlayerData()
    {
        if (context != MenuContext.MainMenu)
            return;

        if (meleeToggle == null || rangedToggle == null)
            return;

        AttackType selectedAttack = playerDataService != null
            ? playerDataService.SelectedAttack
            : AttackType.Melee;

        bool isMelee = selectedAttack == AttackType.Melee;
        meleeToggle.SetIsOnWithoutNotify(isMelee);
        rangedToggle.SetIsOnWithoutNotify(!isMelee);
    }

    private void RefreshContinueButton()
    {
        if (continueButton == null)
            return;

        bool hasSave = gameSaveService != null && gameSaveService.Exists(slotName);
        continueButton.interactable = hasSave;

        if (context == MenuContext.MainMenu)
            continueButton.gameObject.SetActive(hasSave);
    }

    private void SetFeedback(string message)
    {
        if (feedbackLabel != null)
            feedbackLabel.text = message;
    }

    private string ResolveTargetSceneFromSave(string savedSceneName)
    {
        if (IsPlayableScene(savedSceneName))
            return savedSceneName;

        if (!string.IsNullOrWhiteSpace(savedSceneName))
        {
            Debug.LogWarning(
                $"MenuSaveUIController: saved scene '{savedSceneName}' is not playable for continue. Falling back to '{defaultGameplayScene}'.",
                this);
        }

        if (IsPlayableScene(defaultGameplayScene))
            return defaultGameplayScene;

        return string.Empty;
    }

    private bool IsPlayableScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        if (string.Equals(sceneName, mainMenuSceneName, StringComparison.Ordinal))
            return false;

        return Application.CanStreamedLevelBeLoaded(sceneName);
    }
}
