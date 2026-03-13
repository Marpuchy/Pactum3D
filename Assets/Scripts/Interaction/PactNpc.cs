using System;
using System.Collections.Generic;
using SaveSystem;
using UnityEngine;
using UnityEngine.Serialization;

public sealed class PactNpc : InteractableBase
{
    [Header("Identity")]
    [SerializeField] private string npcId;

    [Header("Pools")]
    [SerializeField, FormerlySerializedAs("pactPool")] private PactPoolSO generalPactPool;
    [SerializeField, FormerlySerializedAs("linePool")] private PactPoolSO ownPactPool;

    [SerializeField] private int offerCount = 3;
    [SerializeField] private OfferPactEventChannelSO offerPactChannel;
    [SerializeField] private PactSelectedEventChannelSO pactSelectedChannel;
    [SerializeField] private OpenDialogueEventChannelSO openDialogueChannel;
    [SerializeField] private DialogueDefinition dialogue;

    private bool pactChosen;
    private IRunState runState;
    private INpcSelector npcSelector;
    private IPactOfferService offerService;
    private readonly List<PactPoolSO> extraGeneralLikePactPools = new List<PactPoolSO>();
    private readonly List<PactPoolSO> extraOwnLikePactPools = new List<PactPoolSO>();
    private Sprite pactCanvasSprite;

    public bool PactChosen => pactChosen;
    public string NpcId => ResolveNpcId();
    public string PactLineId => ResolvePactLineId();

    private void Awake()
    {
        PactRuntimeContext runtimeContext = PactRuntimeContext.Ensure();
        runState = runtimeContext != null ? runtimeContext.RunState : null;
        npcSelector = runtimeContext != null ? runtimeContext.NpcSelector : null;
        offerService = runtimeContext != null ? runtimeContext.PactOfferService : null;
    }

    protected override void OnInteract(Interactor interactor)
    {
        if (pactChosen)
            return;

        if (dialogue != null && openDialogueChannel != null)
            openDialogueChannel.Raise(new OpenDialogueRequest(interactor, dialogue));

        if (offerPactChannel == null)
            return;

        List<PactDefinition> offers = BuildOffers();
        if (offers.Count == 0)
        {
            Debug.LogWarning($"{nameof(PactNpc)}: No available pacts to offer.", this);
            return;
        }

        Dictionary<string, PactPoolSO> offerSourcePools = BuildOfferSourcePoolMap(offers);
        offerPactChannel.Raise(new OfferPactRequest(
            interactor,
            offers,
            OnPactSelected,
            ResolvePactCanvasSprite(),
            offerSourcePools));
    }

    public override bool CanInteract(Interactor interactor)
    {
        return base.CanInteract(interactor) &&
               offerPactChannel != null &&
               !SaveSystem.PendingGameSaveState.TryGet(out _) &&
               !pactChosen &&
               IsCurrentNpcEligible() &&
               GetUsablePactCount() > 0;
    }

    public void RestorePactChosenState(bool value)
    {
        pactChosen = value;
    }

    public void ApplyRuntimeDefinition(NpcDefinitionSO definition)
    {
        if (definition == null)
            return;

        string runtimeNpcId = PactIdentity.Normalize(definition.NpcId);
        if (!string.IsNullOrEmpty(runtimeNpcId))
            npcId = runtimeNpcId;

        generalPactPool = definition.GeneralPactPool;
        ownPactPool = definition.OwnPactPool;
        pactCanvasSprite = definition.PactCanvasSprite;
        extraGeneralLikePactPools.Clear();
        extraOwnLikePactPools.Clear();

        IReadOnlyList<NpcExtraPactPullEntry> extraPulls = definition.ExtraPactPulls;
        if (extraPulls != null)
        {
            for (int i = 0; i < extraPulls.Count; i++)
            {
                NpcExtraPactPullEntry entry = extraPulls[i];
                if (entry == null || entry.Pool == null)
                    continue;

                if (entry.Mode == NpcExtraPactPullMode.LikeOwn)
                    AddUniquePool(extraOwnLikePactPools, entry.Pool);
                else
                    AddUniquePool(extraGeneralLikePactPools, entry.Pool);
            }
        }

        if (generalPactPool == null || ownPactPool == null)
        {
            Debug.LogWarning(
                $"PactNpc '{name}': NPC Definition '{definition.name}' requires both General and Own pact pools.",
                this);
        }
    }

    private int GetUsablePactCount()
    {
        if (offerService == null)
            return 0;

        PactManager manager = PactManager.Instance;
        IReadOnlyList<PactDefinition> activePacts = manager != null ? manager.ActivePacts : null;
        List<PactDefinition> generalCandidates = CollectEligiblePactsFromPools(generalPactPool, extraGeneralLikePactPools, activePacts, manager);
        List<PactDefinition> ownCandidates = CollectEligiblePactsFromPools(ownPactPool, extraOwnLikePactPools, activePacts, manager);
        return CountUniqueCandidates(generalCandidates, ownCandidates);
    }

    private List<PactDefinition> BuildOffers()
    {
        if (TryBuildOffersFromLoadedSnapshot(out List<PactDefinition> loadedSnapshotOffers))
            return loadedSnapshotOffers;

        if (offerService == null)
            return new List<PactDefinition>();

        PactManager manager = PactManager.Instance;
        IReadOnlyList<PactDefinition> activePacts = manager != null ? manager.ActivePacts : null;
        List<PactDefinition> generalCandidates = CollectEligiblePactsFromPools(generalPactPool, extraGeneralLikePactPools, activePacts, manager);
        List<PactDefinition> ownCandidates = CollectEligiblePactsFromPools(ownPactPool, extraOwnLikePactPools, activePacts, manager);
        SortCandidatesByKey(generalCandidates);
        SortCandidatesByKey(ownCandidates);
        int maxOffers = Mathf.Max(1, offerCount);
        System.Random random = CreateOfferRandom();

        return BuildBalancedOffers(generalCandidates, ownCandidates, maxOffers, random);
    }

    public bool TryBuildOfferSnapshot(
        out string resolvedNpcId,
        out int offerSeed,
        out string[] offerPactIds)
    {
        resolvedNpcId = ResolveNpcId();
        offerSeed = ResolveOfferSeed();
        offerPactIds = Array.Empty<string>();

        List<PactDefinition> offers = BuildOffers();
        if (offers == null || offers.Count == 0 || string.IsNullOrEmpty(resolvedNpcId))
            return false;

        var ids = new List<string>(offers.Count);
        for (int i = 0; i < offers.Count; i++)
        {
            string pactId = ResolveSnapshotPactId(offers[i]);
            if (pactId.Length > 0)
                ids.Add(pactId);
        }

        if (ids.Count == 0)
            return false;

        offerPactIds = ids.ToArray();
        return true;
    }

    private bool TryBuildOffersFromLoadedSnapshot(out List<PactDefinition> offers)
    {
        offers = null;

        if (!LoadedNpcRoomOfferState.TryGetOfferPactIdsForCurrentRoom(ResolveNpcId(), out string[] savedPactIds))
            return false;

        List<PactDefinition> resolved = BuildOffersFromSavedIds(savedPactIds);
        if (resolved.Count == 0)
            return false;

        int maxOffers = Mathf.Max(1, offerCount);
        if (resolved.Count > maxOffers)
            resolved.RemoveRange(maxOffers, resolved.Count - maxOffers);

        offers = resolved;
        return true;
    }

    private static List<PactDefinition> BuildOffersFromSavedIds(IReadOnlyList<string> savedPactIds)
    {
        var resolved = new List<PactDefinition>();
        if (savedPactIds == null || savedPactIds.Count == 0)
            return resolved;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < savedPactIds.Count; i++)
        {
            string savedId = PactIdentity.Normalize(savedPactIds[i]);
            if (savedId.Length == 0 || !seen.Add(savedId))
                continue;

            PactDefinition pact = ResolvePactById(savedId);
            if (pact != null)
                resolved.Add(pact);
        }

        return resolved;
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

            string candidateId = ResolveSnapshotPactId(pact);
            if (candidateId.Length == 0)
                continue;

            if (PactIdentity.AreEqual(candidateId, pactId))
                return pact;
        }

        return null;
    }

    private List<PactDefinition> CollectEligiblePactsFromPools(
        PactPoolSO primaryPool,
        IReadOnlyList<PactPoolSO> extraPools,
        IReadOnlyList<PactDefinition> activePacts,
        PactManager manager)
    {
        var candidates = new List<PactDefinition>();
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);

        CollectEligiblePactsFromPool(primaryPool, activePacts, manager, candidates, seenKeys);
        if (extraPools != null)
        {
            for (int i = 0; i < extraPools.Count; i++)
                CollectEligiblePactsFromPool(extraPools[i], activePacts, manager, candidates, seenKeys);
        }

        return candidates;
    }

    private void CollectEligiblePactsFromPool(
        PactPoolSO pool,
        IReadOnlyList<PactDefinition> activePacts,
        PactManager manager,
        List<PactDefinition> destination,
        HashSet<string> seenKeys)
    {
        if (pool == null || destination == null || seenKeys == null)
            return;

        IReadOnlyList<GameplayTag> runtimeRequiredAnyTags =
            manager != null ? manager.GetAdditionalRequiredAnyPactTagsForPool(pool) : null;

        IReadOnlyList<PactDefinition> poolPacts = pool.ResolvePacts(runtimeRequiredAnyTags);
        for (int i = 0; i < poolPacts.Count; i++)
        {
            PactDefinition pact = poolPacts[i];
            if (!offerService.IsPactEligible(pact, activePacts))
                continue;

            AddUniquePact(destination, seenKeys, pact);
        }
    }

    private static void AddUniquePool(List<PactPoolSO> pools, PactPoolSO pool)
    {
        if (pools == null || pool == null || pools.Contains(pool))
            return;

        pools.Add(pool);
    }

    private static int CountUniqueCandidates(
        List<PactDefinition> generalCandidates,
        List<PactDefinition> ownCandidates)
    {
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        AddCandidateKeys(generalCandidates, seenKeys);
        AddCandidateKeys(ownCandidates, seenKeys);
        return seenKeys.Count;
    }

    private static void AddCandidateKeys(
        List<PactDefinition> candidates,
        HashSet<string> destination)
    {
        if (candidates == null || destination == null)
            return;

        for (int i = 0; i < candidates.Count; i++)
        {
            string key = ResolvePactKey(candidates[i]);
            if (key.Length > 0)
                destination.Add(key);
        }
    }

    private List<PactDefinition> BuildBalancedOffers(
        List<PactDefinition> generalCandidates,
        List<PactDefinition> ownCandidates,
        int maxOffers,
        System.Random random)
    {
        var offers = new List<PactDefinition>(maxOffers);
        var selectedKeys = new HashSet<string>(StringComparer.Ordinal);
        if (random == null)
            random = new System.Random(1);

        if (maxOffers > 0 && ownCandidates != null && ownCandidates.Count > 0)
        {
            PactDefinition guaranteedOwn = TakeRandomUnique(ownCandidates, selectedKeys, random);
            if (guaranteedOwn != null)
            {
                offers.Add(guaranteedOwn);
                string guaranteedOwnKey = ResolvePactKey(guaranteedOwn);
                RemoveByKey(generalCandidates, guaranteedOwnKey);
                RemoveByKey(ownCandidates, guaranteedOwnKey);
            }
        }

        while (offers.Count < maxOffers)
        {
            bool hasGeneral = generalCandidates != null && generalCandidates.Count > 0;
            bool hasOwn = ownCandidates != null && ownCandidates.Count > 0;
            if (!hasGeneral && !hasOwn)
                break;

            bool pickOwn = hasOwn && (!hasGeneral || random.NextDouble() < 0.5d);
            List<PactDefinition> primaryPool = pickOwn ? ownCandidates : generalCandidates;
            List<PactDefinition> secondaryPool = pickOwn ? generalCandidates : ownCandidates;

            PactDefinition selected = TakeRandomUnique(primaryPool, selectedKeys, random);
            if (selected == null)
                selected = TakeRandomUnique(secondaryPool, selectedKeys, random);

            if (selected == null)
                break;

            offers.Add(selected);
            string selectedKey = ResolvePactKey(selected);
            RemoveByKey(generalCandidates, selectedKey);
            RemoveByKey(ownCandidates, selectedKey);
        }

        return offers;
    }

    private static PactDefinition TakeRandomUnique(
        List<PactDefinition> source,
        HashSet<string> selectedKeys,
        System.Random random)
    {
        if (source == null || selectedKeys == null || source.Count == 0)
            return null;

        while (source.Count > 0)
        {
            int index = random.Next(source.Count);
            PactDefinition candidate = source[index];
            source.RemoveAt(index);

            if (candidate == null)
                continue;

            string key = ResolvePactKey(candidate);
            if (key.Length == 0 || !selectedKeys.Add(key))
                continue;

            return candidate;
        }

        return null;
    }

    private static void RemoveByKey(List<PactDefinition> source, string pactKey)
    {
        if (source == null || source.Count == 0 || pactKey.Length == 0)
            return;

        for (int i = source.Count - 1; i >= 0; i--)
        {
            if (ResolvePactKey(source[i]) == pactKey)
                source.RemoveAt(i);
        }
    }

    private static void AddUniquePact(
        List<PactDefinition> destination,
        HashSet<string> seenKeys,
        PactDefinition pact)
    {
        if (destination == null || seenKeys == null || pact == null)
            return;

        string key = ResolvePactKey(pact);
        if (key.Length == 0 || !seenKeys.Add(key))
            return;

        destination.Add(pact);
    }

    private static string ResolvePactKey(PactDefinition pact)
    {
        if (pact == null)
            return string.Empty;

        string pactId = PactIdentity.ResolvePactId(pact);
        if (!string.IsNullOrEmpty(pactId))
            return pactId;

        string assetName = PactIdentity.Normalize(pact.name);
        if (!string.IsNullOrEmpty(assetName))
            return assetName;

        return pact.GetInstanceID().ToString();
    }

    private static string ResolveSnapshotPactId(PactDefinition pact)
    {
        if (pact == null)
            return string.Empty;

        string pactId = PactIdentity.ResolvePactId(pact);
        if (!string.IsNullOrEmpty(pactId))
            return pactId;

        return PactIdentity.Normalize(pact.name);
    }

    private System.Random CreateOfferRandom()
    {
        return new System.Random(ResolveOfferSeed());
    }

    private int ResolveOfferSeed()
    {
        int roomSeed = 1;
        if (RoomBuilder.Current != null)
        {
            if (RoomBuilder.Current.CurrentRoomSeed != 0)
                roomSeed = RoomBuilder.Current.CurrentRoomSeed;
            else if (RoomBuilder.Current.CurrentRunSeed != 0)
                roomSeed = RoomBuilder.Current.CurrentRunSeed;
        }

        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + SanitizeSeed(roomSeed);
            hash = (hash * 31) + ComputeStableHash(ResolveNpcId());
            hash = (hash * 31) + ComputeStableHash(ResolvePactLineId());
            hash = (hash * 31) + ComputeStableHash(ResolvePoolKey(ownPactPool));
            hash = (hash * 31) + ComputeStableHash(ResolvePoolKey(generalPactPool));
            return SanitizeSeed(hash);
        }
    }

    private static int SanitizeSeed(int seed)
    {
        return seed == 0 ? 1 : seed;
    }

    private static int ComputeStableHash(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        unchecked
        {
            int hash = 23;
            for (int i = 0; i < value.Length; i++)
                hash = (hash * 31) + value[i];
            return hash;
        }
    }

    private static void SortCandidatesByKey(List<PactDefinition> candidates)
    {
        if (candidates == null || candidates.Count <= 1)
            return;

        candidates.Sort((left, right) => StringComparer.Ordinal.Compare(ResolvePactKey(left), ResolvePactKey(right)));
    }

    private static string ResolvePoolKey(PactPoolSO pool)
    {
        if (pool == null)
            return string.Empty;

        string normalizedName = PactIdentity.Normalize(pool.name);
        if (normalizedName.Length > 0)
            return normalizedName;

        return pool.GetInstanceID().ToString();
    }

    private Dictionary<string, PactPoolSO> BuildOfferSourcePoolMap(IReadOnlyList<PactDefinition> offers)
    {
        var map = new Dictionary<string, PactPoolSO>(StringComparer.Ordinal);
        if (offers == null || offers.Count == 0)
            return map;

        PactManager manager = PactManager.Instance;
        IReadOnlyList<PactDefinition> activePacts = manager != null ? manager.ActivePacts : null;

        for (int i = 0; i < offers.Count; i++)
        {
            PactDefinition pact = offers[i];
            if (pact == null)
                continue;

            string pactId = ResolveSnapshotPactId(pact);
            if (pactId.Length == 0 || map.ContainsKey(pactId))
                continue;

            PactPoolSO sourcePool = ResolveSourcePoolForPact(pact, manager, activePacts);
            if (sourcePool != null)
                map[pactId] = sourcePool;
        }

        return map;
    }

    private PactPoolSO ResolveSourcePoolForPact(
        PactDefinition pact,
        PactManager manager,
        IReadOnlyList<PactDefinition> activePacts)
    {
        if (pact == null)
            return null;

        if (PoolContainsPact(ownPactPool, pact, manager, activePacts))
            return ownPactPool;

        if (extraOwnLikePactPools != null)
        {
            for (int i = 0; i < extraOwnLikePactPools.Count; i++)
            {
                PactPoolSO extraOwnPool = extraOwnLikePactPools[i];
                if (PoolContainsPact(extraOwnPool, pact, manager, activePacts))
                    return extraOwnPool;
            }
        }

        if (PoolContainsPact(generalPactPool, pact, manager, activePacts))
            return generalPactPool;

        if (extraGeneralLikePactPools != null)
        {
            for (int i = 0; i < extraGeneralLikePactPools.Count; i++)
            {
                PactPoolSO extraGeneralPool = extraGeneralLikePactPools[i];
                if (PoolContainsPact(extraGeneralPool, pact, manager, activePacts))
                    return extraGeneralPool;
            }
        }

        return null;
    }

    private bool PoolContainsPact(
        PactPoolSO pool,
        PactDefinition targetPact,
        PactManager manager,
        IReadOnlyList<PactDefinition> activePacts)
    {
        if (pool == null || targetPact == null)
            return false;

        IReadOnlyList<GameplayTag> runtimeRequiredAnyTags =
            manager != null ? manager.GetAdditionalRequiredAnyPactTagsForPool(pool) : null;

        IReadOnlyList<PactDefinition> poolPacts = pool.ResolvePacts(runtimeRequiredAnyTags);
        for (int i = 0; i < poolPacts.Count; i++)
        {
            PactDefinition candidate = poolPacts[i];
            if (candidate == null)
                continue;

            if (offerService != null && !offerService.IsPactEligible(candidate, activePacts))
                continue;

            if (ReferenceEquals(candidate, targetPact))
                return true;

            if (ResolvePactKey(candidate) == ResolvePactKey(targetPact))
                return true;
        }

        return false;
    }

    private void OnPactSelected(PactDefinition selected)
    {
        if (selected == null)
            return;

        pactChosen = true;

        string selectedLineId = PactIdentity.ResolveLineId(selected);
        bool isGenericLine = PactIdentity.AreEqual(
            selectedLineId,
            PactTagUtility.RemoveTagSuffix(PactTagUtility.GenericLineTagName));

        if (!string.IsNullOrEmpty(selectedLineId) && !isGenericLine)
            runState?.LockTo(ResolveNpcId(), selectedLineId);

        pactSelectedChannel?.Raise(new PactSelectionContext(
            selected,
            ResolveNpcId(),
            selectedLineId));
    }

    private bool IsCurrentNpcEligible()
    {
        return npcSelector == null || npcSelector.IsNpcEligible(ResolveNpcId(), PactLineId);
    }

    private string ResolveNpcId()
    {
        string explicitId = PactIdentity.Normalize(npcId);
        if (!string.IsNullOrEmpty(explicitId))
            return explicitId;

        string sourceName = gameObject != null ? gameObject.name : string.Empty;
        const string cloneSuffix = "(Clone)";
        if (sourceName.EndsWith(cloneSuffix))
            sourceName = sourceName.Replace(cloneSuffix, string.Empty);

        return PactIdentity.Normalize(sourceName);
    }

    private string ResolvePactLineId()
    {
        string fromOwnPool = ResolveLineIdFromPool(ownPactPool);
        if (!string.IsNullOrEmpty(fromOwnPool))
            return fromOwnPool;

        if (extraOwnLikePactPools != null)
        {
            for (int i = 0; i < extraOwnLikePactPools.Count; i++)
            {
                string fromExtraOwn = ResolveLineIdFromPool(extraOwnLikePactPools[i]);
                if (!string.IsNullOrEmpty(fromExtraOwn))
                    return fromExtraOwn;
            }
        }

        return string.Empty;
    }

    private static string ResolveLineIdFromPool(PactPoolSO pool)
    {
        if (pool == null)
            return string.Empty;

        string fromRequiredAll = ResolveLineIdFromTags(pool.RequiredAllPactTags);
        if (fromRequiredAll.Length > 0)
            return fromRequiredAll;

        string fromRequiredAny = ResolveLineIdFromTags(pool.RequiredAnyPactTags);
        if (fromRequiredAny.Length > 0)
            return fromRequiredAny;

        string fromPoolTags = ResolveLineIdFromTags(pool.PoolTags);
        if (fromPoolTags.Length > 0)
            return fromPoolTags;

        IReadOnlyList<PactDefinition> poolPacts = pool.ResolvePacts();
        string resolved = string.Empty;
        for (int i = 0; i < poolPacts.Count; i++)
        {
            string lineId = PactIdentity.ResolveLineId(poolPacts[i]);
            if (lineId.Length == 0)
                continue;

            if (resolved.Length == 0)
            {
                resolved = lineId;
                continue;
            }

            if (!PactIdentity.AreEqual(resolved, lineId))
                return string.Empty;
        }

        return resolved;
    }

    private static string ResolveLineIdFromTags(IReadOnlyList<GameplayTag> tags)
    {
        if (tags == null || tags.Count == 0)
            return string.Empty;

        string lineId = PactTagUtility.ResolveLineIdFromTags(tags);
        return PactIdentity.Normalize(lineId);
    }

    private Sprite ResolvePactCanvasSprite()
    {
        if (pactCanvasSprite != null)
            return pactCanvasSprite;

        SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>();
        return renderer != null ? renderer.sprite : null;
    }
}
