using UnityEngine;

public sealed class PactRuntimeContext : MonoBehaviour
{
    public static PactRuntimeContext Instance { get; private set; }

    [SerializeField] private bool persistentAcrossScenes = true;

    private IRunState runState;
    private INpcSelector npcSelector;
    private IPactOfferService pactOfferService;

    public IRunState RunState => runState;
    public INpcSelector NpcSelector => npcSelector;
    public IPactOfferService PactOfferService => pactOfferService;

    public static PactRuntimeContext Ensure()
    {
        if (Instance != null)
            return Instance;

        PactRuntimeContext existing = FindObjectOfType<PactRuntimeContext>();
        if (existing != null)
        {
            existing.EnsureInitialized();
            return existing;
        }

        GameObject go = new GameObject(nameof(PactRuntimeContext));
        PactRuntimeContext created = go.AddComponent<PactRuntimeContext>();
        created.EnsureInitialized();
        return created;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureInitialized();

        if (persistentAcrossScenes)
            DontDestroyOnLoad(gameObject);
    }

    public void ResetRun()
    {
        runState?.ResetRun();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void EnsureInitialized()
    {
        if (runState != null && npcSelector != null && pactOfferService != null)
            return;

        runState = new RunState();
        npcSelector = new WeightedNpcSelector(runState);
        pactOfferService = new PactOfferService(runState);
    }
}
