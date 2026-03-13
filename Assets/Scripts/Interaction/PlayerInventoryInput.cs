using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Interactor))]
public sealed class PlayerInventoryInput : MonoBehaviour
{
    [SerializeField] private InventoryUIFactory factory;
    [SerializeField] private KeyCode toggleKey = KeyCode.I;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private InventoryOpenEvent inventoryOpenEvent;

    private Interactor interactor;

    private void Awake()
    {
        interactor = GetComponent<Interactor>();
        ResolveFactory();
        
        UpdateStats();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Update()
    {
        UpdateStats();
        
        if (Input.GetKeyDown(toggleKey))
        {
            ResolveFactory();
            if (factory == null) return;

            if (GameplayUIState.IsGameplayInputBlocked && !factory.IsOpen)
                return;

            factory.Toggle(interactor, toggleKey);
        }
    }

    private void UpdateStats()
    {
        if (playerController != null && inventoryOpenEvent != null)
        {
            inventoryOpenEvent.Raise(
                playerController.CurrentHealth,
                playerController.CurrentDamage,
                playerController.CurrentArmor,
                playerController.CurrentAttackSpeed,
                playerController.CurrentSpeed
            );
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveFactory();
    }

    private void ResolveFactory()
    {
        if (factory != null)
            return;

        factory = FindFirstObjectByType<InventoryUIFactory>();
    }

    public KeyCode ToggleKey => toggleKey;
}
