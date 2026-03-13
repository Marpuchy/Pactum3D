using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class InventoryUIFactory : MonoBehaviour
{
    [SerializeField] private Transform uiRoot;
    [SerializeField] private InventoryGridCanvas inventoryPrefab;

    private InventoryGridCanvas instance;

    public bool IsOpen => instance != null && instance.IsVisible;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    public void Toggle(Interactor interactor)
    {
        Toggle(interactor, KeyCode.None);
    }

    public void Toggle(Interactor interactor, KeyCode closeKey)
    {
        if (IsOpen) Close();
        else Open(interactor, closeKey);
    }

    public void Open(Interactor interactor)
    {
        Open(interactor, KeyCode.None);
    }

    public void Open(Interactor interactor, KeyCode closeKey)
    {
        if (interactor == null || inventoryPrefab == null) return;
        ResolveUiRoot();

        if (instance == null)
        {
            instance = uiRoot != null
                ? Instantiate(inventoryPrefab, uiRoot)
                : Instantiate(inventoryPrefab);
        }
        instance.Bind(interactor);
        if (closeKey != KeyCode.None)
            instance.SetCloseKey(closeKey);
        instance.SetCloseOnKey(true);

        instance.Show();
    }

    public void Close()
    {
        if (instance == null) return;
        instance.Hide();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        uiRoot = null;
        ResolveUiRoot();
    }

    private void ResolveUiRoot()
    {
        if (uiRoot != null)
            return;

        var rootObject = GameObject.Find("UIRoot");
        if (rootObject != null)
        {
            uiRoot = rootObject.transform;
            return;
        }

        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
            uiRoot = canvas.transform;
    }
}
