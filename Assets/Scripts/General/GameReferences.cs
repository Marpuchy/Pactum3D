using UnityEngine;

public class GameReferences : MonoBehaviour
{
    public static GameReferences Instance { get; private set; }

    [Header("Global References")]
    [SerializeField] private ItemCatalog itemCatalog;
    public ItemCatalog ItemCatalog => itemCatalog;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (itemCatalog == null)
            Debug.LogWarning("GameReferences: ItemCatalog not assigned!");
    }
}