using System.Collections.Generic;
using UnityEngine;

public class CharacterStatResolver : MonoBehaviour
{
    public enum PactProviderMode
    {
        Player,
        Enemy
    }

    [SerializeField] private CharacterStats baseStats;
    [SerializeField] private List<GameplayTag> tags = new List<GameplayTag>();
    [SerializeField] private PactProviderMode pactProvider = PactProviderMode.Player;

    private readonly ModifierStack equipmentStack = new ModifierStack();
    private readonly ModifierStack runtimeStack = new ModifierStack();
    private PactManager subscribedManager;

    public event System.Action StatsChanged;

    public IReadOnlyList<GameplayTag> Tags => tags;

    private void OnEnable()
    {
        TrySubscribeToPacts();
    }

    private void OnDisable()
    {
        UnsubscribeFromPacts();
    }

    private void Update()
    {
        if (subscribedManager == null)
        {
            TrySubscribeToPacts();
            return;
        }

        if (subscribedManager != PactManager.Instance)
        {
            UnsubscribeFromPacts();
            TrySubscribeToPacts();
        }
    }

    public void RegisterEquipmentModifier(IStatModifier modifier)
    {
        equipmentStack.Register(modifier);
        NotifyStatsChanged();
    }

    public void UnregisterEquipmentModifier(IStatModifier modifier)
    {
        equipmentStack.Unregister(modifier);
        NotifyStatsChanged();
    }

    public void RegisterRuntimeModifier(IStatModifier modifier)
    {
        runtimeStack.Register(modifier);
        NotifyStatsChanged();
    }

    public void SetBaseStats(CharacterStats statsAsset)
    {
        if (statsAsset == null || baseStats == statsAsset)
            return;

        baseStats = statsAsset;
        NotifyStatsChanged();
    }

    public void UnregisterRuntimeModifier(IStatModifier modifier)
    {
        runtimeStack.Unregister(modifier);
        NotifyStatsChanged();
    }

    public float Get(StatType type)
    {
        float baseValue = baseStats != null ? baseStats.GetBaseValue(type) : 0f;
        return Resolve(type, baseValue);
    }

    public float Get(StatType type, float fallbackBaseValue)
    {
        float baseValue = baseStats != null ? baseStats.GetBaseValue(type) : fallbackBaseValue;
        return Resolve(type, baseValue);
    }

    private float Resolve(StatType type, float baseValue)
    {
        StatQuery query = new StatQuery(type, baseValue, tags);
        equipmentStack.Apply(query);
        runtimeStack.Apply(query);

        float value = query.Value;
        PactManager manager = PactManager.Instance;
        if (manager == null)
            return value;

        if (pactProvider == PactProviderMode.Enemy && manager.EnemyStats != null)
        {
            if (TryMapEnemyStatType(type, out EnemyStatType enemyType))
                return manager.EnemyStats.Get(enemyType, value, tags);

            return value;
        }

        if (manager.Stats == null)
            return value;

        return manager.Stats.Get(type, value, tags);
    }

    private static bool TryMapEnemyStatType(StatType type, out EnemyStatType enemyType)
    {
        switch (type)
        {
            case StatType.MaxHealth:
                enemyType = EnemyStatType.MaxHealth;
                return true;
            case StatType.ShieldArmor:
                enemyType = EnemyStatType.ShieldArmor;
                return true;
            case StatType.AttackDamage:
                enemyType = EnemyStatType.AttackDamage;
                return true;
            case StatType.AttackSpeed:
                enemyType = EnemyStatType.AttackSpeed;
                return true;
            case StatType.AttackRange:
                enemyType = EnemyStatType.AttackRange;
                return true;
            case StatType.DetectionRange:
                enemyType = EnemyStatType.DetectionRange;
                return true;
            case StatType.MaxSpeed:
                enemyType = EnemyStatType.MaxSpeed;
                return true;
            case StatType.Acceleration:
                enemyType = EnemyStatType.Acceleration;
                return true;
            case StatType.Deceleration:
                enemyType = EnemyStatType.Deceleration;
                return true;
            case StatType.TurnAcceleration:
                enemyType = EnemyStatType.TurnAcceleration;
                return true;
            case StatType.HitReactThreshold:
                enemyType = EnemyStatType.HitReactThreshold;
                return true;
            default:
                enemyType = default;
                return false;
        }
    }

    public void AddTag(GameplayTag tag)
    {
        if (tag == null)
            return;

        if (tags == null)
            tags = new List<GameplayTag>();

        if (tags.Contains(tag))
            return;

        tags.Add(tag);
        NotifyStatsChanged();
    }

    public void RemoveTag(GameplayTag tag)
    {
        if (tag == null || tags == null || tags.Count == 0)
            return;

        bool changed = false;
        for (int i = tags.Count - 1; i >= 0; i--)
        {
            if (tags[i] != tag)
                continue;

            tags.RemoveAt(i);
            changed = true;
        }

        if (changed)
            NotifyStatsChanged();
    }

    public void AddTags(IReadOnlyList<GameplayTag> newTags)
    {
        if (newTags == null || newTags.Count == 0)
            return;

        if (tags == null)
            tags = new List<GameplayTag>();

        bool changed = false;

        for (int i = 0; i < newTags.Count; i++)
        {
            GameplayTag tag = newTags[i];
            if (tag == null || tags.Contains(tag))
                continue;

            tags.Add(tag);
            changed = true;
        }

        if (changed)
            NotifyStatsChanged();
    }

    public void RemoveTags(IReadOnlyList<GameplayTag> tagsToRemove)
    {
        if (tagsToRemove == null || tagsToRemove.Count == 0 || tags == null || tags.Count == 0)
            return;

        bool changed = false;
        for (int i = 0; i < tagsToRemove.Count; i++)
        {
            GameplayTag tagToRemove = tagsToRemove[i];
            if (tagToRemove == null)
                continue;

            for (int j = tags.Count - 1; j >= 0; j--)
            {
                if (tags[j] != tagToRemove)
                    continue;

                tags.RemoveAt(j);
                changed = true;
            }
        }

        if (changed)
            NotifyStatsChanged();
    }

    public void ReplaceTags(IReadOnlyList<GameplayTag> newTags)
    {
        if (tags == null)
            tags = new List<GameplayTag>();

        tags.Clear();

        if (newTags != null)
        {
            for (int i = 0; i < newTags.Count; i++)
            {
                GameplayTag tag = newTags[i];
                if (tag == null || tags.Contains(tag))
                    continue;

                tags.Add(tag);
            }
        }

        NotifyStatsChanged();
    }

    public void SetPactProvider(PactProviderMode mode)
    {
        if (pactProvider == mode)
            return;

        pactProvider = mode;
        NotifyStatsChanged();
    }

    private void TrySubscribeToPacts()
    {
        if (subscribedManager != null)
            return;

        PactManager manager = PactManager.Instance;
        if (manager == null)
            return;

        subscribedManager = manager;
        subscribedManager.ModifiersChanged += HandlePactModifiersChanged;
    }

    private void UnsubscribeFromPacts()
    {
        if (subscribedManager == null)
            return;

        subscribedManager.ModifiersChanged -= HandlePactModifiersChanged;
        subscribedManager = null;
    }

    private void HandlePactModifiersChanged()
    {
        NotifyStatsChanged();
    }

    private void NotifyStatsChanged()
    {
        StatsChanged?.Invoke();
    }
}
