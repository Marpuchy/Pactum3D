using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using Injections;
using SaveSystem;

public class GameOverController : MonoBehaviour
{
    [Header("ARCHITECTURE")]
    [SerializeField] private OnDeathEventSO onDeathEvent;

    [Header("UI GROUPS")]
    public GameObject gameOverPanel;
    public CanvasGroup mainCanvasGroup;
    public CanvasGroup buttonsGroup;
    [SerializeField] private Canvas gameOverCanvas;
    [SerializeField] private int gameOverSortingOrder = GameplayUIState.GameOverCanvasSortOrder;


    [Header("TRANSITION")]
    public CanvasGroup deathFaderCG;
    public float fadeToBlackDuration = 1.5f;

    [Header("SETTINGS")]
    public float delayBeforeButtons = 2.0f;
    [SerializeField] private bool deleteSaveOnDeath = true;
    [SerializeField] private string saveSlotOnDeath = "slot_0";

    private bool deathSequenceStarted;

    private void Awake()
    {
        if (gameOverCanvas == null && gameOverPanel != null)
            gameOverCanvas = gameOverPanel.GetComponent<Canvas>();

        GameplayUIState.ConfigureCanvas(gameOverCanvas, gameOverSortingOrder);
    }

    private void OnEnable()
    {
        if (onDeathEvent != null)
            onDeathEvent.RegisterListener(OnDeathEventReceived);
    }

    private void OnDisable()
    {
        if (onDeathEvent != null)
            onDeathEvent.UnregisterListener(OnDeathEventReceived);
    }

    private void Start()
    {
        if (mainCanvasGroup != null)
        {
            mainCanvasGroup.alpha = 0f;
            mainCanvasGroup.blocksRaycasts = false;
            mainCanvasGroup.interactable = false;
        }

        if (buttonsGroup != null)
        {
            buttonsGroup.alpha = 0f;
            buttonsGroup.blocksRaycasts = false;
            buttonsGroup.interactable = false;
        }

        if (deathFaderCG != null)
        {
            deathFaderCG.alpha = 0f;
            deathFaderCG.blocksRaycasts = false;
        }
    }

    private void OnDeathEventReceived()
    {
        if (deathSequenceStarted)
            return;

        deathSequenceStarted = true;
        DeleteSaveOnDeath();
        StartCoroutine(GameOverSequenceRoutine());
    }

    private void DeleteSaveOnDeath()
    {
        if (!deleteSaveOnDeath || string.IsNullOrWhiteSpace(saveSlotOnDeath))
            return;

        PendingGameSaveState.Clear();
        LoadedNpcRoomOfferState.Clear();

        IGameSaveService saveService = RuntimeServiceFallback.GameSaveService;
        if (saveService == null)
            return;

        saveService.Delete(saveSlotOnDeath);
    }

    IEnumerator GameOverSequenceRoutine()
    {
        Debug.Log("💀 Death Sequence Started...");

        if (deathFaderCG != null)
            deathFaderCG.blocksRaycasts = true;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / fadeToBlackDuration;
            if (deathFaderCG != null)
                deathFaderCG.alpha = t;

            yield return null;
        }

        if (deathFaderCG != null)
            deathFaderCG.alpha = 1f;

        yield return new WaitForSecondsRealtime(0.5f);

        if (mainCanvasGroup != null)
        {
            mainCanvasGroup.alpha = 1f;
            mainCanvasGroup.blocksRaycasts = true;
            mainCanvasGroup.interactable = true;
        }

        if (deathFaderCG != null)
        {
            deathFaderCG.alpha = 0f;
            deathFaderCG.blocksRaycasts = false;
        }

        StartCoroutine(ShowButtonsRoutine());
    }

    IEnumerator ShowButtonsRoutine()
    {
        yield return new WaitForSeconds(delayBeforeButtons);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 1.5f;
            if (buttonsGroup != null)
                buttonsGroup.alpha = t;

            yield return null;
        }

        if (buttonsGroup != null)
        {
            buttonsGroup.alpha = 1f;
            buttonsGroup.interactable = true;
            buttonsGroup.blocksRaycasts = true;
        }
    }

    public void RetryGame()
    {
        StartCoroutine(LoadSceneGameAfterDelay(1.5f));
    }

    public void BackToMenu()
    {

        StartCoroutine(LoadSceneAfterDelay(1.5f));
    }
    
    private IEnumerator LoadSceneAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        GameplayUIState.Reset();
        SceneManager.LoadScene(0);
    }
    
    private IEnumerator LoadSceneGameAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        GameplayUIState.Reset();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
