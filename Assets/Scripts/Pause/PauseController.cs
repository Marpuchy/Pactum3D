using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; 

public class PauseController : MonoBehaviour
{
    [Header("CONFIGURACIÓN UI")]
    public GameObject pausePanel;
    public string menuSceneName = "MainMenu";
    public GameObject playerObject; 
    [SerializeField] private Canvas pauseCanvas;
    [SerializeField] private int pauseSortingOrder = GameplayUIState.PauseCanvasSortOrder;

    [Header("Events")]
    [SerializeField] private PauseEventSO onPauseEvent;

    [SerializeField] private PauseEventSO onResumeEvent;
    
    private bool isPaused = false;

    private void Awake()
    {
        if (pauseCanvas == null && pausePanel != null)
            pauseCanvas = pausePanel.GetComponent<Canvas>();

        GameplayUIState.ConfigureCanvas(pauseCanvas, pauseSortingOrder);
    }

    void Start()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        isPaused = false;
        
        Time.timeScale = 1f; 
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isPaused) ResumeGame();
            else PauseGame();
        }
    }

    private void OnDisable()
    {
        GameplayUIState.Unregister(this);
    }

    public void PauseGame()
    {
        if (pausePanel != null) pausePanel.SetActive(true);
        isPaused = true;

        // 👇 1. ESTA ES LA CLAVE: CONGELAR EL TIEMPO
        Time.timeScale = 0f; 

        // 2. Opcional pero recomendado: Apagar controles del jugador
        if (playerObject != null)
        {
            var input = playerObject.GetComponent<PlayerInput>();
            if (input != null) input.DeactivateInput();
        }
        
        GameplayUIState.Register(this);
        onPauseEvent?.Raise();
    }

    public void ResumeGame()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        isPaused = false;

        // 👇 1. IMPORTANTE: DESCONGELAR EL TIEMPO
        Time.timeScale = 1f; 

        // 2. Reactivar controles
        if (playerObject != null)
        {
            var input = playerObject.GetComponent<PlayerInput>();
            var attackController = playerObject.GetComponent<PlayerAttackController>();

            if (input != null) input.ActivateInput();
            
            if (attackController != null) attackController.ApplySelectedAttack();
        }

        
        GameplayUIState.Unregister(this);
        onResumeEvent?.Raise();
    }

    public void QuitToMainMenu()
    {
        StartCoroutine(ReturnAfterRealtime(1.5f));
    }

    private IEnumerator ReturnAfterRealtime(float delay)
    {
        yield return new WaitForSecondsRealtime(delay); // ignora Time.timeScale
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuSceneName);
    }
}
