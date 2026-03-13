using System;
using System.Collections.Generic;
using SaveSystem;
using UnityEngine;

public sealed class PactManager : MonoBehaviour
{
    public static PactManager Instance { get; private set; }

    [SerializeField] private List<PactDefinition> startingPacts = new List<PactDefinition>();
    [SerializeField] private bool applyStartingPactsOnAwake = false;
    [SerializeField] private bool resetRunStateOnAwake = true;
    [SerializeField] private RoomClearedEvent _roomClearedEvent;
    [Header("Domain Tags")]
    [SerializeField] private GameplayTag enemyTag;
    [SerializeField] private GameplayTag lootTag;
    [SerializeField] private GameplayTag roomTag;

    private readonly List<PactDefinition> activePacts = new List<PactDefinition>();
    private ModifierStack modifierStack;
    private PlayerRuleModifierStack playerRuleStack;
    private EnemyModifierStack enemyModifierStack;
    private LootModifierStack lootModifierStack;
    private RoomModifierStack roomModifierStack;
    private StatsProvider statsProvider;
    private PlayerRuleProvider playerRuleProvider;
    private EnemyStatsProvider enemyStatsProvider;
    private LootStatsProvider lootStatsProvider;
    private RoomStatsProvider roomStatsProvider;
    private IRunState runState;
    private PactLineSO activeLine;
    private string activeLineId;
    private int activeLineTier;
    private readonly Dictionary<PactPoolSO, Dictionary<GameplayTag, int>> runtimeRequiredAnyPactTagCountsByPool =
        new Dictionary<PactPoolSO, Dictionary<GameplayTag, int>>();
    private readonly Dictionary<PactTagAdditionalEffectsModifierEffect, int> runtimePactTagAdditionalEffectCounts =
        new Dictionary<PactTagAdditionalEffectsModifierEffect, int>();
    private readonly Dictionary<PactDefinition, Dictionary<PactModifierAsset, int>> runtimeInjectedEffectsByPact =
        new Dictionary<PactDefinition, Dictionary<PactModifierAsset, int>>();
    private bool isRebuildingRuntimeInjectedEffects;

    public StatsProvider Stats => statsProvider;
    public PlayerRuleProvider PlayerRules => playerRuleProvider;
    public EnemyStatsProvider EnemyStats => enemyStatsProvider;
    public LootStatsProvider Loot => lootStatsProvider;
    public RoomStatsProvider Rooms => roomStatsProvider;
    public IReadOnlyList<PactDefinition> ActivePacts => activePacts;
    public PactLineSO ActiveLine => activeLine;
    public string ActiveLineId => activeLineId;
    public int ActiveLineTier => activeLineTier;
    public event Action ModifiersChanged;
    public event Action<PactDefinition> PactApplied;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        PactRuntimeContext runtimeContext = PactRuntimeContext.Ensure();
        runState = runtimeContext != null ? runtimeContext.RunState : null;

        if (resetRunStateOnAwake && runState != null)
            runState.ResetRun();

        modifierStack = new ModifierStack();
        playerRuleStack = new PlayerRuleModifierStack();
        enemyModifierStack = new EnemyModifierStack();
        lootModifierStack = new LootModifierStack();
        roomModifierStack = new RoomModifierStack();
        statsProvider = new StatsProvider(modifierStack);
        playerRuleProvider = new PlayerRuleProvider(playerRuleStack);
        enemyStatsProvider = new EnemyStatsProvider(enemyModifierStack, enemyTag);
        lootStatsProvider = new LootStatsProvider(lootModifierStack, lootTag);
        roomStatsProvider = new RoomStatsProvider(roomModifierStack, roomTag);

        bool loadedFromPendingSave = TryApplyPendingPacts();
        if (!loadedFromPendingSave && applyStartingPactsOnAwake)
        {
            for (int i = 0; i < startingPacts.Count; i++)
                ApplyPact(startingPacts[i], notifyPactApplied: false);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ApplyPact(
        PactDefinition pact,
        bool notifyPactApplied = true,
        bool raiseRoomClearedEvent = true)
    {
        if (pact == null)
            return;

        activePacts.Add(pact);
        RegisterEffects(pact.Effects);
        RebuildRuntimeInjectedEffects();
        UpdateActiveLine(pact);
        runState?.RecordPact(pact);
        NotifyModifiersChanged();

        if (notifyPactApplied)
            PactApplied?.Invoke(pact);

        if (raiseRoomClearedEvent && _roomClearedEvent != null)
            _roomClearedEvent.Raise();
    }

    public void RemovePact(PactDefinition pact)
    {
        if (pact == null)
            return;

        if (!activePacts.Remove(pact))
            return;

        UnregisterEffects(pact.Effects);
        RebuildRuntimeInjectedEffects();
        NotifyModifiersChanged();
    }

    public void RegisterModifier(IStatModifier modifier)
    {
        modifierStack.Register(modifier);
        NotifyModifiersChanged();
    }

    public void UnregisterModifier(IStatModifier modifier)
    {
        modifierStack.Unregister(modifier);
        NotifyModifiersChanged();
    }

    public void RegisterRuleModifier(IPlayerRuleModifier modifier)
    {
        playerRuleStack.Register(modifier);
        NotifyModifiersChanged();
    }

    public void UnregisterRuleModifier(IPlayerRuleModifier modifier)
    {
        playerRuleStack.Unregister(modifier);
        NotifyModifiersChanged();
    }

    private void RegisterEffects(IReadOnlyList<PactModifierAsset> effects)
    {
        if (effects == null)
            return;

        for (int i = 0; i < effects.Count; i++)
            RegisterEffect(effects[i]);
    }

    private void UnregisterEffects(IReadOnlyList<PactModifierAsset> effects)
    {
        if (effects == null)
            return;

        for (int i = 0; i < effects.Count; i++)
            UnregisterEffect(effects[i]);
    }

    private void RegisterEffect(PactModifierAsset effect)
    {
        RegisterEffect(effect, includeRuntimeHooks: true);
    }

    private void RegisterEffect(PactModifierAsset effect, bool includeRuntimeHooks)
    {
        if (effect == null)
            return;

        if (effect is PactEffect pactEffect)
            modifierStack.Register(pactEffect);

        if (effect is PlayerRuleEffect playerRuleEffect)
            playerRuleStack.Register(playerRuleEffect);

        if (effect is EnemyPactEffect enemyEffect)
            enemyModifierStack.Register(enemyEffect);

        if (effect is LootPactEffect lootEffect)
            lootModifierStack.Register(lootEffect);

        if (effect is RoomPactEffect roomEffect)
            roomModifierStack.Register(roomEffect);

        if (!includeRuntimeHooks)
            return;

        if (effect is PoolRequiredAnyTagModifierEffect poolEffect)
            RegisterPoolRequiredAnyTagEffect(poolEffect);

        if (effect is PactTagAdditionalEffectsModifierEffect pactTagEffect)
            RegisterPactTagAdditionalEffects(pactTagEffect);
    }

    private void UnregisterEffect(PactModifierAsset effect)
    {
        UnregisterEffect(effect, includeRuntimeHooks: true);
    }

    private void UnregisterEffect(PactModifierAsset effect, bool includeRuntimeHooks)
    {
        if (effect == null)
            return;

        if (effect is PactEffect pactEffect)
            modifierStack.Unregister(pactEffect);

        if (effect is PlayerRuleEffect playerRuleEffect)
            playerRuleStack.Unregister(playerRuleEffect);

        if (effect is EnemyPactEffect enemyEffect)
            enemyModifierStack.Unregister(enemyEffect);

        if (effect is LootPactEffect lootEffect)
            lootModifierStack.Unregister(lootEffect);

        if (effect is RoomPactEffect roomEffect)
            roomModifierStack.Unregister(roomEffect);

        if (!includeRuntimeHooks)
            return;

        if (effect is PoolRequiredAnyTagModifierEffect poolEffect)
            UnregisterPoolRequiredAnyTagEffect(poolEffect);

        if (effect is PactTagAdditionalEffectsModifierEffect pactTagEffect)
            UnregisterPactTagAdditionalEffects(pactTagEffect);
    }

    public IReadOnlyList<GameplayTag> GetAdditionalRequiredAnyPactTagsForPool(PactPoolSO pool)
    {
        if (pool == null)
            return Array.Empty<GameplayTag>();

        if (!runtimeRequiredAnyPactTagCountsByPool.TryGetValue(pool, out Dictionary<GameplayTag, int> countsByTag) ||
            countsByTag == null ||
            countsByTag.Count == 0)
        {
            return Array.Empty<GameplayTag>();
        }

        var tags = new List<GameplayTag>(countsByTag.Count);
        foreach (KeyValuePair<GameplayTag, int> pair in countsByTag)
        {
            if (pair.Key != null && pair.Value > 0)
                tags.Add(pair.Key);
        }

        return tags;
    }

    private void RegisterPactTagAdditionalEffects(PactTagAdditionalEffectsModifierEffect effect)
    {
        AdjustRuntimePactTagAdditionalEffects(effect, +1);
    }

    private void UnregisterPactTagAdditionalEffects(PactTagAdditionalEffectsModifierEffect effect)
    {
        AdjustRuntimePactTagAdditionalEffects(effect, -1);
    }

    private void AdjustRuntimePactTagAdditionalEffects(PactTagAdditionalEffectsModifierEffect effect, int delta)
    {
        if (effect == null || delta == 0)
            return;

        runtimePactTagAdditionalEffectCounts.TryGetValue(effect, out int currentCount);
        int nextCount = currentCount + delta;

        if (nextCount <= 0)
            runtimePactTagAdditionalEffectCounts.Remove(effect);
        else
            runtimePactTagAdditionalEffectCounts[effect] = nextCount;

        RebuildRuntimeInjectedEffects();
    }

    private void RebuildRuntimeInjectedEffects()
    {
        if (isRebuildingRuntimeInjectedEffects)
            return;

        isRebuildingRuntimeInjectedEffects = true;
        try
        {
            ClearRuntimeInjectedEffects();

            if (activePacts.Count == 0 || runtimePactTagAdditionalEffectCounts.Count == 0)
                return;

            for (int i = 0; i < activePacts.Count; i++)
                ApplyRuntimeInjectedEffectsToPact(activePacts[i]);
        }
        finally
        {
            isRebuildingRuntimeInjectedEffects = false;
        }
    }

    private void ClearRuntimeInjectedEffects()
    {
        if (runtimeInjectedEffectsByPact.Count == 0)
            return;

        foreach (KeyValuePair<PactDefinition, Dictionary<PactModifierAsset, int>> pactEntry in runtimeInjectedEffectsByPact)
        {
            Dictionary<PactModifierAsset, int> effectsByCount = pactEntry.Value;
            if (effectsByCount == null || effectsByCount.Count == 0)
                continue;

            foreach (KeyValuePair<PactModifierAsset, int> effectEntry in effectsByCount)
            {
                PactModifierAsset effect = effectEntry.Key;
                int count = Mathf.Max(0, effectEntry.Value);
                for (int i = 0; i < count; i++)
                    UnregisterEffect(effect, includeRuntimeHooks: false);
            }
        }

        runtimeInjectedEffectsByPact.Clear();
    }

    private void ApplyRuntimeInjectedEffectsToPact(PactDefinition pact)
    {
        if (pact == null)
            return;

        foreach (KeyValuePair<PactTagAdditionalEffectsModifierEffect, int> injectorEntry in runtimePactTagAdditionalEffectCounts)
        {
            PactTagAdditionalEffectsModifierEffect injector = injectorEntry.Key;
            int applications = Mathf.Max(0, injectorEntry.Value);
            if (injector == null || applications <= 0 || !injector.Matches(pact))
                continue;

            IReadOnlyList<PactModifierAsset> effectsToAdd = injector.EffectsToAdd;
            if (effectsToAdd == null || effectsToAdd.Count == 0)
                continue;

            for (int application = 0; application < applications; application++)
            {
                for (int i = 0; i < effectsToAdd.Count; i++)
                {
                    PactModifierAsset additionalEffect = effectsToAdd[i];
                    if (additionalEffect == null || ReferenceEquals(additionalEffect, injector))
                        continue;

                    RegisterEffect(additionalEffect, includeRuntimeHooks: false);
                    AddRuntimeInjectedEffectCount(pact, additionalEffect);
                }
            }
        }
    }

    private void AddRuntimeInjectedEffectCount(PactDefinition pact, PactModifierAsset effect)
    {
        if (pact == null || effect == null)
            return;

        if (!runtimeInjectedEffectsByPact.TryGetValue(pact, out Dictionary<PactModifierAsset, int> effectsByCount))
        {
            effectsByCount = new Dictionary<PactModifierAsset, int>();
            runtimeInjectedEffectsByPact[pact] = effectsByCount;
        }

        effectsByCount.TryGetValue(effect, out int currentCount);
        effectsByCount[effect] = currentCount + 1;
    }

    private void NotifyModifiersChanged()
    {
        ModifiersChanged?.Invoke();
    }

    private bool TryApplyPendingPacts()
    {
        if (!PendingGameSaveState.TryGet(out GameSaveData pendingData))
            return false;

        string[] pactIds = pendingData.State.ActivePactIds;
        if (pactIds == null || pactIds.Length == 0)
            return true;

        var loadedIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < pactIds.Length; i++)
        {
            string pactId = pactIds[i];
            if (string.IsNullOrWhiteSpace(pactId) || !loadedIds.Add(pactId))
                continue;

            PactDefinition pact = ResolvePactById(pactId);
            if (pact == null)
                continue;

            ApplyPact(
                pact,
                notifyPactApplied: false,
                raiseRoomClearedEvent: false);
        }

        return true;
    }

    private static PactDefinition ResolvePactById(string pactId)
    {
        if (string.IsNullOrWhiteSpace(pactId))
            return null;

        PactDefinition[] pacts = Resources.FindObjectsOfTypeAll<PactDefinition>();
        for (int i = 0; i < pacts.Length; i++)
        {
            PactDefinition pact = pacts[i];
            if (pact == null)
                continue;

            if (string.Equals(pact.SaveId, pactId, StringComparison.Ordinal) ||
                string.Equals(pact.name, pactId, StringComparison.Ordinal))
            {
                return pact;
            }
        }

        return null;
    }

    private void UpdateActiveLine(PactDefinition pact)
    {
        if (pact == null)
            return;

        string lineId = PactIdentity.ResolveLineId(pact);
        if (lineId.Length == 0)
            return;

        int tier = pact.LineTier;
        if (tier <= 0)
        {
            Debug.LogWarning(
                $"PactManager: Line pact '{pact.name}' has invalid tier {tier}. Using tier 1.",
                this);
            tier = 1;
        }

        if (string.IsNullOrEmpty(activeLineId))
        {
            activeLineId = lineId;
            activeLine = pact.Line;
            activeLineTier = Mathf.Max(activeLineTier, tier);
            return;
        }

        if (!PactIdentity.AreEqual(activeLineId, lineId))
        {
            Debug.LogWarning(
                $"PactManager: Tried to apply line pact '{pact.name}' from '{lineId}' while active line is '{activeLineId}'.",
                this);
            return;
        }

        if (tier > activeLineTier)
            activeLineTier = tier;

        if (activeLine == null)
            activeLine = pact.Line;
    }

    private void RegisterPoolRequiredAnyTagEffect(PoolRequiredAnyTagModifierEffect effect)
    {
        ApplyPoolRequiredAnyTagEffect(effect, +1);
    }

    private void UnregisterPoolRequiredAnyTagEffect(PoolRequiredAnyTagModifierEffect effect)
    {
        ApplyPoolRequiredAnyTagEffect(effect, -1);
    }

    private void ApplyPoolRequiredAnyTagEffect(PoolRequiredAnyTagModifierEffect effect, int delta)
    {
        if (effect == null || delta == 0)
            return;

        PactPoolSO[] pools = Resources.FindObjectsOfTypeAll<PactPoolSO>();
        for (int i = 0; i < pools.Length; i++)
        {
            PactPoolSO pool = pools[i];
            if (pool == null)
                continue;

            if (!pool.HasAllPoolTags(effect.TargetPoolTags))
                continue;

            IReadOnlyList<GameplayTag> tagsToAdd = effect.RequiredAnyTagsToAdd;
            if (tagsToAdd == null || tagsToAdd.Count == 0)
                continue;

            for (int j = 0; j < tagsToAdd.Count; j++)
                AdjustRuntimePoolRequiredAnyTag(pool, tagsToAdd[j], delta);
        }
    }

    private void AdjustRuntimePoolRequiredAnyTag(PactPoolSO pool, GameplayTag tag, int delta)
    {
        if (pool == null || tag == null || delta == 0)
            return;

        if (!runtimeRequiredAnyPactTagCountsByPool.TryGetValue(pool, out Dictionary<GameplayTag, int> countsByTag))
        {
            countsByTag = new Dictionary<GameplayTag, int>();
            runtimeRequiredAnyPactTagCountsByPool[pool] = countsByTag;
        }

        countsByTag.TryGetValue(tag, out int currentCount);
        int nextCount = currentCount + delta;

        if (nextCount <= 0)
            countsByTag.Remove(tag);
        else
            countsByTag[tag] = nextCount;

        if (countsByTag.Count == 0)
            runtimeRequiredAnyPactTagCountsByPool.Remove(pool);
    }
}
