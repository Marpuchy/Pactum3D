using System.Collections;
using Injections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zenject;

public class MainMenuController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Toggle meleeToggle;
    [SerializeField] private Toggle rangedToggle;

    [Header("Config")]
    [SerializeField] private string sceneName;

    private IPlayerDataService playerDataService;

    [Inject]
    private void Construct([InjectOptional] IPlayerDataService injectedPlayerDataService)
    {
        playerDataService = injectedPlayerDataService;
    }

    private void Awake()
    {
        if (playerDataService == null)
            playerDataService = RuntimeServiceFallback.PlayerDataService;

        if (meleeToggle != null)
            meleeToggle.onValueChanged.AddListener(OnMeleeSelected);

        if (rangedToggle != null)
            rangedToggle.onValueChanged.AddListener(OnRangedSelected);

        SyncTogglesFromPlayerData();
    }

    private void OnDestroy()
    {
        if (meleeToggle != null)
            meleeToggle.onValueChanged.RemoveListener(OnMeleeSelected);

        if (rangedToggle != null)
            rangedToggle.onValueChanged.RemoveListener(OnRangedSelected);
    }

    private void OnMeleeSelected(bool value)
    {
        if (!value) return;

        if (rangedToggle != null)
            rangedToggle.SetIsOnWithoutNotify(false);

        if (playerDataService != null)
            playerDataService.SetSelectedAttack(AttackType.Melee);
    }

    private void OnRangedSelected(bool value)
    {
        if (!value) return;

        if (meleeToggle != null)
            meleeToggle.SetIsOnWithoutNotify(false);

        if (playerDataService != null)
            playerDataService.SetSelectedAttack(AttackType.Ranged);
    }

    public void StartGame()
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("Error: no se ha definido la escena a cargar.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"Error: la escena '{sceneName}' no esta en Build Settings.");
            return;
        }

        StartCoroutine(WaitForAudio());
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    private IEnumerator WaitForAudio()
    {
        yield return new WaitForSeconds(1f);
        SceneManager.LoadScene(sceneName);
    }

    private void SyncTogglesFromPlayerData()
    {
        if (meleeToggle == null || rangedToggle == null)
            return;

        AttackType selectedAttack = playerDataService != null
            ? playerDataService.SelectedAttack
            : AttackType.Melee;

        bool isMelee = selectedAttack == AttackType.Melee;
        meleeToggle.SetIsOnWithoutNotify(isMelee);
        rangedToggle.SetIsOnWithoutNotify(!isMelee);
    }
}
